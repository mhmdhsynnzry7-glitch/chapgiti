# Explicitly disposable; this script accepts no target or connection string.
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/Select-IsolatedDevelopment.ps1" -Create
$ownedId = $env:CHAP_DEV_SANDBOX_ID
try {
    & SqlLocalDB.exe stop "ChapGitiDev_$ownedId"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot test the disposable instance restart.' }
    & "$PSScriptRoot/Select-IsolatedDevelopment.ps1" -Id $ownedId
    Write-Host 'PASS same disposable catalog reselected after LocalDB restart'
    node "$PSScriptRoot/startup-smoke.mjs"
    if ($LASTEXITCODE -ne 0) { throw "Development startup acceptance failed ($LASTEXITCODE)." }
} finally {
    & "$PSScriptRoot/Select-IsolatedDevelopment.ps1" -Id $ownedId -Remove
}
