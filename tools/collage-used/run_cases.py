from pathlib import Path
import os,subprocess,json,sys,time,xml.etree.ElementTree as ET,re
root=Path.cwd();base=Path('E:/Kitao-Deliveries/r55-collage-used-20261109');out=base/sys.argv[1];out.mkdir(exist_ok=False,parents=True)
dotnet=re.search(r'dotnet="([^"]+)"',Path('tools/whole-app-followup/build_current.py').read_text()).group(1)
env=os.environ.copy();env.update(DOTNET_ROOT=str(Path(dotnet).parent),PATH=str(Path(dotnet).parent)+os.pathsep+env.get('PATH',''),PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(out/'runtime'),KITAO_FAVORITES_ACCEPTANCE_ROOT=str(out/'evidence'))
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
