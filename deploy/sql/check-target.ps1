[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ExpectedServer,
      [Parameter(Mandatory=$true)][ValidateSet('Before','After')][string]$Phase)
$ErrorActionPreference='Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$inventory = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'MIGRATIONS-CHECKSUMS.json') | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($env:SQLSERVER_CONNECTION_STRING)) { throw 'Set SQLSERVER_CONNECTION_STRING in this PowerShell session for both preflight and the manual runner.' }
$cs = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($env:SQLSERVER_CONNECTION_STRING)
if ($cs.InitialCatalog -ne 'ChapGiti' -or $cs.AttachDBFilename -or $cs.UserInstance) { throw 'Expected an explicitly configured ChapGiti catalog.' }
$conn = [System.Data.SqlClient.SqlConnection]::new($cs.ConnectionString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'acceptance-2025.sql'))
    $null = $cmd.Parameters.Add('@expectedServer',[Data.SqlDbType]::NVarChar,128)
    $cmd.Parameters['@expectedServer'].Value=$ExpectedServer
    $null = $cmd.Parameters.Add('@after',[Data.SqlDbType]::Bit)
    $cmd.Parameters['@after'].Value=($Phase -eq 'After')
    $data=[Data.DataSet]::new()
    $adapter=[Data.SqlClient.SqlDataAdapter]::new($cmd)
    $null=$adapter.Fill($data)
    $identity=$data.Tables[0].Rows[0]
    Write-Output ("Server {0} | database {1} | engine {2} {3} | compatibility {4} | collation {5}" -f $identity.server_name,$identity.database_name,$identity.edition,$identity.version,$identity.compatibility_level,$identity.collation_name)
    $sqlRows=$data.Tables[1].Rows
    $efRows=$data.Tables[2].Rows
    $legacy=@($inventory.order)
    $ef=@($inventory.compiled.ef)
    if ($sqlRows.Count -gt $legacy.Count -or $efRows.Count -gt $ef.Count -or ($efRows.Count -gt 0 -and $sqlRows.Count -ne $legacy.Count)) { throw 'History exceeds or crosses the approved ownership boundary.' }
    for ($i=0;$i -lt $sqlRows.Count;$i++) {
        if ($sqlRows[$i].name -cne $legacy[$i].name -or $sqlRows[$i].checksum -cne $legacy[$i].checksum) { throw 'Legacy history or checksum mismatch.' }
    }
    for ($i=0;$i -lt $efRows.Count;$i++) {
        if ($efRows[$i].MigrationId -cne $ef[$i] -or [string]::IsNullOrWhiteSpace($efRows[$i].ProductVersion)) { throw 'EF history is not an ordered prefix of the release.' }
        Write-Output ("Applied {0} with EF {1}" -f $efRows[$i].MigrationId,$efRows[$i].ProductVersion)
    }
    if ($sqlRows.Count -eq $legacy.Count) {
        $validate=$conn.CreateCommand()
        $validate.CommandText=[IO.File]::ReadAllText((Join-Path $root 'migrations/ef/Legacy009Baseline.sql'))
        $null=$validate.ExecuteNonQuery()
    }
    Write-Output ('Pending SQL: '+ (($legacy | Select-Object -Skip $sqlRows.Count | ForEach-Object name) -join ', '))
    Write-Output ('Pending EF: '+ (($ef | Select-Object -Skip $efRows.Count) -join ', '))
    if ($Phase -eq 'After') {
        if ($sqlRows.Count -ne $legacy.Count -or $efRows.Count -ne $ef.Count) { throw 'Pending migrations remain.' }
        for ($i=3;$i -lt $data.Tables.Count;$i++) {
            $table=$data.Tables[$i]
            Write-Output ($table.Columns[0].ColumnName+' = '+$table.Rows[0][0])
        }
    }
} finally { $conn.Dispose() }
