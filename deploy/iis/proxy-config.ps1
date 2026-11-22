function New-GitiProxyConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$TemplatePath,
        [ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Domain = 'chapgiti.ir',
        [ValidateRange(1024,65535)][int]$Port = 3000,
        [ValidateRange(1024,65535)][int]$BackendPort = 5100
    )
    if ($Port -eq $BackendPort) { throw 'Frontend and backend ports must be distinct.' }
    $template = [IO.File]::ReadAllText($TemplatePath)
    $template.Replace('__PUBLIC_HOST__',$Domain).Replace('__HOST_REGEX__',[regex]::Escape($Domain)).Replace('__PORT__',[string]$Port).Replace('__BACKEND_PORT__',[string]$BackendPort)
}
