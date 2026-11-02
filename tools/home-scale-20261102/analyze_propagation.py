"""CPU readback only: distinguish propagated tc_b changes from LUT-output differences with same per-pixel inputs."""
from pathlib import Path
import sys,json
import numpy as np
repo=Path(__file__).resolve().parents[2];home=Path(sys.argv[1]);output=Path(sys.argv[2]);public=repo/'docs/research/color-engine-20261029/company-scale-localization-20261102/public-gray-taps'
if output.exists():raise ValueError('New CPU analysis output required')
def load(root,name,c):return np.fromfile(root/'taps'/name,dtype='<f4').reshape(17,33,c)
ht,ct=load(home,'01-tc.device.f32',2),load(public,'01-tc.device.f32',2);hb,cb=load(home,'02-brightness.device.f32',1),load(public,'02-brightness.device.f32',1);hu,cu=load(home,'03-upsample.device.f32',3),load(public,'03-upsample.device.f32',3)
sameTc=np.all(ht.view('<u4')==ct.view('<u4'),axis=2);sameB=np.all(hb.view('<u4')==cb.view('<u4'),axis=2);sameInputs=sameTc&sameB;upChanged=hu.view('<u4')!=cu.view('<u4');pixels=upChanged.any(2);subset=upChanged&sameInputs[:,:,None];coords=np.argwhere(subset);first=None
if len(coords):
 y,x,c=map(int,coords[0]);first={'xyChannel':[x,y,c],'homeTc':ht[y,x].tolist(),'companyTc':ct[y,x].tolist(),'homeBrightness':hb[y,x].tolist(),'companyBrightness':cb[y,x].tolist(),'homeUpsample':hu[y,x].tolist(),'companyUpsample':cu[y,x].tolist(),'absDifference':float(abs(float(hu[y,x,c])-float(cu[y,x,c])))}
report={'version':'HomeGrayPropagationReadbackV1','gpuCalls':0,'pixelCount':561,'sameTcPixels':int(sameTc.sum()),'sameBrightnessPixels':int(sameB.sum()),'sameTcAndBrightnessPixels':int(sameInputs.sum()),'upsampleChangedPixels':int(pixels.sum()),'upsampleChangedPixelsWithSameTcBrightness':int((pixels&sameInputs).sum()),'upsampleChangedComponentsWithSameTcBrightness':int(subset.sum()),'maxAbsSameTcBrightnessSubset':float(np.abs(hu.astype(float)-cu.astype(float))[subset].max(initial=0)),'firstSameInputsUpsampleDifference':first,'lutIdentityAuthority':'Full float runtime LUT upload/device SHA256 equal in sealed records; company complete arrays not migrated, direct full-content comparison NOT_PERFORMED.','conclusion':'Earliest paired GPU output difference is tc_b. Not every downstream difference is proven to be propagated from tc_b: same per-pixel tc/b inputs already show LUT output differences. No claim about specific FMA/instruction/driver/hardware cause.'}
with output.open('x',encoding='utf-8') as f:json.dump(report,f,indent=2,allow_nan=False)
print(json.dumps(report,indent=2))
