[CmdletBinding()]
param(
    [string]$AppRoot = (Join-Path $PSScriptRoot '..\..'),
    [ValidateRange(4,10)][int]$CodeLength = 4,
    [ValidatePattern('^[a-zA-Z0-9]+$')][string]$ServiceName = 'GitiNode'
)
$ErrorActionPreference = 'Stop'
$resolvedApp = (Resolve-Path -LiteralPath $AppRoot).Path
$environmentPath = Join-Path $resolvedApp '.env.local'
if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw 'Create .env.local from .env.example and configure the existing SQL Server connection first.'
}
$secret = Read-Host 'MelliPayamak Console API key (hidden)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer).Trim()
    if ($apiKey -notmatch '^[a-zA-Z0-9_-]{16,256}$') {
        throw 'Enter only the console API key, not the endpoint URL or the old SMS password.'
    }
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls.exe $environmentPath /inheritance:r /grant:r "*$($currentIdentity):(F)" '*S-1-5-18:(F)' '*S-1-5-32-544:(F)' '*S-1-5-19:(R)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not protect .env.local permissions. Run this setup in an administrator terminal.' }
    $lines = @([IO.File]::ReadAllLines($environmentPath) | Where-Object {
        $_ -notmatch '^\s*(OTP_ENABLED|OTP_CODE_LENGTH|MELIPAYAMAK_CONSOLE_API_KEY)\s*='
    })
    $lines += @('OTP_ENABLED=true', "OTP_CODE_LENGTH=$CodeLength", "MELIPAYAMAK_CONSOLE_API_KEY=$apiKey")
    [IO.File]::WriteAllLines($environmentPath, [string[]]$lines, [Text.UTF8Encoding]::new($false))
    Write-Host 'OTP configuration saved securely. No SMS was sent.'
    Write-Host ("Review and apply approved pending migrations separately, then restart " + $ServiceName + ".")
} finally {
    $apiKey = $null
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $secret.Dispose()
}
