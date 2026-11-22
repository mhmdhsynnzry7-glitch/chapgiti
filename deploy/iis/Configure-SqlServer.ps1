#Requires -RunAsAdministrator
param(
  [string]$AppRoot = (Join-Path $PSScriptRoot '..\..'),
  [string]$ServerName = '.',
  [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,63}$')][string]$Database = 'ChapGiti',
  [string]$DataDirectory = 'C:\Giti\data'
)
$ErrorActionPreference = 'Stop'
$resolvedApp = (Resolve-Path -LiteralPath $AppRoot).Path
$localNames = @('.','(local)','localhost','127.0.0.1',$env:COMPUTERNAME)
$serverBase = ($ServerName -split '\\')[0]
if ($serverBase -notin $localNames) { throw 'This helper configures local SQL Server with Windows authentication. Configure SQL authentication in .env.local for a remote database.' }
if (-not [IO.Path]::IsPathRooted($DataDirectory)) { throw 'DataDirectory must be absolute.' }
$dataRoot = [IO.Path]::GetFullPath($DataDirectory).TrimEnd('\')
if ($dataRoot -eq [IO.Path]::GetPathRoot($dataRoot).TrimEnd('\') -or $dataRoot -eq $resolvedApp -or $dataRoot.StartsWith($resolvedApp + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a dedicated private data directory outside the application.' }
$envFile = Join-Path $resolvedApp '.env.local'
if (-not (Test-Path -LiteralPath $envFile)) { [IO.File]::Copy((Join-Path $resolvedApp '.env.example'),$envFile) }
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = $ServerName
$builder['Initial Catalog'] = $Database
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $true
$builder['TrustServerCertificate'] = $true
$existingDatabase = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
  $existingDatabase.Open()
} catch {
  throw "The database '$Database' must already exist and be accessible. This script never creates a database."
} finally { $existingDatabase.Dispose() }

$content = [IO.File]::ReadAllText($envFile)
function Set-ConfigValue([string]$Name,[string]$Value) {
  $pattern = '(?m)^' + [regex]::Escape($Name) + '=.*$'
  if ([regex]::IsMatch($script:content,$pattern)) { $script:content = [regex]::Replace($script:content,$pattern,[Text.RegularExpressions.MatchEvaluator]{ param($match) return $Name + '=' + $Value }) }
  else { $script:content = $script:content.TrimEnd() + [Environment]::NewLine + $Name + '=' + $Value + [Environment]::NewLine }
}
Set-ConfigValue 'SQLSERVER_CONNECTION_STRING' $builder.ConnectionString
Set-ConfigValue 'DOTNET_API_URL' 'http://127.0.0.1:5100'
Set-ConfigValue 'DATA_DIR' $dataRoot
[IO.File]::WriteAllText($envFile,$content,[Text.UTF8Encoding]::new($false))
& icacls.exe $envFile /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' '*S-1-5-19:R' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect .env.local.' }
[IO.Directory]::CreateDirectory($dataRoot) | Out-Null
& icacls.exe $dataRoot /grant '*S-1-5-19:(OI)(CI)M' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not grant the service access to the private data folder.' }
$frontendRoot = @('frontend','frontend\publish','frontend\.next\standalone') | ForEach-Object { Join-Path $resolvedApp $_ } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'server.js') } | Select-Object -First 1
if ($frontendRoot) {
  $frontendCache = Join-Path $frontendRoot '.next\cache'
  [IO.Directory]::CreateDirectory($frontendCache) | Out-Null
  & icacls.exe $frontendCache /grant '*S-1-5-19:(OI)(CI)M' | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Could not grant the service access to the Next.js cache.' }
}

$previousNodeEnv = [Environment]::GetEnvironmentVariable('NODE_ENV','Process')
Push-Location -LiteralPath $resolvedApp
try {
  $env:NODE_ENV = 'production'
  & node.exe 'scripts\db-setup.mjs'
  if ($LASTEXITCODE -ne 0) { throw 'SQL Server migration failed. Run this helper using a Windows account with SQL Server setup permissions.' }
  $connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
  try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @"
IF SUSER_ID(N'NT AUTHORITY\LOCAL SERVICE') IS NULL CREATE LOGIN [NT AUTHORITY\LOCAL SERVICE] FROM WINDOWS;
IF USER_ID(N'NT AUTHORITY\LOCAL SERVICE') IS NULL CREATE USER [NT AUTHORITY\LOCAL SERVICE] FOR LOGIN [NT AUTHORITY\LOCAL SERVICE];
IF IS_ROLEMEMBER(N'db_datareader',N'NT AUTHORITY\LOCAL SERVICE') <> 1 ALTER ROLE db_datareader ADD MEMBER [NT AUTHORITY\LOCAL SERVICE];
IF IS_ROLEMEMBER(N'db_datawriter',N'NT AUTHORITY\LOCAL SERVICE') <> 1 ALTER ROLE db_datawriter ADD MEMBER [NT AUTHORITY\LOCAL SERVICE];
"@
    [void]$command.ExecuteNonQuery()
  } finally { $connection.Dispose() }
  Write-Host 'The existing SQL Server database schema is ready. The existing LocalService account has read/write access to this database.'
  Write-Host 'No IIS bindings, sites or Windows services were changed.'
} finally {
  Pop-Location
  [Environment]::SetEnvironmentVariable('NODE_ENV',$previousNodeEnv,'Process')
}
