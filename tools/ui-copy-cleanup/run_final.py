import pathlib,os,subprocess,json,hashlib,shutil,re,sys,time
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");app=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/Guangjing-UI-Cleanup-Diagnostic-win-x64/app");sha="b9cb1e25e811903b117aef3afeb203aadf7d0790";dotnet="D:/AI AGENT/.dotnet/dotnet.exe";os.chdir(r);env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','');env['KITAO_OWNED_JPEG_EVIDENCE']=str(d/'synthetic-final');env['KITAO_WHOLE_APP_SOURCE_SHA']=sha;env['KITAO_CANVAS_OWNED_EVIDENCE']=str(d/'synthetic-ab-final');env['KITAO_CANVAS_QUEUE_EVIDENCE']=str(d/'canvas-queue-final');env['KITAO_COPY_ROUTE_EVIDENCE']=str(d/'routes-final/images')
bindings=[]
assemblies={'CORE':r/'tests/RAWSelectionAssistant.Tests/bin/Release/net10.0/RAWSelectionAssistant.Tests.dll','WPF':r/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll'}
for assembly in assemblies.values():
 for f in app.glob('*.dll'):
  target=assembly.parent/f.name
  if target.exists():
   shutil.copy2(f,target);h=hashlib.sha256(f.read_bytes()).hexdigest();assert hashlib.sha256(target.read_bytes()).hexdigest()==h;bindings.append(dict(published=str(f),test=str(target),sha256=h))
(d/'FINAL_BINDINGS.json').write_text(json.dumps(dict(sourceSha=sha,bindings=bindings),indent=2))
plans=[]
for label,assembly in assemblies.items():
 discovery=d/(label+'-discovery.json')
 subprocess.run([dotnet,str(r/'tools/VerifiedTestRunner/bin/Release/net10.0/VerifiedTestRunner.dll'),'D:/AI AGENT/.dotnet/sdk/10.0.302/vstest.console.dll',str(assembly),str(discovery)],check=True,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
 tests=json.loads(discovery.read_text(encoding='utf-8-sig'));classes=sorted({x['fullyQualifiedName'].rsplit('.',1)[0] for x in tests if re.search(('Planning|ProjectShot|Canvas|Docx|RichFlow|UiCopy|Navigation|Workbench|Toolbox|StartupSettings|GlobalSurface' if label=='CORE' else 'Planning|Canvas|UiCopy|Navigation|Workbench|ProductToolbox|RealLayout|StudioToolPanelLayout|AssetFolderTreeLayout|AssetLibraryEagleLayout|AssetLibraryP3Accessibility|BookingContinuePlanning|PhotoGradePageV1|TetherStartup|OnlineSelectionPersistence|OrganizeCommandState|CollageCommandState|PublishingRendering|Version230Rc2Navigation'),x['fullyQualifiedName'].rsplit('.',1)[0])})
 args=[sys.executable,str(r/'tools/whole-app-validation/verified_full_runner.py'),'--assembly',str(assembly),'--output',str(d/('FINAL_'+label)),'--dotnet',dotnet,'--resource-root',str(r),'--classes']+classes
 with (d/('FINAL_'+label+'.log')).open('wb') as log:
  start=time.time();p=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait()
 plans.append(dict(label=label,classes=classes,selected=len([x for x in tests if x['fullyQualifiedName'].rsplit('.',1)[0] in classes]),totalDiscovered=len(tests),args=args,pid=p.pid,exitCode=code,seconds=time.time()-start));print(label,code,flush=True)
 (d/'FINAL_RUN_PLAN.json').write_text(json.dumps(dict(sourceSha=sha,scope='Affected storage/planning/Core/WPF/DOCX only. Not full application suite. Classes selected from actual adapter discovery.',plans=plans),indent=2))
