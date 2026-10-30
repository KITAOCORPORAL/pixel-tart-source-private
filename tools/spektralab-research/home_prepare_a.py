"""A only: reproduce frozen bridge bytes from actual production decode, no new math."""
import argparse,json,hashlib,copy
from pathlib import Path
import numpy as np
from PIL import Image
from natural_admission import *
from host_environment import environment
p=argparse.ArgumentParser();p.add_argument('production',type=Path);p.add_argument('output',type=Path);a=p.parse_args();host=environment();a.output=output_path(a.output);a.output.mkdir();repo=Path(__file__).resolve().parents[2];frozen=load_json(repo/'docs/research/color-engine-20261029/company-round7/new-preparation-manifest-v2.json');prod=load_json(a.production/'report.json');company=load_json(repo/'docs/research/color-engine-20261029/company-round6/production-decode.json')
for key in ['width','height','productionLinearSha256','productionDisplaySha256']:need(prod[key]==company[key],'Production mismatch '+key)
need(prod['sourceSha256']==host['sourceSha256'] and sha(Path(host['sourceFile'])).upper()==host['sourceSha256'],'Wrong source');w,h=prod['width'],prod['height'];bridge=frozen['bridge'];matrix=np.array(bridge['combinedMatrix'],np.float64);rawpath=a.production/'production-linear-d65.f32';need(sha(rawpath).upper()==prod['productionLinearSha256'],'Production master changed');raw=np.memmap(rawpath,dtype='<f4',mode='r',shape=(h,w,3));target=a.output/'A-linear-d50-full.npy';arr=np.lib.format.open_memmap(target,mode='w+',dtype='<f4',shape=(h,w,3))
try:
 for y in range(0,h,32):arr[y:y+32]=(np.asarray(raw[y:y+32],np.float64)@matrix.T).astype(np.float32)
 arr.flush()
finally:arr._mmap.close();raw._mmap.close()
fullInfo=npy_info(target);stats=scan(target,fullInfo);companyEntry=next(x for x in frozen['files'] if x['path']=='A-linear-d50-full.npy');need(stats['pixelSha256']==companyEntry['pixelSha256'] and fullInfo['shape']==companyEntry['shape'],'Frozen A pixels/dimensions differ')
entries=[{'path':target.name,'fileSha256':sha(target),'shape':fullInfo['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':bridge['version'],'pixelSha256':stats['pixelSha256'],'bytes':fullInfo['fileBytes']}]
m={'version':'NaturalAdmissionManifestV2','inputRoot':str(a.output.resolve()),'sourceSha256':prod['sourceSha256'],'productionReportSha256':sha(a.production/'report.json'),'orientation':1,'bridge':bridge,'backend':{'path':host['wholeLibrary'],'sha256':sha(Path(host['wholeLibrary']))},'resources':resources(Path(host['resources'])),'preparationIdentity':{'sourceSha256':prod['sourceSha256'],'mode':'Home reconstruction from same source and frozen bridge; A only','productionLinearSha256':prod['productionLinearSha256'],'companyAInputPixelSha256':companyEntry['pixelSha256']},'files':entries};atomic_json(a.output/'admission-manifest-v2.json',m)
small=Path(host['smallRoot']);need(not small.exists(),'Small input output exists');small.mkdir();source=np.load(target,mmap_mode='r');smallEntries=[]
try:
 for width,height in [(321,215),(641,429),(1200,801)]:
  image=np.empty((height,width,3),np.float32)
  for c in range(3):
   channel=np.empty((h,w),np.float32)
   for y in range(0,h,32):channel[y:y+32]=source[y:y+32,:,c]
   image[:,:,c]=np.asarray(Image.fromarray(channel,mode='F').resize((width,height),Image.Resampling.BOX));del channel
  path=small/f'whole-scene-{width}x{height}.npy';np.save(path,image);info=npy_info(path);st=scan(path,info);smallEntries.append({'path':path.name,'fileSha256':sha(path),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':bridge['version'],'pixelSha256':st['pixelSha256'],'bytes':info['fileBytes']})
finally:source._mmap.close()
# Synthetic boundary test has scene-lineage source identity but is never called natural coverage.
syn=np.zeros((65,97,3),np.float32);syn[:,:,0]=np.linspace(-.2,2,97,dtype=np.float32);syn[:,:,1]=np.linspace(0,1,65,dtype=np.float32)[:,None];syn[:,:,2]=.18;syn[0,0]=[-1,1e-12,3];path=small/'synthetic.npy';np.save(path,syn);info=npy_info(path);st=scan(path,info);smallEntries.append({'path':path.name,'fileSha256':sha(path),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':bridge['version'],'pixelSha256':st['pixelSha256'],'bytes':info['fileBytes'],'synthetic':True})
sm=copy.deepcopy(m);sm.update(inputRoot=str(small.resolve()),files=smallEntries);atomic_json(small/'admission-manifest-v2.json',sm);atomic_json(a.output/'home-reconstruction.json',{'version':'HomeSameInputReconstructionV1','sourceSha256':prod['sourceSha256'],'companyA':companyEntry,'homeA':entries[0],'samePixelHash':True,'sameFileHash':entries[0]['fileSha256']==companyEntry['fileSha256'],'smallFiles':smallEntries});print(json.dumps({'fullPixelHash':stats['pixelSha256'],'companyExact':True,'smallCases':len(smallEntries)}))
