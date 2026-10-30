"""Passive existing-build trace of default scanner finish on registered native windows."""
from pathlib import Path
import argparse,json,sys,os,hashlib
import numpy as np
p=argparse.ArgumentParser();p.add_argument('local',type=Path);p.add_argument('engine',type=Path);a=p.parse_args();root=a.local;engine=a.engine;sys.path[:0]=[str(engine/'source2/engine/tests'),str(engine/'source2/engine/tools')]
from spk_ctypes import Engine
from windows_lifecycle import OwnedResult
for route in ['A','B']:
 d=root/('unsharp-trace-'+route)
 if d.exists():raise ValueError('Use a new local evaluation root; trace already exists')
 d.mkdir();os.environ['SPK_RESEARCH_TRACE']=str(d);saved=json.loads((root/f'portra-{route}-crop-edge_people/report.json').read_text());image=np.load(saved['inputFile'])
 with Engine(library=engine/'build-trace/engine/spektrafilm_engine.dll',resources=engine/'resources-patch') as e:
  with e.open(image,saved['resolved']) as session:
   if session.get_params()!=saved['resolved']:raise ValueError('Trace parameters changed')
   with OwnedResult(session,'full') as result:out=result.pixels().copy()
 if hashlib.sha256(out.tobytes()).hexdigest()!=saved['outputPixelSha256']:raise ValueError('Passive trace changed original output')
 print(route,'default unsharp actual trace output exact')
os.environ.pop('SPK_RESEARCH_TRACE',None)
