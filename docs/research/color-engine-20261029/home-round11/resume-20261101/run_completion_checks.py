import ast,json,os,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[5];out=Path(__file__).parent
env=os.environ.copy();env['PYTHONPATH']='N:/pixart/ColorResearchHome-20261030/deps/python-frozen';env['PYTHONDONTWRITEBYTECODE']='1'
evidence='docs/research/color-engine-20261029/home-round11';local='N:/pixart/ColorResearchHome-20261030/round11/'
commands=[('saved-audit-tests-completion',[sys.executable,'-O','tools/color-round11/test_saved_outputs.py','-v']),('saved-natural-audit-completion',[sys.executable,'-O','tools/color-round11/verify_saved_outputs.py','--evidence',evidence,'--inputs',local+'natural-inputs-v1','--outputs',local+'natural-coverage-run1','--stages',local+'natural-stage-finite-v1','--output',str(out/'saved-natural-audit.json')]),('sealed-report-completion',[sys.executable,'-O','tools/color-round11/verify_round11.py',evidence])]
results=[]
for name,cmd in commands:
 with (out/(name+'.txt')).open('x',encoding='utf-8') as log:
  r=subprocess.run(cmd,cwd=root,env=env,capture_output=True,text=True);log.write(r.stdout+r.stderr)
 results.append({'name':name,'exitCode':r.returncode,'command':cmd});print(name,r.returncode,(r.stdout+r.stderr)[-1000:],flush=True)
files=list((root/'tools/color-round11').rglob('*.py'))
for path in files:ast.parse(path.read_text(encoding='utf-8-sig'),filename=str(path))
with (out/'checks-completion.json').open('x',encoding='utf-8') as f:json.dump({'date':'2026-11-01','timezone':'Europe/Helsinki','renderCalls':0,'rawDecodeCalls':0,'pythonSyntaxFiles':len(files),'results':results,'priorFailure':'checks-final.json saved-natural-audit: assumed per-photo sequence; corrected to measured process-global counter without altering pixels or prior files'},f,indent=2)
sys.exit(1 if any(x['exitCode'] for x in results) else 0)
