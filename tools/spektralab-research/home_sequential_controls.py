"""Final caller sequential reuse: no20MP rerender and no hidden cap increase."""
import argparse
from pathlib import Path
from types import SimpleNamespace
from host_environment import environment
from natural_admission import load_json,sha,atomic_json,need
from stream_case import run
p=argparse.ArgumentParser();p.add_argument('--prefix',required=True);a=p.parse_args();host=environment();root=Path(host['researchRoot']);small=Path(host['smallRoot']);manifest=small/'admission-manifest-v2.json';records=[]
for i,consumer in enumerate([3,11]):
 args=SimpleNamespace(input=small/'whole-scene-321x215.npy',output=root/f'{a.prefix}-{i}',manifest=manifest,manifest_sha=sha(manifest),library=Path(host['streamLibrary']),resources=Path(host['resources']),rows=37,consumer_rows=consumer,device_mib=256,staging_mib=64,host_mib=512,oracle=small/'321-whole/rgba16.bin',fault='none');r=run(args);records.append(r);need(r['success'] and r['oracle']['changedChannels']==0 and r['native']['deviceLiveAfter']==r['native']['stagingLiveAfter']==0,'Sequential call failed')
atomic_json(root/'evidence'/f'{a.prefix}.json',{'version':'HomeFinalCallerSameProcessV1','cases':records,'no20MPrender':True})
