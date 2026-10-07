$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskFolder=Join-Path $taskRoot 'lib\WebView2'
$taskArchive=Join-Path $taskFolder 'sdk.zip'
$taskExpected='56F7F4B8BF9AEE4B8EFEFBBDD4F67D5F74EBD1B100ED0806DA71BF76AF481AA9'
New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
if(-not (Test-Path -LiteralPath $taskArchive)) {
 Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg' -OutFile $taskArchive
}
if((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskExpected){throw 'WebView2 SDK checksum mismatch'}
Expand-Archive -LiteralPath $taskArchive -DestinationPath (Join-Path $taskFolder 'package') -Force
Write-Output 'Verified WebView2 SDK 1.0.4258.31 restored from NuGet.'
