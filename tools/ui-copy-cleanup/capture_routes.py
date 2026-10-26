import pathlib,subprocess,os,sys,json,shutil
r=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026");d=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");dotnet='D:/AI AGENT/.dotnet/dotnet.exe';host=r/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0';old=pathlib.Path('E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023/Planning-BoundedThumbnails-Diagnostic-win-x64/app');env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','');env['KITAO_TEST_RESOURCE_ROOT']=str(r)
for label in ['before','after']:
 target=d/('route-host-'+label)
 if target.exists():raise FileExistsError('New capture hosts required')
 shutil.copytree(host,target)
 if label=='before':
  for f in old.glob('*.dll'):
   if (target/f.name).exists():shutil.copy2(f,target/f.name)
 out=d/('routes-'+label);out.mkdir();local=env.copy();local.update({'PIXEL_TART_ISOLATED_RUNTIME':'1','PIXEL_TART_ISOLATED_RUNTIME_ROOT':str(out/'runtime'),'PIXEL_TART_ACCEPTANCE_ROOT':str(out/'runtime'),'PIXEL_TART_HUMAN_ACCEPTANCE':'0','KITAO_COPY_ROUTE_EVIDENCE':str(out/'images')})
 if label=='before':local['KITAO_COPY_ROUTES']='Planning,Settings,Tether,Toolbox,Finance'
 args=[dotnet,'vstest',str(target/'RAWSelectionAssistant.WpfTests.dll'),'/TestCaseFilter:FullyQualifiedName=RAWSelectionAssistant.WpfTests.UiCopyRouteEvidenceTests.CurrentProductionRoutesCaptureSyntheticStatesWithoutNativeClaim','/Logger:trx;LogFileName=routes.trx','/ResultsDirectory:'+str(out)]
 with (out/'run.log').open('wb') as log:p=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,env=local,cwd=r,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait(timeout=300)
 print(label,code,flush=True)
