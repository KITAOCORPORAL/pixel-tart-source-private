import ast,json,os,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[5]
out=Path(__file__).parent
env=os.environ.copy();env['PYTHONPATH']='N:/pixart/ColorResearchHome-20261030/deps/python-frozen';env['PYTHONDONTWRITEBYTECODE']='1'
evidence='docs/research/color-engine-20261029/home-round11'
local='N:/pixart/ColorResearchHome-20261030/round11/'
commands=[
 ('independent-final',[sys.executable,'-O','tools/color-round11/diagnostic/test_independent_validation.py','-v']),
 ('two-side-final',[sys.executable,'-O','tools/color-round11/diagnostic/test_two_side_compare.py','-v']),
 ('saved-audit-tests',[sys.executable,'-O','tools/color-round11/test_saved_outputs.py','-v']),
 ('saved-natural-audit',[sys.executable,'-O','tools/color-round11/verify_saved_outputs.py','--evidence',evidence,'--inputs',local+'natural-inputs-v1','--outputs',local+'natural-coverage-run1','--stages',local+'natural-stage-finite-v1','--output',str(out/'saved-natural-audit.json')]),
 ('sealed-report-final',[sys.executable,'-O','tools/color-round11/verify_round11.py',evidence]),
 ('home-package-final',[sys.executable,'-O','tools/color-round11/diagnostic/verify_run.py',evidence+'/diagnostic-home-v2','--role','home']),
 ('legacy-package-final',[sys.executable,'-O','tools/color-round11/diagnostic/verify_run.py','docs/research/color-engine-20261029/home-round10/diagnostic-home','--role','home','--allow-sealed-round10'])]
results=[]
for name,command in commands:
 result=subprocess.run(command,cwd=root,env=env,capture_output=True,text=True)
 with (out/(name+'.txt')).open('x',encoding='utf-8') as log:log.write(result.stdout+result.stderr)
 results.append({'name':name,'exitCode':result.returncode,'command':command})
 print(name,result.returncode,(result.stdout+result.stderr)[-700:],flush=True)
files=list((root/'tools/color-round11').rglob('*.py'))
for path in files:ast.parse(path.read_text(encoding='utf-8-sig'),filename=str(path))
report={'date':'2026-11-01','timezone':'Europe/Helsinki','renderCalls':0,'rawDecodeCalls':0,'pythonSyntaxFiles':len(files),'results':results}
with (out/'checks-final.json').open('x',encoding='utf-8') as log:log.write(json.dumps(report,indent=2))
sys.exit(1 if any(item['exitCode'] for item in results) else 0)
