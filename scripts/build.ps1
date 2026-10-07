param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskSrc=Join-Path $taskRoot 'src'
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $taskRoot 'dist\BabelTowerLauncher-1.5'}
$taskCompiler=Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $taskCompiler)){throw 'Windows .NET Framework x64 C# compiler not found'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskRefs=@('/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Web.Extensions.dll','/r:Microsoft.VisualBasic.dll','/r:System.Management.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$taskRefs+=@(('/r:'+(Join-Path $taskRoot 'lib\WebView2\package\lib\net462\Microsoft.Web.WebView2.Core.dll')),('/r:'+(Join-Path $taskRoot 'lib\WebView2\package\lib\net462\Microsoft.Web.WebView2.WinForms.dll')))
$taskFiles=@('RhineRuntime.cs','RhineShell.cs','RepairService.cs','ReportExport.cs','Core.cs','DownloadCache.cs','OfficialUpdates.cs','PackageInstaller.cs','SelfUpdater.cs','SelfUpdateFiles.cs','Discovery.cs','ArchiveDrop.cs','Bridge.cs','Diagnostics.cs','Theme.cs','ThemePreferences.cs','UI.cs','SoftwareUpdateUI.cs','StartupUpdates.cs','StartupUpdateDialog.cs','StartupUpdateUI.cs','AssemblyInfo.cs') | ForEach-Object {Join-Path $taskSrc $_}
& $taskCompiler /nologo /target:winexe /platform:x64 ('/win32icon:'+(Join-Path $taskRoot 'assets\app.ico')) ('/win32manifest:'+(Join-Path $taskSrc 'app.manifest')) ('/out:'+(Join-Path $OutputDirectory '巴别塔启动器.exe')) @taskRefs @taskFiles
if($LASTEXITCODE -ne 0){throw 'Application compilation failed'}
Copy-Item -LiteralPath (Join-Path $taskSrc 'app.config') -Destination (Join-Path $OutputDirectory '巴别塔启动器.exe.config') -Force
foreach($taskFolder in @('assets','tools')){
 $taskDestination=Join-Path $OutputDirectory $taskFolder
 New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
 Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskFolder) -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $taskDestination -Force}
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $OutputDirectory -Force
& $taskCompiler /nologo /target:winexe /platform:x64 ('/win32manifest:'+(Join-Path $taskSrc 'app.manifest')) ('/out:'+(Join-Path $OutputDirectory 'tools\BabelTowerUpdater.exe')) /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll (Join-Path $taskSrc 'SelfUpdateFiles.cs') (Join-Path $taskSrc 'UpdaterHelper.cs')
if($LASTEXITCODE -ne 0){throw 'Updater compilation failed'}
Copy-Item -LiteralPath (Join-Path $taskSrc 'app.config') -Destination (Join-Path $OutputDirectory 'tools\BabelTowerUpdater.exe.config') -Force
$taskWeb=Join-Path $taskRoot 'web\dist'
if(-not (Test-Path -LiteralPath (Join-Path $taskWeb 'index.html'))){throw 'Build web UI first: npm run build in web/'}
$taskWebTarget=Join-Path $OutputDirectory 'assets\web'
New-Item -ItemType Directory -Path $taskWebTarget -Force | Out-Null
Get-ChildItem -LiteralPath $taskWeb | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $taskWebTarget -Recurse -Force}
$taskRuntime=Join-Path $OutputDirectory 'assets\lib'
New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
foreach($taskDll in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $taskRoot ('lib\WebView2\package\lib\net462\'+$taskDll)) -Destination $taskRuntime -Force}
Copy-Item -LiteralPath (Join-Path $taskRoot 'lib\WebView2\package\runtimes\win-x64\native\WebView2Loader.dll') -Destination $taskRuntime -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'lib\WebView2\package\LICENSE.txt') -Destination (Join-Path $taskRuntime 'WebView2-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'lib\WebView2\package\NOTICE.txt') -Destination (Join-Path $taskRuntime 'WebView2-NOTICE.txt') -Force
foreach($taskDocument in @('README.md','使用说明.md')){Copy-Item -LiteralPath (Join-Path $taskRoot $taskDocument) -Destination $OutputDirectory -Force}
$taskDocs=Join-Path $OutputDirectory 'docs'
New-Item -ItemType Directory -Path $taskDocs -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskRoot 'docs') | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $taskDocs -Recurse -Force}
Write-Output ('Built Windows application: '+(Join-Path $OutputDirectory '巴别塔启动器.exe'))



