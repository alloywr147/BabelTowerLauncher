$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskSrc=Join-Path $taskRoot 'src'
$taskTest=Join-Path $taskRoot 'tests'
$taskOut=Join-Path $taskRoot 'artifacts\tests'
$taskFixtures=Join-Path $taskRoot 'artifacts\temp-fixtures'
New-Item -ItemType Directory -Path $taskOut,$taskFixtures -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'tools') -Destination $taskOut -Recurse -Force
$taskCompiler=Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskRefs=@('/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Web.Extensions.dll','/r:Microsoft.VisualBasic.dll','/r:System.Management.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$taskAppSources=@('Core.cs','Discovery.cs','ArchiveDrop.cs','Bridge.cs','Diagnostics.cs','Theme.cs','UI.cs','AssemblyInfo.cs') | ForEach-Object {Join-Path $taskSrc $_}
$taskJobs=@(
 @{Name='Core';Main='Tests';Files=@('Tests.cs');Args=@((Join-Path $taskTest 'fixtures\portable.7z'))},
 @{Name='Detection';Main='DetectionTests';Files=@('DetectionTests.cs');Args=@()},
 @{Name='Drop';Main='DropTests';Files=@('DropTests.cs');Args=@()},
 @{Name='Diagnostics';Main='DiagnosticsTests';Files=@('DiagnosticsTests.cs');Args=@()},
 @{Name='UIState';Main='UIStateTests';Files=@('UIStateTests.cs');Args=@()},
 @{Name='Navigation';Main='NavigationTests';Files=@('NavigationTests.cs');Args=@()},
 @{Name='Layout';Main='LayoutTests';Files=@('LayoutTests.cs');Args=@((Join-Path $taskOut 'layout.png'))}
)
$taskPreviousTemp=$env:TEMP
$taskPreviousTmp=$env:TMP
$taskResults=@()
try{
 $env:TEMP=$taskFixtures
 $env:TMP=$taskFixtures
 foreach($taskJob in $taskJobs){
  $taskExe=Join-Path $taskOut ($taskJob.Name+'.exe')
  $taskFiles=@($taskJob.Files | ForEach-Object {Join-Path $taskTest $_})
  & $taskCompiler /nologo /target:exe /platform:x64 ('/main:'+$taskJob.Main) ('/out:'+$taskExe) @taskRefs @taskAppSources @taskFiles
  if($LASTEXITCODE -ne 0){throw ($taskJob.Name+' test compilation failed')}
  Copy-Item -LiteralPath (Join-Path $taskSrc 'app.config') -Destination ($taskExe+'.config') -Force
  $taskArgs=$taskJob.Args
  $taskOutput=@(& $taskExe @taskArgs 2>&1)
  $taskExit=$LASTEXITCODE
  $taskOutput | Set-Content -LiteralPath (Join-Path $taskOut ($taskJob.Name+'.txt')) -Encoding UTF8
  $taskOutput | Write-Output
  if($taskExit -ne 0){throw ($taskJob.Name+' tests failed')}
  $taskCount=@($taskOutput | Where-Object {$_ -match '^PASS '}).Count
  $taskResults+=[pscustomobject]@{Suite=$taskJob.Name;Passed=$taskCount;ExitCode=$taskExit}
 }
}finally{$env:TEMP=$taskPreviousTemp;$env:TMP=$taskPreviousTmp}
$taskResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOut 'results.json') -Encoding UTF8
Write-Output ('TOTAL '+(($taskResults | Measure-Object Passed -Sum).Sum)+' checks passed; no real game or bridge started')
