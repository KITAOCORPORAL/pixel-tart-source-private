"""One explicitly registered extra native-coordinate window, not another scene."""
raise SystemExit("Legacy round6 batch is frozen and cannot satisfy V2 admission. Use natural_prepare --library/--resources and natural_case --manifest/--manifest-sha; do not resubmit six full tasks.")
from pathlib import Path
import argparse,json,subprocess,sys
import numpy as np
p=argparse.ArgumentParser();p.add_argument('local',type=Path);p.add_argument('engine',type=Path);a=p.parse_args();root=a.local;box={'left':3500,'top':1350,'width':640,'height':640};name='wire_edge';records=json.loads((root/'natural-run-index.json').read_text())
for route,label in [('A','A-linear-d50'),('B','B-display-srgb')]:
 f=np.load(root/'canon-inputs'/f'{label}-full.npy',mmap_mode='r');inp=root/'canon-inputs'/f'{label}-crop-{name}.npy';require=not inp.exists()
 if not require:raise ValueError('New window input exists')
 np.save(inp,f[box['top']:box['top']+640,box['left']:box['left']+640].copy())
 for short,film,paper in [('portra','kodak_portra_400','kodak_portra_endura'),('ektar','kodak_ektar_100','kodak_ultra_endura'),('trix','kodak_tri_x_400','ilford_multigrade_iv_rc')]:
  dest=root/f'{short}-{route}-crop-{name}';cmd=[sys.executable,str(Path(__file__).with_name('natural_case.py')),'case',str(a.engine/'source2'),str(a.engine/'build-patch/engine/spektrafilm_engine.dll'),str(a.engine/'resources-patch'),str(inp),str(dest),'--route',route,'--film',film,'--paper',paper,'--role','crop-native-'+name];p=subprocess.run(cmd,capture_output=True,text=True);r=json.loads((dest/'report.json').read_text());records.append({'directory':dest.name,'returncode':p.returncode,'status':r['status'],'role':'crop-native-'+name,'route':route,'film':film,'report':str(dest/'report.json')});print(p.stdout.strip())
(root/'additional-windows.json').write_text(json.dumps({'version':'AdditionalNaturalWindowV1','regions':{name:box},'sameScene':True,'earlierWindowNameCorrection':'sky_wire original coordinates show sky only; do not claim wire coverage there'},indent=2));(root/'natural-run-index.json').write_text(json.dumps(records,indent=2))
