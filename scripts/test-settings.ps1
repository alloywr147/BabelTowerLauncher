param([string]$RuntimeDirectory='', [string]$ArtifactDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($RuntimeDirectory)){$RuntimeDirectory=Join-Path $taskRoot 'dist\BabelTowerLauncher-1.5'}
if(-not (Test-Path -LiteralPath (Join-Path $RuntimeDirectory 'assets\web\index.html'))){throw 'Build application before native UI tests'}
$taskCompiler=Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskRefs=@('/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Web.Extensions.dll','/r:Microsoft.VisualBasic.dll','/r:System.Management.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$taskRefs+=@(('/r:'+(Join-Path $taskRoot 'lib\WebView2\package\lib\net462\Microsoft.Web.WebView2.Core.dll')),('/r:'+(Join-Path $taskRoot 'lib\WebView2\package\lib\net462\Microsoft.Web.WebView2.WinForms.dll')))
$taskFiles=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | Where-Object Name -ne 'UpdaterHelper.cs' | ForEach-Object FullName
$taskExe=Join-Path $RuntimeDirectory 'SettingsTransitionTests.exe'
& $taskCompiler /nologo /target:exe /platform:x64 /main:SettingsTransitionTests ('/win32manifest:'+(Join-Path $taskRoot 'src\app.manifest')) ('/out:'+$taskExe) @taskRefs @taskFiles (Join-Path $taskRoot 'tests\SettingsTransitionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Native integration test compilation failed'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\app.config') -Destination ($taskExe+'.config') -Force
if([string]::IsNullOrWhiteSpace($ArtifactDirectory)){$ArtifactDirectory=Join-Path $taskRoot 'artifacts\settings-flash'}
& $taskExe $ArtifactDirectory
if($LASTEXITCODE -ne 0){throw 'Native integration tests failed'}
