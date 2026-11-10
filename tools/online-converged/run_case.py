from pathlib import Path
import os,subprocess,json,sys,time,xml.etree.ElementTree as ET,re
root=Path.cwd();base=Path('E:/Kitao-Deliveries/online-selection-converged-20261110');out=base/sys.argv[1];out.mkdir(exist_ok=False,parents=True)
dotnet=re.search(r'dotnet="([^"]+)"',Path('tools/whole-app-followup/build_current.py').read_text(encoding='utf-8-sig')).group(1)
connection=json.loads(Path(sys.argv[4]).read_text(encoding='utf-8-sig'));env=os.environ.copy();env.update(PIXELTART_SELECTION_ENDPOINT=connection['endpoint'],PIXELTART_SELECTION_OWNER_KEY=connection['ownerKey']);env.update(DOTNET_ROOT=str(Path(dotnet).parent),PATH=str(Path(dotnet).parent)+os.pathsep+env.get('PATH',''),PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(base/((sys.argv[5]if len(sys.argv)>5 else 'dev01')+'-app-runtime')),KITAO_FAVORITES_ACCEPTANCE_ROOT=str(base/((sys.argv[5]if len(sys.argv)>5 else 'dev01')+'-app-evidence')))
if '--manual-picker' in sys.argv:env['KITAO_HTTP_NATIVE_PICKER_MANUAL']='1'
assembly=root/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll';filter=sys.argv[2];expected=int(sys.argv[3])
args=[dotnet,'vstest',str(assembly),'/TestCaseFilter:'+filter,'/Logger:trx;LogFileName=results.trx','/ResultsDirectory:'+str(out)]
with (out/'stdout.log').open('wb')as log:
 start=time.time();p=subprocess.Popen(args,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
 try:code=p.wait(timeout=240)
 except subprocess.TimeoutExpired:p.kill();code=-100
observed=[];trx=out/'results.trx'
if trx.exists():observed=[dict(x.attrib)for x in ET.parse(trx).findall('.//{*}UnitTestResult')]
receipt=dict(sourceSha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),dirty=subprocess.check_output(['git','status','--porcelain'],text=True).splitlines(),args=args,pid=p.pid,exitCode=code,seconds=time.time()-start,expected=expected,results=observed)
(out/'RECEIPT.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(dict(exitCode=code,count=len(observed),outcomes=[x.get('outcome')for x in observed])));sys.exit(0 if code==0 and len(observed)==expected and all(x['outcome']=='Passed'for x in observed)else 1)
