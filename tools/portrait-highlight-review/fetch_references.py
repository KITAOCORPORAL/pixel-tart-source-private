"""Fetch only the seven pinned public reference source/license blobs."""
from pathlib import Path
import argparse,concurrent.futures,hashlib,json,urllib.request
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('frozen_reference_manifest',type=Path);a=p.parse_args();folder=a.root/'references';folder.mkdir(exist_ok=True,parents=True)
manifest=json.loads(a.frozen_reference_manifest.read_text('utf-8-sig'));names=['dt-exposure.c','dt-LICENSE','rt-improcfun.cc','rt-curves.cc','rt-curves.h','rt-LUT.h','rt-LICENSE']
def one(pair):
 record,name=pair;path=folder/name
 if path.exists():data=path.read_bytes()
 else:
  url='https://raw.githubusercontent.com/'+record['repository']+'/'+record['commit']+'/'+record['path'];request=urllib.request.Request(url,headers={'User-Agent':'PortraitHighlightFrozenReferenceReview'})
  with urllib.request.urlopen(request,timeout=40) as response:data=response.read()
  with path.open('xb') as output:output.write(data)
 assert len(data)==record['bytes'] and hashlib.sha256(data).hexdigest()==record['sha256'],name
 return dict(**record,localName=name,hashVerified=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:files=list(pool.map(one,zip(manifest['sources'],names)))
output=a.root/'PINNED_REFERENCE_SOURCE_READBACK.json'
if not output.exists():output.write_bytes((json.dumps(dict(status='PASS_EXACT_FROZEN_SOURCE_BYTES',files=files),indent=2)+'\n').encode())
print('PASS: seven exact pinned source/license blobs')
