"""Validate public run artifacts without requiring a GPU or external engine."""
from pathlib import Path
import json,hashlib,argparse
from compare_rows import compare
from generate_inputs import sha
p=argparse.ArgumentParser();p.add_argument('fixtures',type=Path);p.add_argument('run',type=Path);a=p.parse_args();m=json.loads((a.fixtures/'fixtures.json').read_text());r=json.loads((a.run/'run.json').read_text());
if sha(a.fixtures/'fixtures.json')!=r['syntheticManifestHash']:raise ValueError('Synthetic manifest mismatch')
if len(r['cases'])!=3 or len(m['fixtures'])!=3:raise ValueError('Case count')
for case,fixture in zip(r['cases'],m['fixtures']):
 if case['id']!=fixture['id'] or sha(a.fixtures/fixture['path'])!=fixture['fileSha256']:raise ValueError('Input changed')
 folder=a.run/case['id'];shape=case['shape'];whole=folder/'whole.rgba16'
 if sha(whole)!=case['wholeInfo']['sha256']:raise ValueError('Whole changed')
 for rows in [1,7,17,64]:
  d=compare(whole,folder/f'stream-r{rows}.rgba16',shape,'<u2')
  if d['changedChannels']:raise ValueError('Same-device exact gate failed')
 for kind,file in [('whole','whole-traced.rgba16'),('stream','stream-traced-r7.rgba16')]:
  if compare(whole,folder/file,shape,'<u2')['changedChannels']:raise ValueError('Passive trace changed final')
 for item in case['stageWhole']+case['stageStream']:
  if sha(a.run/item['file'])!=item['sha256']:raise ValueError('Stage changed')
 if not case['naturalEntrySyntheticRejected']:raise ValueError('Natural guard not exercised')
if sha(a.run/'bt-decode.f32')!=r['btDiagnostic']['outputHash']:raise ValueError('BT changed')
print('PUBLIC_SYNTHETIC_PACKAGE_VALID; same-device exact; company execution remains separate')

