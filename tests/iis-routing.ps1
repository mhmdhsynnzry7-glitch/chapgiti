$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../deploy/iis/proxy-config.ps1')
$template = Join-Path $PSScriptRoot '../deploy/iis/web.config'
[xml]$xml = New-GitiProxyConfiguration -TemplatePath $template
$rules = @($xml.configuration.'system.webServer'.rewrite.rules.rule)
if ($rules.Count -ne 3 -or $rules[0].action.type -ne 'Redirect' -or $rules[0].action.url -ne 'https://chapgiti.ir/{R:1}' -or $rules[0].stopProcessing -ne 'true') { throw 'Canonical public TLS origin must precede proxy rules.' }
if ($rules[0].conditions.add[0].input -ne '{HTTPS}' -or $rules[0].conditions.add[1].input -ne '{HTTP_HOST}' -or $rules[0].conditions.add[1].negate -ne 'true') { throw 'Canonical scheme/host mismatch.' }
if ($xml.SelectSingleNode('/configuration/system.webServer/webSocket')) { throw 'The site must inherit the server WebSocket section.' }
Write-Output 'PASS site inherits the locked WebSocket section'
foreach ($rule in $rules[1..2]) {
    if ($rule.conditions -or $rule.stopProcessing -ne 'true' -or $rule.action.type -ne 'Rewrite' -or $rule.action.appendQueryString -ne 'true') { throw 'Every transport and HTTP method must preserve query and route.' }
    $headers = @($rule.serverVariables.set)
    if (($headers.name -join ',') -ne 'HTTP_X_NAGHSH_CLIENT_IP,HTTP_X_FORWARDED_PROTO,HTTP_X_FORWARDED_HOST' -or ($headers.value -join ',') -ne '{REMOTE_ADDR},https,{HTTP_HOST}') { throw 'Cookie, Authorization, Origin and Upgrade must pass through. Forwarding must derive from the trusted edge.' }
}
$paths = @('', 'admin/orders', '_next/static/app.js', 'api/orders', 'api/customer/notifications','api/realtime/customer/support-malformed','api/realtime/admin/support/other','api/realtime/other/support')
foreach ($role in 'customer','admin') {foreach ($kind in 'notifications','support') {foreach ($suffix in '', '/negotiate', '/') {
    $url = "api/realtime/$role/$kind$suffix"
    if ($url -notmatch $rules[1].match.url -or $rules[1].action.url -ne 'http://127.0.0.1:5100/{R:0}' -or $Matches[0] -ne $url) { throw ('Hub misrouted: '+$url) }
    Write-Output ('PASS '+$url)
}}}
foreach ($url in $paths) {
    if ($url -match $rules[1].match.url -or $url -notmatch $rules[2].match.url -or $rules[2].action.url -ne 'http://127.0.0.1:3000/{R:1}') { throw ('Page/API misrouted: '+$url) }
    Write-Output ('PASS Next '+$url)
}
[xml]$custom = New-GitiProxyConfiguration -TemplatePath $template -Domain 'test.invalid' -Port 4000 -BackendPort 5200
if ($custom.OuterXml.Contains('__') -or $custom.OuterXml -notmatch '127\.0\.0\.1:5200/' -or $custom.OuterXml -notmatch '127\.0\.0\.1:4000/') { throw 'Template substitutions failed.' }
Write-Output 'PASS canonical TLS, forwarding, WebSocket, query strings, custom bindings'
