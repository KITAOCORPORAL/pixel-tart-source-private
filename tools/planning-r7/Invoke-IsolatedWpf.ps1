param([string]$BinaryDirectory,[string]$SourceDirectory,[string]$OutputRoot,[int]$TimeoutSeconds=180,[string]$ClassPattern="*")
$ErrorActionPreference='Stop'
$dotnet='D:/AI AGENT/.dotnet/dotnet.exe'
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
$env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $OutputRoot 'runtime'
$classes=Get-ChildItem $SourceDirectory -Filter '*.cs' | ForEach-Object { $s=Get-Content $_.FullName -Raw; if($s -match '\[TestClass\]'){[regex]::Matches($s,'\[TestClass(?:Attribute)?\]\s*(?:\[[^\]]+\]\s*)*(?:public|internal)\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(\w+)') | ForEach-Object {$_.Groups[1].Value}}} | Sort-Object -Unique | Where-Object {$_ -like $ClassPattern}
$results=@()
foreach($c in $classes){
 $log=Join-Path $OutputRoot ($c+'.log');$err=Join-Path $OutputRoot ($c+'.err');$trx=Join-Path $OutputRoot ($c+'.trx')
 $args=@('vstest',('"'+(Join-Path $BinaryDirectory 'RAWSelectionAssistant.WpfTests.dll')+'"'),('/TestCaseFilter:FullyQualifiedName~RAWSelectionAssistant.WpfTests.'+$c),('"/ResultsDirectory:'+$OutputRoot+'"'),('/Logger:trx;LogFileName='+$c+'.trx'))
 $proc=Start-Process $dotnet -ArgumentList $args -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError $err
 $timeout=if($c -in @("AssetLibraryP1AutomatedEvidenceContractTests","AssetLibraryP1GateAEvidenceContractTests")){600}else{$TimeoutSeconds};$done=$proc.WaitForExit($timeout*1000);if(!$done){$proc.Kill($true);$proc.WaitForExit()}
 $results+=@{class=$c;finished=$done;exitCode=$proc.ExitCode;trx=(Test-Path $trx)}
 $results | ConvertTo-Json | Set-Content (Join-Path $OutputRoot 'progress.json')
}
