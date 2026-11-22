[CmdletBinding(DefaultParameterSetName='Select')]
param(
    [Parameter(Mandatory, ParameterSetName='Create')][switch]$Create,
    [Parameter(Mandatory, ParameterSetName='Select')]
    [ValidatePattern('^[a-f0-9]{32}$')][string]$Id,
    [Parameter(ParameterSetName='Select')][switch]$Remove
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ((Get-Location).Path -ne $repo -or (Test-Path 'RELEASE-MANIFEST.json')) {
    throw 'Use the source repository root in a dedicated Development PowerShell.'
}
# A failed selection must not leave a usable working database in this shell.
$env:SQLSERVER_CONNECTION_STRING = 'INVALID_UNSELECTED_DEVELOPMENT_TARGET'
$env:ConnectionStrings__SqlServer = $env:SQLSERVER_CONNECTION_STRING
if ($Create) { $Id = [Guid]::NewGuid().ToString('N') }
$instance = "ChapGitiDev_$Id"
$database = "ChapGiti_Dev_$Id"
$server = "(localdb)\$instance"
$privateRoot = Join-Path $repo ".qa\dev-$Id"
$marker = Join-Path $privateRoot 'instance.txt'
$catalogMarker = Join-Path $privateRoot 'catalog.txt'

function Invoke-LocalDb([string[]]$Arguments) {
    & SqlLocalDB.exe @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "SqlLocalDB $($Arguments[0]) failed." }
}
function Invoke-LocalSql([string]$Catalog, [string]$Statement) {
    $result = & sqlcmd.exe -S $server -d $Catalog -E -C -b -l 8 -h -1 -W -Q "SET NOCOUNT ON; $Statement"
    if ($LASTEXITCODE -ne 0) { throw "SQL guard or operation failed on $instance." }
    ($result -join '').Trim()
}
$masterGuard = "IF DB_NAME()<>'master' OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 OR EXISTS(SELECT 1 FROM sys.databases WHERE database_id>4 AND name<>N'$database') THROW 51000,'Unexpected server or catalog inventory',1;"
if ($Create) {
    $instances = & SqlLocalDB.exe info
    if ($LASTEXITCODE -ne 0 -or $instances -contains $instance -or (Test-Path -LiteralPath $privateRoot)) { throw 'Could not reserve a new, unique fixture identity.' }
    New-Item -ItemType Directory -Path $privateRoot | Out-Null
    Invoke-LocalDb @('create', $instance, '15.0')
    Invoke-LocalDb @('start', $instance)
    # LocalDB's runtime ServerName changes after a stop/start. The master file belongs
    # to the generated instance directory, and the catalog has its own persistent GUID.
    $identity = Invoke-LocalSql master "$masterGuard IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id>4) THROW 51001,'Expected an empty instance',1; SELECT physical_name FROM sys.master_files WHERE database_id=1 AND file_id=1;"
    if ((Split-Path -Leaf (Split-Path -Parent $identity)) -ne $instance -or (Split-Path -Leaf $identity) -ne 'master.mdf') { throw 'Unexpected LocalDB master file.' }
    [IO.File]::WriteAllText($marker, $identity)
    Write-Host "Reserved isolated Development ID: $Id"
} else {
    if (!(Test-Path -LiteralPath $marker -PathType Leaf)) { throw 'The ownership marker for this Development ID is missing.' }
    Invoke-LocalDb @('start', $instance)
    $identity = [IO.File]::ReadAllText($marker)
}
$expected = $identity.Replace("'", "''")
$identityGuard = "IF NOT EXISTS(SELECT 1 FROM sys.master_files WHERE database_id=1 AND file_id=1 AND physical_name=N'$expected') OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 THROW 51002,'LocalDB identity mismatch',1;"
if ($Create) {
    Invoke-LocalSql master "$identityGuard $masterGuard CREATE DATABASE [$database];"
    $broker = Invoke-LocalSql master "$identityGuard $masterGuard SELECT CONVERT(char(36),service_broker_guid) FROM sys.databases WHERE name=N'$database';"
    [IO.File]::WriteAllText($catalogMarker, ([guid]::Parse($broker)).ToString())
}
$ownedCatalog = ([guid]::Parse([IO.File]::ReadAllText($catalogMarker))).ToString()
$catalogGuard = "IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=N'$database' AND service_broker_guid='$ownedCatalog') THROW 51004,'Catalog identity mismatch',1;"
if ($Remove) {
    Invoke-LocalSql master "$identityGuard $masterGuard $catalogGuard DROP DATABASE [$database]; IF DB_ID(N'$database') IS NOT NULL THROW 51003,'Removal failed',1;"
    Invoke-LocalDb @('stop', $instance)
    Invoke-LocalDb @('delete', $instance)
    Remove-Item -LiteralPath $marker,$catalogMarker
    $initialCredential = Join-Path $privateRoot 'initial-admin.xml'
    if (Test-Path -LiteralPath $initialCredential -PathType Leaf) { Remove-Item -LiteralPath $initialCredential }
    Write-Host 'Disposable SQL database and instance removed. Close this PowerShell.'
    return
}
Invoke-LocalSql master "$identityGuard $masterGuard $catalogGuard"
Invoke-LocalSql $database "$identityGuard $catalogGuard IF DB_NAME()<>N'$database' THROW 51005,'Unexpected selected database',1;"
$env:SQLSERVER_CONNECTION_STRING = "Server=$server;Database=$database;Integrated Security=True;Encrypt=False;Pooling=False"
$env:ConnectionStrings__SqlServer = $env:SQLSERVER_CONNECTION_STRING
$env:CHAP_DEV_SANDBOX_ID = $Id
$env:DATA_DIR = Join-Path $privateRoot 'private'
$env:NODE_ENV = 'development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:SMS_ENABLED = 'false'
$env:OTP_ENABLED = 'false'
$env:OTP_VERIFICATION_ENABLED = 'true'
$env:INDEXING_ENABLED = 'false'
$env:TRUST_IIS_PROXY = 'false'
$env:SITE_URL = 'http://127.0.0.1:3000'
$env:PORT = '3000'
$env:DOTNET_API_URL = 'http://127.0.0.1:5100'
Write-Host "Selected $server / $database for this PowerShell only. Development ID: $Id"
