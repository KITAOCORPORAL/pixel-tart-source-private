import json,subprocess,sys
from pathlib import Path
from stage_validation import validate_stage,validate_candidate,require
root=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(exist_ok=False);m=validate_stage(root);s=m['sizes'];h=s['IHeight'];w=s['IWidth'];oracle=Path(sys.argv[3]);validate_candidate(Path(sys.argv[4]),m)
report={}
for kind,inputname in [('u16x4','matrix_after.u16'),('f32',sys.argv[4])]:
 for ev in [0,-2]:
  output=out/f'{kind}_ev{ev}.f64';source=root/inputname if kind=='u16x4' else Path(inputname);subprocess.run(['dotnet',str(oracle),str(source),str(output),kind,str(ev)],check=True);args=[sys.executable,str(Path(__file__).with_name('crosscheck_full_display.py')),str(root),kind,str(ev),str(output)]
  if kind=='f32':args.append(str(source))
  result=subprocess.run(args,check=True,text=True,capture_output=True);report[f'{kind}_EV{ev}']=json.loads(result.stdout)
(out/'summary.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
