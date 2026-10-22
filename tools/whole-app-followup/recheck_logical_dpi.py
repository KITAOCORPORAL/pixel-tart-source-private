import pathlib,subprocess,os,json,time,sys,shutil
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");dotnet="D:/AI AGENT/.dotnet/dotnet.exe";sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
os.chdir(root);env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','')
def run(label,exe,args,required=True):
 start=time.time()
 with (d/(label+'.log')).open('wb') as log:
  p=subprocess.Popen([exe]+args,stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait()
 (d/(label+'.json')).write_text(json.dumps(dict(sourceSha=sha,exe=exe,args=args,pid=p.pid,exitCode=code,startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime(start)),seconds=time.time()-start),indent=2));print(label,code,flush=True)
 if required and code:sys.exit(code)
common=['--disable-build-servers','-m:1','-nr:false'];props=['-p:WholeAppDiagnosticBuild=true','-p:PhotographerTrialBuild=false','-p:SourceRevisionId='+sha]
review=d/'logical-visual-app'
visual=d/'current-run-visual-recheck'
run('RECHECK_PRODUCE_LOGICAL_VISUAL','pwsh',['-NoProfile','-File',str(root/'tools/RC12ProductVisualHarness/Invoke-RC12ProductVisualHarness.ps1'),'-SkipBuild','-ExecutablePath',str(review/'KitaoPhotoSelector.UiReview.exe'),'-OutputRoot',str(visual)])
run('RECHECK_VALIDATE_LOGICAL_VISUAL','pwsh',['-NoProfile','-File',str(root/'tools/RC12ProductVisualHarness/Test-RC12ProductVisualEvidence.ps1'),'-EvidenceRoot',str(visual)])
gate=root/'artifacts/current-run-visual'
if gate.exists():raise FileExistsError('Refuse existing gate evidence')
gate.mkdir(parents=True);shutil.copy2(visual/'current-run-visual-evidence.json',gate/'current-run-visual-evidence.json');shutil.copytree(visual/'dpi-current',gate/'dpi-current')
run('BUILD_DPI',dotnet,['build','tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj','-c','Release','--no-restore']+common)
run('FULL_DPI',sys.executable,[str(root/'tools/whole-app-validation/verified_full_runner.py'),'--assembly',str(root/'tests/RAWSelectionAssistant.DpiTests/bin/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.DpiTests.dll'),'--output',str(d/'FULL_DPI'),'--dotnet',dotnet,'--resource-root',str(root)],False)
(d/'VISUAL_COMPLETE.json').write_text(json.dumps(dict(sourceSha=sha,complete=True,scope='OFFSCREEN LOGICAL ONLY; separate UiReview build, not physical desktop'),indent=2))
