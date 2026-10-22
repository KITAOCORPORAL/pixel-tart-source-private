import pathlib,subprocess,os,json,time,sys,shutil,hashlib
root=pathlib.Path("E:/Kitao-Worktrees/guangjing-whole-app-followup-20261022");d=pathlib.Path("E:/Kitao-Deliveries/guangjing-whole-app-followup-20261022");dotnet="D:/AI AGENT/.dotnet/dotnet.exe";sha="13ec9ca1c678622e5c7b884d8f69882c409c33a4";os.chdir(root);env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','');env['KITAO_WHOLE_APP_SOURCE_SHA']=sha;env['KITAO_TEST_RESOURCE_ROOT']=str(root)
for label,project,assembly in [('DOCUMENTS','tests/PixelTart.DocumentPrototype.Tests/PixelTart.DocumentPrototype.Tests.csproj','tests/PixelTart.DocumentPrototype.Tests/bin/Release/net10.0/PixelTart.DocumentPrototype.Tests.dll'),('RESEARCH','tools/InstantFilmCandidate.Tests/InstantFilmCandidate.Tests.csproj','tools/InstantFilmCandidate.Tests/bin/Release/net10.0/InstantFilmCandidate.Tests.dll')]:
 for phase,args in [('RESTORE',['restore',project,'--configfile',str(d/'NuGet.Config')]),('BUILD',['build',project,'-c','Release','--no-restore']),('TEST',[str(root/'tools/whole-app-validation/verified_full_runner.py'),'--assembly',str(root/assembly),'--output',str(d/('FULL_'+label)),'--dotnet',dotnet,'--resource-root',str(root)])]:
  exe=sys.executable if phase=='TEST' else dotnet
  if phase!='TEST':args+=['--disable-build-servers','-m:1','-nr:false']
  with (d/(phase+'_'+label+'.log')).open('wb') as log:
   start=time.time();p=subprocess.Popen([exe]+args,stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait()
  (d/(phase+'_'+label+'.json')).write_text(json.dumps(dict(sourceSha=sha,exe=exe,args=args,pid=p.pid,exit=code,seconds=time.time()-start,scope='Independent document prototype/research component, not product device acceptance'),indent=2));print(phase,label,code,flush=True)
  if code and phase!='TEST':sys.exit(code)
(d/'EXTRA_COMPLETE.json').write_text(json.dumps(dict(sourceSha=sha,complete=True),indent=2))
