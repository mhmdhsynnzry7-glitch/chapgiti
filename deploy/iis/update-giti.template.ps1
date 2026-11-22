#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$AppRoot = 'C:\Giti\app',
    [ValidatePattern('^[a-zA-Z0-9]+$')][string]$ServiceName = 'GitiNode',
    [string]$SiteName = 'Giti',
    [string]$WebRoot = 'C:\inetpub\giti-proxy',
    [Parameter(Mandatory=$true)][string]$AdminEmail,
    [ValidatePattern('^[a-z][a-z0-9_.-]{2,31}$')][string]$Username = 'admin'
)
$ErrorActionPreference = 'Stop'
$resolvedApp = (Resolve-Path -LiteralPath $AppRoot).Path.TrimEnd('\')
if ($resolvedApp -eq [IO.Path]::GetPathRoot($resolvedApp).TrimEnd('\')) { throw 'AppRoot must be the project folder.' }
$serviceFile = Join-Path $resolvedApp ('deploy\iis\' + $ServiceName + '.xml')
foreach ($name in @('package.json','.env.local','node_modules\next\dist\bin\next')) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedApp $name) -PathType Leaf)) { throw ('Required file missing: ' + $name) }
}
$node = (Get-Command node.exe).Source
$null = Get-Service -Name $ServiceName
[xml]$serviceConfig = [IO.File]::ReadAllText($serviceFile)
if ($serviceConfig.service.id -ne $ServiceName -or
    [IO.Path]::GetFullPath([string]$serviceConfig.service.workingdirectory).TrimEnd('\') -ne $resolvedApp) {
    throw 'The service does not belong to this project folder.'
}
$dataEntry = @($serviceConfig.service.env | Where-Object { $_.name -eq 'DATA_DIR' })
if ($dataEntry.Count -ne 1 -or -not [IO.Path]::IsPathRooted([string]$dataEntry[0].value)) { throw 'Service DATA_DIR must be an absolute path.' }
$dataDir = (Resolve-Path -LiteralPath ([string]$dataEntry[0].value)).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $dataDir 'site.sqlite') -PathType Leaf)) { throw 'The existing service database was not found.' }
Import-Module WebAdministration
$site = Get-Website | Where-Object { $_.Name -eq $SiteName }
if ($site -and [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables([string]$site.physicalPath)).TrimEnd('\') -ne [IO.Path]::GetFullPath($WebRoot).TrimEnd('\')) {
    throw 'The named IIS site points to an unexpected folder.'
}
$siteWasStarted = $site -and ([string]$site.State -eq 'Started')
$payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__PAYLOAD__')) | ConvertFrom-Json
$files = @($payload.PSObject.Properties.Name)
foreach ($relative in $files) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Invalid relative update path.' }
    $target = [IO.Path]::GetFullPath((Join-Path $resolvedApp $relative))
    if (-not $target.StartsWith($resolvedApp + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid update path.' }
}
$nextDir = [IO.Path]::GetFullPath((Join-Path $resolvedApp '.next'))
if (Test-Path -LiteralPath $nextDir) {
    if ((Get-Item -LiteralPath $nextDir).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The build directory must not be a junction or link.' }
}
$backupBase = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $resolvedApp) 'backups'))
$backup = [IO.Path]::GetFullPath((Join-Path $backupBase ('giti-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
if (-not $backup.StartsWith($backupBase + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $backup.StartsWith($resolvedApp + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $backupBase -eq $dataDir -or $backupBase.StartsWith($dataDir + '\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Backup must be outside the application and data directories.'
}
[IO.Directory]::CreateDirectory($backup) | Out-Null
& icacls.exe $backup /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the backup directory.' }
$oldBuild = [IO.Path]::GetFullPath((Join-Path $backup 'previous-build'))
$failedBuild = [IO.Path]::GetFullPath((Join-Path $backup 'failed-build'))
foreach ($path in @($oldBuild,$failedBuild)) {
    if (-not $path.StartsWith($backup + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid backup build path.' }
}
$securePassword = Read-Host 'New password for admin (12-128 characters)' -AsSecureString
$secureConfirm = Read-Host 'Confirm password' -AsSecureString
$password = ([Net.NetworkCredential]::new('', $securePassword)).Password
$confirmation = ([Net.NetworkCredential]::new('', $secureConfirm)).Password
if ($password -ne $confirmation -or $password.Length -lt 12 -or $password.Length -gt 128 -or $password -match '[\r\n]') {
    $password = $null
    $confirmation = $null
    throw 'Passwords must match and contain 12-128 characters.'
}
$confirmation = $null
$previousEnv = @{}
foreach ($key in @('NODE_ENV','DATA_DIR','PORT','SITE_URL')) { $previousEnv[$key] = [Environment]::GetEnvironmentVariable($key,'Process') }
$env:NODE_ENV = 'production'
$env:DATA_DIR = $dataDir
foreach ($key in @('PORT','SITE_URL')) {
    $entry = @($serviceConfig.service.env | Where-Object { $_.name -eq $key })
    [Environment]::SetEnvironmentVariable($key, $(if ($entry.Count) { [string]$entry[-1].value } else { $null }), 'Process')
}
$sourceBackup = Join-Path $backup 'source'
$databaseBackup = Join-Path $backup 'database'
[IO.Directory]::CreateDirectory($databaseBackup) | Out-Null
$existingFiles = @{}
$databaseCopied = $false
$buildMoved = $false
$payloadStarted = $false
$locationPushed = $false
try {
    if ($siteWasStarted) { Stop-Website -Name $SiteName }
    Stop-Service -Name $ServiceName
    (Get-Service -Name $ServiceName).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(40))
    foreach ($name in @('site.sqlite','site.sqlite-wal','site.sqlite-shm')) {
        $source = Join-Path $dataDir $name
        if (Test-Path -LiteralPath $source) { [IO.File]::Copy($source,(Join-Path $databaseBackup $name)) }
    }
    $databaseCopied = $true
    foreach ($relative in $files) {
        $target = Join-Path $resolvedApp $relative
        if (Test-Path -LiteralPath $target) {
            $saved = Join-Path $sourceBackup $relative
            [IO.Directory]::CreateDirectory((Split-Path -Parent $saved)) | Out-Null
            [IO.File]::Copy($target,$saved)
            $existingFiles[$relative] = $true
        }
    }
    if (Test-Path -LiteralPath $nextDir) {
        [IO.Directory]::Move($nextDir,$oldBuild)
        $buildMoved = $true
    }
    $payloadStarted = $true
    foreach ($relative in $files) {
        $target = Join-Path $resolvedApp $relative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
        [IO.File]::WriteAllBytes($target,[Convert]::FromBase64String([string]$payload.$relative))
    }
    Push-Location -LiteralPath $resolvedApp
    $locationPushed = $true
    Write-Host 'Building Chap Giti. Please keep this window open.'
    & $node 'node_modules\next\dist\bin\next' build --webpack
    if ($LASTEXITCODE -ne 0) { throw 'Production build failed.' }
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    $password | & $node 'scripts\admin-setup.mjs' $AdminEmail --username $Username --password-stdin
    if ($LASTEXITCODE -ne 0) { throw 'Admin update failed.' }
    $password = $null
    $cache = Join-Path $nextDir 'cache'
    [IO.Directory]::CreateDirectory($cache) | Out-Null
    & icacls.exe $cache /grant '*S-1-5-19:(OI)(CI)M' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the service cache permissions.' }
    $contextFile = Join-Path $backup 'deployment-context.json'
    & $node 'scripts\deployment-context.mjs' $contextFile
    if ($LASTEXITCODE -ne 0) { throw 'Could not read deployment context.' }
    $context = [IO.File]::ReadAllText($contextFile) | ConvertFrom-Json
    Start-Service -Name $ServiceName
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri ('http://127.0.0.1:' + $context.port + '/admin') -TimeoutSec 2
            if ($response.StatusCode -eq 200 -and $response.Content -match 'name="username"') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'The updated login page did not become ready.' }
    if ($siteWasStarted) { Start-Website -Name $SiteName }
    Write-Host ('Updated. Admin: ' + $context.siteUrl + '/admin')
    Write-Host ('Username: ' + $Username)
    Write-Host ('Backup: ' + $backup)
} catch {
    $failure = $_.Exception.Message
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    (Get-Service -Name $ServiceName).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(40))
    if ($payloadStarted) {
        foreach ($relative in $files) {
            $target = Join-Path $resolvedApp $relative
            if ($existingFiles.ContainsKey($relative)) {
                [IO.File]::Copy((Join-Path $sourceBackup $relative),$target,$true)
            } elseif (Test-Path -LiteralPath $target) {
                $saved = Join-Path (Join-Path $backup 'failed-source') $relative
                [IO.Directory]::CreateDirectory((Split-Path -Parent $saved)) | Out-Null
                [IO.File]::Move($target,$saved)
            }
        }
    }
    if ($payloadStarted -and (Test-Path -LiteralPath $nextDir)) { [IO.Directory]::Move($nextDir,$failedBuild) }
    if ($buildMoved) { [IO.Directory]::Move($oldBuild,$nextDir) }
    if ($databaseCopied) {
        foreach ($name in @('site.sqlite','site.sqlite-wal','site.sqlite-shm')) {
            $target = Join-Path $dataDir $name
            if (Test-Path -LiteralPath $target) { [IO.File]::Move($target,(Join-Path $backup ('failed-' + $name))) }
            $saved = Join-Path $databaseBackup $name
            if (Test-Path -LiteralPath $saved) { [IO.File]::Copy($saved,$target) }
        }
    }
    Start-Service -Name $ServiceName
    if ($siteWasStarted) { Start-Website -Name $SiteName }
    throw ($failure + ' Previous files were restored. Backup: ' + $backup)
} finally {
    $password = $null
    $securePassword.Dispose()
    $secureConfirm.Dispose()
    if ($locationPushed) { Pop-Location }
    foreach ($key in $previousEnv.Keys) { [Environment]::SetEnvironmentVariable($key,$previousEnv[$key],'Process') }
}
