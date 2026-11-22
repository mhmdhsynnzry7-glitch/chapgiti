param(
    [Parameter(Mandatory)][ValidateSet('inspect','host','signal','terminate')][string]$Action,
    [string]$Session, [string]$Key, [string]$NodePath, [string]$ProcessIds,
    [int]$Target, [string]$Birth
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if ($Action -eq 'inspect') {
    $ports = @(Get-NetTCPConnection -State Listen | Where-Object {
        $_.LocalPort -in @(3000,5100) -and $_.LocalAddress -in @('127.0.0.1','0.0.0.0','::')
    } | Select-Object @{n='port';e={$_.LocalPort}}, @{n='pid';e={$_.OwningProcess}})
    $all = @{}
    Get-CimInstance Win32_Process | ForEach-Object { $all[[int]$_.ProcessId] = $_ }
    # Include an initializing host before the worker has written its first PID record.
    $hostIds = @($all.Values | Where-Object {
        $_.Name -eq 'powershell.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($PSCommandPath,[StringComparison]::OrdinalIgnoreCase) -ge 0
    } | ForEach-Object { [int]$_.ProcessId })
    $found = @{}
    $ids = @($ports | ForEach-Object { $_.pid })
    $ids += $hostIds
    if ($ProcessIds) { $ids += @($ProcessIds.Split(',') | ForEach-Object { [int]$_ }) }
    foreach ($id in $ids) {
        while ($id -gt 0 -and $all.ContainsKey([int]$id) -and !$found.ContainsKey([int]$id)) {
            $p = $all[[int]$id]
            $stamp = ''
            try { $stamp = (Get-Process -Id $id).StartTime.ToUniversalTime().ToFileTimeUtc().ToString() } catch {}
            $found[[int]$id] = @{pid=$id; parent=[int]$p.ParentProcessId; born=$stamp; exe=$p.ExecutablePath; name=$p.Name; command=$p.CommandLine}
            $id = [int]$p.ParentProcessId
        }
    }
    @{ports=$ports; processes=@($found.Values)} | ConvertTo-Json -Depth 4 -Compress
    return
}
if ($Action -eq 'terminate') {
    # Acquire a handle before the final birth-time check. Kill uses this process object.
    # This fallback is only called for positively identified orphan/legacy Development roots.
    $p = Get-Process -Id $Target -ErrorAction SilentlyContinue
    if (!$p) { return }
    try {
        $null = $p.Handle
        if ($p.StartTime.ToUniversalTime().ToFileTimeUtc().ToString() -cne $Birth) { throw 'Process identity changed; termination refused.' }
        if (!$p.HasExited -and (!$p.CloseMainWindow() -or !$p.WaitForExit(2500))) {
            if (!$p.HasExited) { $p.Kill() }
        }
        if (!$p.WaitForExit(5000)) { throw 'Verified Development process did not exit.' }
    } finally { $p.Dispose() }
    return
}
$null = [guid]::ParseExact($Session, 'D')
if ($Key -notmatch '^[0-9a-f]{32}$') { throw 'Invalid repository key.' }
$eventName = "Local\ChapGiti-dev-stop-$Key-$Session"
if ($Action -eq 'signal') {
    $signal = [Threading.EventWaitHandle]::OpenExisting($eventName)
    try { $null=$signal.Set() } finally { $signal.Dispose() }
    return
}
Add-Type -Path (Join-Path $PSScriptRoot 'WindowsDevJob.cs')
$worker = Join-Path $PSScriptRoot 'worker.mjs'
try {
    $code = [WindowsDevJob]::Run($NodePath, $worker, $Session, "Local\ChapGiti-dev-job-$Key", $eventName)
} finally {
    # The ID is validated above, so two overlapping launches cannot remove each other's record.
    $state = Join-Path $PSScriptRoot "../../.qa/dev-processes/$Session.json"
    foreach ($record in @($state, "$state.tmp")) {
        if (Test-Path -LiteralPath $record) { Remove-Item -LiteralPath $record }
    }
}
exit $code
