from pathlib import Path
import sys,json,base64,hashlib,urllib.request
r=Path('E:/Kitao-Deliveries/r54-nodes-20261028');sys.path.insert(0,str(r/'ref-deps'))
import numpy as np,cv2
from PIL import Image,ImageChops
rows=json.loads((r/'BLEND_CORPUS.json').read_text(encoding='utf8'));stats={}
for row in rows:
 a=np.array(list(base64.b64decode(row['a'])),dtype='uint8').reshape(1,1,3);b=np.array(list(base64.b64decode(row['b'])),dtype='uint8').reshape(1,1,3);mode=row['mode'];t=row['amount']
 ia=Image.fromarray(a);ib=Image.fromarray(b);fn={'Normal':lambda a,b:b,'Multiply':ImageChops.multiply,'Screen':ImageChops.screen,'Darken':ImageChops.darker,'Lighten':ImageChops.lighter}[mode];pil=np.array(Image.blend(ia,fn(ia,ib),t)).reshape(3)/255.
 if mode=='Normal':mixed=b
 elif mode=='Multiply':mixed=cv2.multiply(a,b,scale=1/255.)
 elif mode=='Screen':mixed=255-cv2.multiply(255-a,255-b,scale=1/255.)
 elif mode=='Darken':mixed=cv2.min(a,b)
 else:mixed=cv2.max(a,b)
 ocv=cv2.addWeighted(a,1-t,mixed,t,0).reshape(3)/255.;actual=np.array(row['actual']);entry=stats.setdefault(mode,{'pillow_max8':0,'opencv_max8':0,'n':0});entry['n']+=1;entry['pillow_max8']=max(entry['pillow_max8'],float(np.max(np.abs(actual-pil))*255));entry['opencv_max8']=max(entry['opencv_max8'],float(np.max(np.abs(actual-ocv))*255))
print(stats)
result={'status':'PASS' if all(max(v['pillow_max8'],v['opencv_max8'])<=2.1 for v in stats.values()) else 'FAIL','fixed_before_execution_max8':2.1,'meaning':'existing encoded sRGB adjustment-result blend, same opaque branch inputs; PIL/OpenCV quantization differs from product float; no physical colour accuracy claim','corpus_sha256':hashlib.sha256((r/'BLEND_CORPUS.json').read_bytes()).hexdigest(),'samples':len(rows),'versions':{'Pillow':__import__('PIL').__version__,'OpenCV':cv2.__version__},'stats':stats,'references':[{'project':'python-pillow/Pillow','commit':'bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d','files':['src/PIL/ImageChops.py','src/libImaging/Chops.c','LICENSE'],'license':'MIT-CMU (Pillow license); reference runtime only'},{'project':'opencv/opencv','commit':'71d3237a093b60a27601c20e9ee6c3e52154e8b1','files':['modules/core/src/arithm.cpp','LICENSE'],'license':'Apache-2.0; reference runtime only'}]}
(r/'BLEND_TWO_REFERENCE_RESULT.json').write_text(json.dumps(result,indent=2),encoding='utf8')
# Actually retrieve and read fixed implementation entry files/licenses, not only project names.
f=r/'blend-reference';f.mkdir(exist_ok=True);reads=[]
for repo,sha,paths in [('python-pillow/Pillow','bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d',['src/PIL/ImageChops.py','src/libImaging/Chops.c','LICENSE']),('opencv/opencv','71d3237a093b60a27601c20e9ee6c3e52154e8b1',['modules/core/src/arithm.cpp','LICENSE'])]:
 for path in paths:
  url=f'https://raw.githubusercontent.com/{repo}/{sha}/{path}'
  try:
   data=urllib.request.urlopen(url,timeout=35).read();p=f/(repo.split('/')[1]+'-'+path.replace('/','_'));p.write_bytes(data);reads.append({'url':url,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
  except Exception as e:reads.append({'url':url,'error':str(e)})
(r/'BLEND_REFERENCE_FILES.json').write_text(json.dumps(reads,indent=2),encoding='utf8')
