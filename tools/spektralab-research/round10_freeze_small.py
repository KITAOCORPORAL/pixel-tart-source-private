from pathlib import Path
from types import SimpleNamespace
import os,json,subprocess,sys
from host_environment import environment
from natural_admission import sha
from execution_freeze import create_snapshot
h=environment();r=Path(h['researchRoot']);m=Path(h['smallRoot'])/'admission-manifest-v2.json';a=SimpleNamespace(input=Path(h['smallRoot'])/'whole-scene-321x215.npy',manifest=m,library=Path(h['streamLibrary']),resources=Path(h['resources']));freeze=r/'evidence/round10-small-execution-freeze.json';create_snapshot(a,freeze);cmd=[sys.executable,str(Path(__file__).with_name('stream_case.py')),str(a.input),str(r/'round10-frozen-small'),'--manifest',str(m),'--manifest-sha',sha(m),'--library',str(a.library),'--resources',str(a.resources),'--rows','127','--consumer-rows','7','--oracle',str(Path(h['smallRoot'])/'321-whole/rgba16.bin'),'--freeze',str(freeze)];p=subprocess.run(cmd,capture_output=True,text=True);(r/'evidence/round10-frozen-small-console.txt').write_text(p.stdout+p.stderr);print(p.stdout+p.stderr);raise SystemExit(p.returncode)
