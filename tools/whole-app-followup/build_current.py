import pathlib,subprocess,os,json,time,sys
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022"); d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022"); dotnet="D:/AI AGENT/.dotnet/dotnet.exe"; sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4"
os.chdir(root)
env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','')
common=['--disable-build-servers','-m:1','-nr:false']
def run(label,args):
 start=time.time()
 with (d/(label+'.log')).open('wb') as log:
  p=subprocess.Popen([dotnet]+args,stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
  code=p.wait()
 record=dict(label=label,args=args,pid=p.pid,exitCode=code,sourceSha=sha,startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime(start)),seconds=time.time()-start)
 (d/(label+'.json')).write_text(json.dumps(record,indent=2));print(label,code,flush=True)
 if code:sys.exit(code)
run('RESTORE',['restore','RAWSelectionAssistant.sln','--configfile',str(d/'NuGet.Config')]+common)
run('RESTORE_RUNNER',['restore','tools/VerifiedTestRunner/VerifiedTestRunner.csproj','--configfile',str(d/'NuGet.Config')]+common)
props=['-p:WholeAppDiagnosticBuild=true','-p:PhotographerTrialBuild=false','-p:IncludeSourceRevisionInInformationalVersion=true','-p:SourceRevisionId='+sha]
run('BUILD_SOLUTION',['build','RAWSelectionAssistant.sln','-c','Release','--no-restore']+props+common)
run('BUILD_WPF_X64',['build','tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj','-c','Release','-p:Platform=x64','--no-restore']+props+common)
run('BUILD_RUNNER',['build','tools/VerifiedTestRunner/VerifiedTestRunner.csproj','-c','Release','--no-restore']+common)
run('PUBLISH_FULL',['publish','src/RAWSelectionAssistant/RAWSelectionAssistant.csproj','-c','Release','-r','win-x64','--self-contained','true','--no-restore']+props+common+['-o',"E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022/Guangjing-FullScope-Followup-Diagnostic-win-x64/app"])
(d/'BUILD_COMPLETE.json').write_text(json.dumps(dict(sourceSha=sha,success=True),indent=2))
