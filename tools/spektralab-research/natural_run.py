"""Dispatch serial, each case in a fresh process; no implicit proxy substitution."""
raise SystemExit("Legacy round6 batch is frozen and cannot satisfy V2 admission. Use natural_prepare --library/--resources and natural_case --manifest/--manifest-sha; do not resubmit six full tasks.")
from pathlib import Path
import argparse,json,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('local',type=Path);p.add_argument('engine',type=Path);a=p.parse_args();root=a.local;source=a.engine/'source2';library=a.engine/'build-patch/engine/spektrafilm_engine.dll';resources=a.engine/'resources-patch';script=Path(__file__).with_name('natural_case.py');pairs=[('portra','kodak_portra_400','kodak_portra_endura'),('ektar','kodak_ektar_100','kodak_ultra_endura'),('trix','kodak_tri_x_400','ilford_multigrade_iv_rc')];records=[]
def call(folder,inputpath,route,film,paper,role,repeat=False,reopen=False):
 cmd=[sys.executable,str(script),'reopen' if reopen else 'case',str(source),str(library),str(resources),str(inputpath),str(folder),'--route',route,'--film',film,'--paper',paper,'--role',role]+(['--repeat'] if repeat else [])
 p=subprocess.run(cmd,capture_output=True,text=True);reportfile=folder/'report.json'
 if not reportfile.exists():raise RuntimeError(p.stdout+p.stderr)
 report=json.loads(reportfile.read_text());records.append({'directory':folder.name,'returncode':p.returncode,'status':report['status'],'role':role,'route':route,'film':film,'report':str(reportfile)});print(p.stdout.strip(),flush=True)
 # Unexpected engine/test failure is retained, not converted to a pass.
 (root/'natural-run-index.json').write_text(json.dumps(records,indent=2));return report
for short,film,paper in pairs:
 for route,label in [('A','A-linear-d50'),('B','B-display-srgb')]:
  proxy=root/f'{short}-{route}-proxy';inp=root/'canon-inputs'/f'{label}-proxy.npy'
  if proxy.exists():
   r=json.loads((proxy/'report.json').read_text());records.append({'directory':proxy.name,'status':r['status'],'role':'proxy','route':route,'film':film,'report':str(proxy/'report.json')})
  else:call(proxy,inp,route,film,paper,'proxy',True)
  call(root/f'{short}-{route}-proxy-reopen',proxy/'report.json',route,film,paper,'proxy',reopen=True)
  call(root/f'{short}-{route}-full',root/'canon-inputs'/f'{label}-full.npy',route,film,paper,'full',False)
  for region in ['edge_people','shadow_vehicle','sky_wire']:
   crop=root/f'{short}-{route}-crop-{region}';r=call(crop,root/'canon-inputs'/f'{label}-crop-{region}.npy',route,film,paper,'crop-native-'+region,short=='portra' and region=='edge_people')
   if short=='portra' and region=='edge_people' and r['status'].startswith('RENDERED'):call(root/f'{short}-{route}-crop-{region}-reopen',crop/'report.json',route,film,paper,'crop-native-'+region,reopen=True)
(root/'natural-run-index.json').write_text(json.dumps(records,indent=2))
