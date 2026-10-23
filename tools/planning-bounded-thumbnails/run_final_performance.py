import pathlib,shutil,subprocess,os,json,time,hashlib
r=pathlib.Path("E:/Kitao-Worktrees/planning-bounded-thumbnails-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023");dotnet='D:/AI AGENT/.dotnet/dotnet.exe';host=r/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0';baseline=pathlib.Path('E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/Planning-OwnedJPEG-Diagnostic-win-x64/app');env=os.environ.copy();env['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent);env['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH','');env['KITAO_TEST_RESOURCE_ROOT']=str(r);env['KITAO_LARGE_FIXTURES']=str(d/'large-fixtures');records=[]
for mode in ['before','after']:
 target=d/('perf-final-host-'+mode)
 if target.exists():raise FileExistsError('Preserve old measurement host')
 shutil.copytree(host,target)
 if mode=='before':
  for f in baseline.glob('*.dll'):
   if (target/f.name).exists():shutil.copy2(f,target/f.name)
 if mode=='after':
  for f in pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023/Planning-BoundedThumbnails-Diagnostic-win-x64/app").glob('*.dll'):
   if (target/f.name).exists():shutil.copy2(f,target/f.name)
 for i in range(3):
  out=d/('perf-final-'+mode+'-'+str(i));out.mkdir();local=env.copy();temp=out/'temp';temp.mkdir();local.update({'TEMP':str(temp),'TMP':str(temp),'PIXEL_TART_ISOLATED_RUNTIME':'1','PIXEL_TART_ISOLATED_RUNTIME_ROOT':str(out/'runtime'),'PIXEL_TART_ACCEPTANCE_ROOT':str(out/'runtime'),'PIXEL_TART_HUMAN_ACCEPTANCE':'0','KITAO_THUMB_PERF_OUTPUT':str(out)})
  args=[dotnet,'vstest',str(target/'RAWSelectionAssistant.WpfTests.dll'),'/TestCaseFilter:FullyQualifiedName=RAWSelectionAssistant.WpfTests.PlanningBoundedThumbnailTests.SameFixtureLargeOpenWarmCacheAndMultiShotMeasurement','/Logger:trx;LogFileName=result.trx','/ResultsDirectory:'+str(out)]
  with (out/'run.log').open('wb') as log:start=time.time();p=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,env=local,cwd=r,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait(timeout=300)
  record=dict(mode=mode,iteration=i,args=args,pid=p.pid,exitCode=code,seconds=time.time()-start,wpfSha256=hashlib.sha256((target/'KitaoPhotoSelector.dll').read_bytes()).hexdigest(),testSha256=hashlib.sha256((target/'RAWSelectionAssistant.WpfTests.dll').read_bytes()).hexdigest());records.append(record);(d/'FINAL_PERFORMANCE_RUNS.json').write_text(json.dumps(records,indent=2));print(mode,i,code,flush=True)
  if code:raise RuntimeError('Measurement failed; preserve original run')
