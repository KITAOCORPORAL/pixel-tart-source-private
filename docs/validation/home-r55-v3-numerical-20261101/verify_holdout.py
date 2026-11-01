import json,math,subprocess,hashlib
from pathlib import Path
import numpy as np
r=Path(__import__('sys').argv[1]);st=r/'holdout-stages';results=[]
for name,amp in [('hold-y-ac3-96',96),('hold-cb-ac3-64',64)]:
 d=next(x for x in json.loads((st/'STAGES.json').read_text())if x['name']==name);w,h=d['width'],d['height'];p=st/(name+'-plane0-opt0.raw');plane=np.frombuffer(p.read_bytes(),np.uint8).reshape(h,w)
 if name.startswith('hold-y'):
  expected=np.rint([128+sum(amp/(4*math.sqrt(2))*math.cos((2*x+1)*3*math.pi/16)for x in range(start,start+4))/4 for start in [0,4]]).astype(int)
  assert np.abs(plane[:,:2].astype(int)-expected).max()<=1
  raw=r/(name+'-hold-ff.yuv');subprocess.run(['ffmpeg','-v','error','-lowres','2','-i',str(r/'holdout-coeff'/(name+'.jpg')),'-frames:v','1','-c:v','rawvideo','-f','rawvideo','-y',str(raw)],check=True);ff=np.frombuffer(raw.read_bytes(),np.uint8)[:w*h];assert np.all(ff==128);results.append({'relation':'JPEG-IDCT-AREA','name':name,'expectedFirstTwo':expected.tolist(),'wicFirstTwo':plane[0,:2].tolist(),'ffUnique':np.unique(ff).tolist(),'pass':True})
 else:
  first,second=d['planar'];assert first['planes'][1]['width']==w and second['planes'][1]['width']==w//2
  results.append({'relation':'JPEG-SCALE-GRID','name':name,'output':[w,h],'defaultCb':[first['planes'][1]['width'],first['planes'][1]['height']],'preserveCb':[second['planes'][1]['width'],second['planes'][1]['height']],'pass':True})
icc=json.loads((r/'holdout-contract-stages-icc-analysis/ICC_STAGES.json').read_text())
for row in icc:
 system=next(x for x in row['mscms']if x['intent']==1 and x['flags']==3);assert system['max']==0;assert row['orientationOnly']['max']==0;results.append({'relation':'ICC-SYSTEM-EXACT + EXIF-ONLY','name':row['name'],'systemMax':system['max'],'orientationMax':row['orientationOnly']['max'],'oldLcmsResult':row['lcms'][0],'pass':True,'oldLcmsStillFail':row['lcms'][0]['max']>1})
for name in ['hold-icc-dark-grid','hold-icc-rotated']:
 oracle=json.loads((r/('ICC_MECHANISM_'+name+'.json')).read_text())['oracle'];assert oracle['maxVsLcms']<=1;results.append({'relation':'ICC-ORACLE-DOMAIN','name':name,**oracle,'pass':True})
contract=Path(__file__).parent/'COMPARISON_CONTRACT_V1.json';(r/'HOLDOUT_RESULTS.json').write_text(json.dumps({'contractSha256':hashlib.sha256(contract.read_bytes()).hexdigest(),'relations':results,'scope':'Frozen diagnostic relation passed; original cross-engine acceptance FAIL retained'},indent=2));print(json.dumps(results,indent=2))
