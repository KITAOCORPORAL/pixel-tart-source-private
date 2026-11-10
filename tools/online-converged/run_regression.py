from pathlib import Path
import json,os,subprocess,re,time,xml.etree.ElementTree as ET,collections
r=Path.cwd();d=Path('E:/Kitao-Deliveries/online-selection-converged-20261110');o=d/'regression01';o.mkdir(exist_ok=False)
dotnet=re.search(r'dotnet="([^"]+)"',(r/'tools/whole-app-followup/build_current.py').read_text()).group(1);sdk=str(Path(dotnet).parent/'sdk/10.0.302/vstest.console.dll');env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent);env['PATH']=str(Path(dotnet).parent)+os.pathsep+env.get('PATH','');env['KITAO_TEST_RESOURCE_ROOT']=str(r);[env.pop(k,None)for k in ['PIXELTART_SELECTION_ENDPOINT','PIXELTART_SELECTION_OWNER_KEY','PIXELTART_SELECTION_PUBLIC_BASE_URL']]
plans={'Core':(r/'tests/RAWSelectionAssistant.Tests/bin/x64/Release/net10.0/RAWSelectionAssistant.Tests.dll',['OnlineSelectionV1Tests','OnlineSelectionProxySafetyClosureTests','CanvasFavoriteStoreTests','CanvasProductClosureTests','CanvasPlanningSourceTests','PlanningReadableSummaryTests','OnboardingRequirementTests']),'Wpf':(r/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll',['OnlineHttpResultGuardTests','OnlineSelectionV1WpfTests','OnlineSelectionProxyClosureWpfTests','OnlineSelectionPersistenceWpfTests','CanvasUsageStateTests','PaletteCanvasObjectTests','CanvasSourceSafetyTests','CanvasInspirationBoardLinkTests','CanvasProjectLinkTests','WholeAppDiagnosticPackageTests','TutorialRetirementProductTests'])}
summary=[];num=0
for kind,(assembly,classes) in plans.items():
 discovery=o/(kind+'-discovery.json')
 subprocess.run([dotnet,str(r/'tools/VerifiedTestRunner/bin/Release/net10.0/VerifiedTestRunner.dll'),sdk,str(assembly),str(discovery)],check=True,env=env,stdout=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 cases=json.loads(discovery.read_text(encoding='utf-8-sig'));selected=[x for x in cases if x['fullyQualifiedName'].split('.')[-2] in classes and not x['fullyQualifiedName'].endswith('.PublicRawSamples_WhenPresentGeneratePrivateProxiesWithoutChangingSources')]
 assert set(x['fullyQualifiedName'].split('.')[-2] for x in selected)==set(classes)
 methods=collections.defaultdict(list)
 for x in selected:methods[x['fullyQualifiedName']].append(x)
 for fqn,rows in methods.items():
  num+=1;folder=o/f'{num:03}';folder.mkdir();local=env.copy();local.update(PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(folder/'runtime'),KITAO_RETIREMENT_APP_EVIDENCE=str(folder/'retirement'),KITAO_RETIREMENT_SCENARIO='first')
  args=[dotnet,'vstest',str(assembly),'/TestCaseFilter:FullyQualifiedName='+fqn,'/Logger:trx;LogFileName=results.trx','/ResultsDirectory:'+str(folder)]
  with (folder/'stdout.log').open('wb') as log:
   start=time.time();p=subprocess.Popen(args,env=local,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
   try:code=p.wait(timeout=190)
   except subprocess.TimeoutExpired:subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW);code=-100
  trx=folder/'results.trx';results=[]
  if trx.exists():results=[dict(x.attrib) for x in ET.parse(trx).findall('.//{*}UnitTestResult')]
  ok=code==0 and len(results)==len(rows) and all(x['outcome']=='Passed' for x in results)
  summary.append(dict(kind=kind,fqn=fqn,pid=p.pid,args=args,seconds=time.time()-start,exitCode=code,expected=len(rows),results=results,ok=ok,directory=str(folder)))
  (o/'SUMMARY.json').write_text(json.dumps(dict(sourceSha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),runs=summary,counts=dict(collections.Counter(x['outcome'] for row in summary for x in row['results'])),ok=all(row['ok'] for row in summary)),indent=2));print(num,fqn.split('.')[-1],ok,flush=True)
