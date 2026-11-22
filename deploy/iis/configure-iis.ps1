#Requires -RunAsAdministrator
[CmdletBinding()]
param(
  [Alias('PublicHost')][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Domain = 'chapgiti.ir',
  [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CertificateThumbprint,
  [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$SiteName = 'Giti',
  [string]$WebRoot = 'C:\inetpub\giti-proxy',
  [ValidateScript({ $_ -eq '*' -or ($_ -match '^\d{1,3}(\.\d{1,3}){3}$' -and [bool]($_ -as [Net.IPAddress])) })][string]$ListenAddress = '*',
  [ValidateRange(1024,65535)][int]$Port = 3000,
  [ValidateRange(1024,65535)][int]$BackendPort = 5100
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'proxy-config.ps1')
$template = New-GitiProxyConfiguration -TemplatePath (Join-Path $PSScriptRoot 'web.config') -Domain $Domain -Port $Port -BackendPort $BackendPort
Import-Module WebAdministration
$parsedIp = $null
$isIp = [Net.IPAddress]::TryParse($Domain, [ref]$parsedIp)
if ($isIp -and ($parsedIp.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $parsedIp.ToString() -ne $Domain)) {
  throw 'Use a full IPv4 address such as 203.0.113.10, or a DNS hostname.'
}
$cert = Get-Item -LiteralPath ("Cert:\LocalMachine\My\" + $CertificateThumbprint)
if (-not $cert.HasPrivateKey -or $cert.NotAfter -lt (Get-Date) -or $cert.NotBefore -gt (Get-Date)) {
  throw 'Certificate is missing its private key, expired, or not yet valid.'
}
if (Test-Path ("IIS:\Sites\" + $SiteName)) { throw 'Site already exists. Update it in IIS Manager; this script only creates new sites.' }
if (Test-Path ("IIS:\AppPools\" + $SiteName)) { throw 'Application pool already exists. Choose a new SiteName.' }
$resolvedRoot = [IO.Path]::GetFullPath($WebRoot)
if (Test-Path -LiteralPath $resolvedRoot) {
  if (@(Get-ChildItem -LiteralPath $resolvedRoot -Force).Count -gt 0) { throw 'WebRoot must be empty. Keep the application and data outside this IIS folder.' }
}
# IP URLs cannot use SNI. Never replace another site's IP:443 certificate.
if ($isIp) {
  foreach ($existingBinding in @(Get-WebBinding -Protocol https)) {
    $info = [regex]::Match([string]$existingBinding.bindingInformation, '^(.*):(\d+):(.*)$')
    if (-not $info.Success -or $info.Groups[2].Value -ne '443' -or (([int]$existingBinding.sslFlags -band 1) -ne 0)) { continue }
    $boundAddress = $info.Groups[1].Value
    if ($ListenAddress -eq '*' -or $boundAddress -in @('*','0.0.0.0','[::]',$ListenAddress)) {
      throw ("An existing non-SNI HTTPS binding uses port 443: " + $existingBinding.bindingInformation + ". Review it in IIS Manager or choose a separate local ListenAddress.")
    }
  }
}
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
# These ARR proxy settings apply at server level. Review other proxy sites first.
& $appcmd set config -section:system.webServer/proxy /enabled:true /preserveHostHeader:true /reverseRewriteHostInResponseHeaders:false /commit:apphost
if ($LASTEXITCODE -ne 0) { throw 'Install IIS URL Rewrite and ARR, then enable Proxy.' }
foreach ($variable in @('HTTP_X_NAGHSH_CLIENT_IP','HTTP_X_FORWARDED_PROTO','HTTP_X_FORWARDED_HOST')) {
  $existing = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter ("system.webServer/rewrite/allowedServerVariables/add[@name='" + $variable + "']")
  if (-not $existing) {
    Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/rewrite/allowedServerVariables' -Name '.' -Value @{name=$variable}
  }
}
[IO.Directory]::CreateDirectory($resolvedRoot) | Out-Null
[IO.File]::WriteAllText((Join-Path $resolvedRoot 'web.config'),$template,[Text.UTF8Encoding]::new($false))
& icacls.exe $resolvedRoot /grant '*S-1-5-32-568:(OI)(CI)RX' '*S-1-5-17:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to set IIS folder permissions.' }
New-WebAppPool -Name $SiteName | Out-Null
Set-ItemProperty ("IIS:\AppPools\" + $SiteName) -Name managedRuntimeVersion -Value ''
New-Website -Name $SiteName -PhysicalPath $resolvedRoot -IPAddress $ListenAddress -Port 80 -HostHeader $Domain -ApplicationPool $SiteName | Out-Null
if ($isIp) {
  New-WebBinding -Name $SiteName -Protocol https -IPAddress $ListenAddress -Port 443 -SslFlags 0
} else {
  New-WebBinding -Name $SiteName -Protocol https -IPAddress $ListenAddress -Port 443 -HostHeader $Domain -SslFlags 1
}
$binding = Get-WebBinding -Name $SiteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint, 'My')
Write-Host ("IIS site created: https://" + $Domain)
Write-Host 'Node must be running on loopback. Only ports 80 and 443 need public firewall access.'
