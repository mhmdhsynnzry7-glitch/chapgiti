#Requires -RunAsAdministrator
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][ValidatePattern('^\d{1,3}(\.\d{1,3}){3}$')][string]$PublicIp,
  [ValidateRange(1024,65535)][int]$HttpPort = 8081,
  [ValidateRange(1024,65535)][int]$BackendPort = 3000,
  [string]$AppRoot = 'C:\Giti\app',
  [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$SiteName = 'Giti',
  [ValidatePattern('^[a-zA-Z0-9]+$')][string]$ServiceName = 'GitiNode',
  [string]$WebRoot = 'C:\inetpub\giti-proxy'
)
$ErrorActionPreference = 'Stop'
$ip = $null
if (-not [Net.IPAddress]::TryParse($PublicIp, [ref]$ip) -or $ip.ToString() -ne $PublicIp) { throw 'Enter the full IPv4 address, without http:// or a port.' }
if ($HttpPort -eq $BackendPort) { throw 'The public HTTP port and loopback Node port must differ.' }
Import-Module WebAdministration
$resolvedApp = (Resolve-Path -LiteralPath $AppRoot).Path.TrimEnd('\')
$resolvedWeb = [IO.Path]::GetFullPath($WebRoot).TrimEnd('\')
if ($resolvedWeb -eq $resolvedApp -or $resolvedApp.StartsWith($resolvedWeb + '\', [StringComparison]::OrdinalIgnoreCase) -or $resolvedWeb.StartsWith($resolvedApp + '\', [StringComparison]::OrdinalIgnoreCase)) {
  throw 'Keep the application and IIS proxy folders separate.'
}
$envFile = Join-Path $resolvedApp '.env.local'
$startFile = Join-Path $resolvedApp 'scripts\start-node.mjs'
$checkFile = Join-Path $resolvedApp 'scripts\check-deploy.mjs'
$serviceFile = Join-Path $resolvedApp ('deploy\iis\' + $ServiceName + '.xml')
$webFile = Join-Path $resolvedWeb 'web.config'
foreach ($file in @($envFile,$startFile,$checkFile,$serviceFile,(Join-Path $resolvedApp 'package.json'))) {
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Required project file missing: ' + $file) }
}
if ([IO.File]::ReadAllText($startFile) -notmatch 'dotnet-runtime' -or [IO.File]::ReadAllText($checkFile) -notmatch 'dotnet-runtime') {
  throw 'The application launcher must be the current ASP.NET Core / SQL Server version.'
}
$node = (Get-Command node.exe).Source
$service = Get-Service -Name $ServiceName
[xml]$serviceConfig = [IO.File]::ReadAllText($serviceFile)
if ($serviceConfig.service.id -ne $ServiceName -or [IO.Path]::GetFullPath([string]$serviceConfig.service.workingdirectory).TrimEnd('\') -ne $resolvedApp) {
  throw 'The Windows service configuration does not belong to this application folder.'
}
$site = Get-Website | Where-Object { $_.Name -eq $SiteName }
if ($site -and [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables([string]$site.physicalPath)).TrimEnd('\') -ne $resolvedWeb) {
  throw 'The named IIS site points to a different folder. Nothing was changed.'
}
if (-not $site -and (Test-Path -LiteralPath $resolvedWeb) -and @(Get-ChildItem -LiteralPath $resolvedWeb -Force).Count) {
  throw 'No matching IIS site was found, but WebRoot is nonempty. Inspect this folder before proceeding.'
}
if (-not $site -and (Test-Path ('IIS:\AppPools\' + $SiteName))) { throw 'An unrelated application pool already has this name.' }
$allSites = @(Get-Website)
$siteSnapshots = @($allSites | ForEach-Object {
  [pscustomobject]@{ Name=$_.Name; State=[string]$_.State; Path=$_.physicalPath; Bindings=@($_.bindings.Collection | ForEach-Object { [pscustomobject]@{ Protocol=$_.protocol; Binding=$_.bindingInformation } }) }
})
foreach ($otherSite in $allSites) {
  if ($otherSite.Name -eq $SiteName) { continue }
  foreach ($binding in $otherSite.bindings.Collection) {
    if ([string]$binding.bindingInformation -match (':' + $HttpPort + ':')) { throw ("Port " + $HttpPort + " already belongs to IIS site " + $otherSite.Name + '. Nothing was changed.') }
  }
}
$ownPortBinding = @($site.bindings.Collection | Where-Object { [string]$_.bindingInformation -match (':' + $HttpPort + ':') })
if (-not $ownPortBinding.Count -and @(Get-NetTCPConnection -State Listen -LocalPort $HttpPort -ErrorAction SilentlyContinue).Count) {
  throw ('Port ' + $HttpPort + ' already has a listener. Choose another HttpPort.')
}
# Read ARR state; never reset global proxy settings on a shared IIS server.
$proxy = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/proxy'
if (-not $proxy -or -not $proxy.enabled) { throw 'ARR proxy is not enabled. No global IIS settings were changed; inspect ARR before proceeding.' }
$null = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/rewrite/allowedServerVariables'
$siteUrl = 'http://' + $PublicIp + ':' + $HttpPort
$envUpdates = [ordered]@{ SITE_URL=$siteUrl; PORT=[string]$BackendPort; ALLOW_HTTP='true'; TRUST_IIS_PROXY='true' }
$envText = [IO.File]::ReadAllText($envFile)
foreach ($key in $envUpdates.Keys) {
  $pattern = '(?m)^[ \t]*(?:export[ \t]+)?' + [regex]::Escape($key) + '[ \t]*=.*$'
  $line = $key + '=' + $envUpdates[$key]
  if ([regex]::IsMatch($envText,$pattern)) { $envText = [regex]::Replace($envText,$pattern,$line) }
  else { $envText = $envText.TrimEnd() + [Environment]::NewLine + $line + [Environment]::NewLine }
  foreach ($entry in @($serviceConfig.service.env)) {
    if ($entry.name -eq $key) { $entry.SetAttribute('value',[string]$envUpdates[$key]) }
  }
}
$startSource = [IO.File]::ReadAllText($startFile)
$checkSource = [IO.File]::ReadAllText($checkFile)
if ($startSource -notmatch 'dotnet-runtime' -or $checkSource -notmatch 'dotnet-runtime') {
  throw 'Install the current ASP.NET Core / SQL Server release before configuring HTTP.'
}
$buildCandidates = @('frontend\.next\BUILD_ID','frontend\publish\.next\BUILD_ID','frontend\.next\standalone\.next\BUILD_ID')
if (-not @($buildCandidates | Where-Object { Test-Path -LiteralPath (Join-Path $resolvedApp $_) }).Count) {
  throw 'Published Next.js frontend is missing.'
}
$webSource = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <directoryBrowse enabled="false" />
    <httpErrors existingResponse="PassThrough" />
    <security><requestFiltering><requestLimits maxAllowedContentLength="16777216" /></requestFiltering></security>
    <httpProtocol><customHeaders>
      <remove name="X-Powered-By" />
      <remove name="Strict-Transport-Security" />
    </customHeaders></httpProtocol>
    <rewrite><rules>
      <clear />
      <rule name="Giti HTTP Node" stopProcessing="true">
        <match url="(.*)" />
        <serverVariables>
          <set name="HTTP_X_NAGHSH_CLIENT_IP" value="{REMOTE_ADDR}" />
          <set name="HTTP_X_FORWARDED_PROTO" value="http" />
          <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
        </serverVariables>
        <action type="Rewrite" url="http://127.0.0.1:$BackendPort/{R:1}" appendQueryString="true" />
      </rule>
    </rules></rewrite>
  </system.webServer>
</configuration>
"@
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backup = Join-Path $resolvedApp ('deploy\iis\backups\http-' + $stamp)
[IO.Directory]::CreateDirectory($backup) | Out-Null
& icacls.exe $backup /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the backup directory.' }
$restoreFiles = [ordered]@{ '.env.local'=$envFile; 'start-node.mjs'=$startFile; 'check-deploy.mjs'=$checkFile; 'service.xml'=$serviceFile }
if (Test-Path -LiteralPath $webFile) { $restoreFiles['web.config'] = $webFile }
foreach ($name in $restoreFiles.Keys) { [IO.File]::Copy($restoreFiles[$name],(Join-Path $backup $name),$false) }
[IO.File]::WriteAllText((Join-Path $backup 'iis-before.json'), (@{
  Sites=$siteSnapshots
  Proxy=@{ enabled=$proxy.enabled; preserveHostHeader=$proxy.preserveHostHeader; reverseRewriteHostInResponseHeaders=$proxy.reverseRewriteHostInResponseHeaders }
} | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Backup-WebConfiguration -Name ($SiteName + '-before-http-' + $stamp) | Out-Null
$runtimeChanged = $false
try {
  if ($site) { Stop-Website -Name $SiteName }
  Stop-Service -Name $ServiceName
  $runtimeChanged = $true
  [IO.File]::WriteAllText($startFile,$startSource,[Text.UTF8Encoding]::new($false))
  [IO.File]::WriteAllText($checkFile,$checkSource,[Text.UTF8Encoding]::new($false))
  [IO.File]::WriteAllText($envFile,$envText,[Text.UTF8Encoding]::new($false))
  $serviceConfig.Save($serviceFile)
  Push-Location -LiteralPath $resolvedApp
  try {
    & $node $checkFile
    if ($LASTEXITCODE -ne 0) { throw 'HTTP deployment validation failed.' }
  } finally { Pop-Location }
  [IO.Directory]::CreateDirectory($resolvedWeb) | Out-Null
  [IO.File]::WriteAllText($webFile,$webSource,[Text.UTF8Encoding]::new($false))
  & icacls.exe $resolvedWeb /grant '*S-1-5-32-568:(OI)(CI)RX' '*S-1-5-17:(OI)(CI)RX' | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Could not grant IIS read access to its proxy folder.' }
  if ($site) {
    foreach ($binding in @(Get-WebBinding -Name $SiteName)) {
      Remove-WebBinding -Name $SiteName -Protocol ([string]$binding.protocol) -BindingInformation ([string]$binding.bindingInformation)
    }
    New-WebBinding -Name $SiteName -Protocol http -IPAddress '*' -Port $HttpPort
  } else {
    New-WebAppPool -Name $SiteName | Out-Null
    Set-ItemProperty ('IIS:\AppPools\' + $SiteName) -Name managedRuntimeVersion -Value ''
    New-Website -Name $SiteName -PhysicalPath $resolvedWeb -IPAddress '*' -Port $HttpPort -ApplicationPool $SiteName | Out-Null
    Stop-Website -Name $SiteName
  }
  foreach ($variable in @('HTTP_X_NAGHSH_CLIENT_IP','HTTP_X_FORWARDED_PROTO','HTTP_X_FORWARDED_HOST')) {
    $existing = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $SiteName -Filter ("system.webServer/rewrite/allowedServerVariables/add[@name='" + $variable + "']")
    if (-not $existing) { Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $SiteName -Filter 'system.webServer/rewrite/allowedServerVariables' -Name '.' -Value @{name=$variable} }
  }
  Start-Service -Name $ServiceName
  $ready = $false
  for ($attempt=0; $attempt -lt 30; $attempt++) {
    try {
      $response = Invoke-WebRequest -UseBasicParsing -Uri ('http://127.0.0.1:' + $BackendPort + '/') -TimeoutSec 2
      if ($response.StatusCode -eq 200) { $ready=$true; break }
    } catch { Start-Sleep -Milliseconds 500 }
  }
  if (-not $ready) { throw 'Node did not become ready. Check the application logs.' }
  Start-Website -Name $SiteName
  $response = Invoke-WebRequest -UseBasicParsing -Uri ('http://127.0.0.1:' + $HttpPort + '/') -Headers @{Host=($PublicIp + ':' + $HttpPort)} -TimeoutSec 15 -MaximumRedirection 0
  if ($response.StatusCode -ne 200) { throw 'IIS did not return HTTP 200 on the new port.' }
  $ruleName = $SiteName + '-HTTP-' + $HttpPort
  if (-not (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name $ruleName -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $HttpPort | Out-Null
  }
  Write-Host ('Ready: ' + $siteUrl)
  Write-Host ('Admin: ' + $siteUrl + '/admin')
  Write-Host ('Backup: ' + $backup)
  Write-Host 'Only this site was rebound. Global ARR settings and other IIS sites were not changed.'
  Write-Host 'Allow this TCP port in the VPS provider firewall too. HTTP traffic is unencrypted.'
} catch {
  $failureMessage = $_.Exception.Message
  if (Get-Website | Where-Object { $_.Name -eq $SiteName }) { Stop-Website -Name $SiteName -ErrorAction SilentlyContinue }
  if ($runtimeChanged) {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    foreach ($name in $restoreFiles.Keys) {
      [IO.File]::WriteAllBytes($restoreFiles[$name],[IO.File]::ReadAllBytes((Join-Path $backup $name)))
    }
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
  }
  throw ($failureMessage + '. ' + $SiteName + ' IIS site remains stopped. Previous application files were restored if changed. Backup: ' + $backup)
}
