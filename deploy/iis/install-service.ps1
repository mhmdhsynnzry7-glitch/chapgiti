#Requires -RunAsAdministrator
param(
  [string]$AppRoot = (Join-Path $PSScriptRoot '..\..'),
  [string]$DataDirectory = 'C:\Giti\data',
  [ValidatePattern('^[a-zA-Z0-9]+$')][string]$ServiceName = 'GitiNode'
)
$ErrorActionPreference = 'Stop'
$resolvedApp = (Resolve-Path -LiteralPath $AppRoot).Path
if (-not [IO.Path]::IsPathRooted($DataDirectory)) { throw 'DataDirectory must be absolute.' }
$resolvedData = [IO.Path]::GetFullPath($DataDirectory).TrimEnd('\')
$appNormalized = $resolvedApp.TrimEnd('\')
if ($resolvedData -eq [IO.Path]::GetPathRoot($resolvedData).TrimEnd('\') -or
    $appNormalized -eq $resolvedData -or
    $appNormalized.StartsWith($resolvedData + '\', [StringComparison]::OrdinalIgnoreCase)) {
  throw 'Use a dedicated data subdirectory, not a drive root, application folder or its parent.'
}
$envFile = Join-Path $resolvedApp '.env.local'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'Create .env.local and set SITE_URL before installing.' }
if (-not (Test-Path -LiteralPath (Join-Path $resolvedApp 'frontend\.next\BUILD_ID')) -and -not (Test-Path -LiteralPath (Join-Path $resolvedApp '.next\BUILD_ID'))) { throw 'Build the project or use the prepared .NET release package first.' }
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { throw 'Service already exists. Use Restart-Service after updates.' }
$serviceDir = Join-Path $resolvedApp 'deploy\iis'
$wrapper = Join-Path $serviceDir ($ServiceName + '.exe')
if (-not (Test-Path -LiteralPath $wrapper)) {
  throw ("Download WinSW-x64.exe v2.12.0 from the official release and save as " + $wrapper)
}
$node = (Get-Command node.exe).Source
Push-Location -LiteralPath $resolvedApp
try {
  & $node (Join-Path $resolvedApp 'scripts\check-deploy.mjs') $resolvedData
  if ($LASTEXITCODE -ne 0) { throw 'Production configuration check failed.' }
} finally { Pop-Location }
$logs = Join-Path $resolvedApp 'logs'
$frontendRoot = @('frontend','frontend\publish','frontend\.next\standalone') | ForEach-Object { Join-Path $resolvedApp $_ } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'server.js') } | Select-Object -First 1
if (-not $frontendRoot) { throw 'Published frontend server.js is missing.' }
$cache = Join-Path $frontendRoot '.next\cache'
foreach ($folder in @($resolvedData,$logs,$cache)) { [IO.Directory]::CreateDirectory($folder) | Out-Null }
& icacls.exe $resolvedApp /grant '*S-1-5-19:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to grant application read access.' }
foreach ($folder in @($resolvedData,$logs,$cache)) {
  & icacls.exe $folder /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M' | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Failed to set runtime directory permissions.' }
}
& icacls.exe $envFile /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' '*S-1-5-19:R' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to protect environment file.' }
. (Join-Path $PSScriptRoot 'service-config.ps1')
$xml = New-GitiServiceConfiguration -AppRoot $resolvedApp -DataDirectory $resolvedData -NodePath $node -ServiceName $ServiceName
$config = Join-Path $serviceDir ($ServiceName + '.xml')
[IO.File]::WriteAllText($config,$xml,[Text.UTF8Encoding]::new($false))
& $wrapper install
if ($LASTEXITCODE -ne 0) { throw 'Service installation failed.' }
& $wrapper start
if ($LASTEXITCODE -ne 0) { throw 'Service start failed. Check logs.' }
Write-Host 'Service installed. Check logs and http://127.0.0.1:3000 before configuring IIS.'
