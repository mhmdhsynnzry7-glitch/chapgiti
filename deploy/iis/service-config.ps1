function New-GitiServiceConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$AppRoot,
        [Parameter(Mandatory=$true)][string]$DataDirectory,
        [Parameter(Mandatory=$true)][string]$NodePath,
        [ValidatePattern('^[a-zA-Z0-9]+$')][string]$ServiceName = 'GitiNode'
    )
    foreach ($value in @($AppRoot,$DataDirectory,$NodePath)) {
        if (-not [IO.Path]::IsPathRooted($value)) { throw 'Service paths must be absolute.' }
    }
    function Escape-ServiceXml([string]$Value) { [Security.SecurityElement]::Escape($Value) }
    @"
<service>
  <id>$ServiceName</id>
  <name>$ServiceName</name>
  <description>Chap Giti React frontend and ASP.NET Core / SQL Server backend</description>
  <executable>$(Escape-ServiceXml $NodePath)</executable>
  <arguments>"$(Escape-ServiceXml (Join-Path $AppRoot 'scripts\start-node.mjs'))"</arguments>
  <workingdirectory>$(Escape-ServiceXml $AppRoot)</workingdirectory>
  <env name="NODE_ENV" value="production" />
  <env name="DATA_DIR" value="$(Escape-ServiceXml $DataDirectory)" />
  <env name="TRUST_IIS_PROXY" value="true" />
  <startmode>Automatic</startmode>
  <delayedAutoStart>true</delayedAutoStart>
  <stoptimeout>30 sec</stoptimeout>
  <onfailure action="restart" delay="10 sec" />
  <logpath>$(Escape-ServiceXml (Join-Path $AppRoot 'logs'))</logpath>
  <log mode="roll-by-size"><sizeThreshold>10240</sizeThreshold><keepFiles>5</keepFiles></log>
  <serviceaccount><domain>NT AUTHORITY</domain><user>LocalService</user></serviceaccount>
</service>
"@
}
