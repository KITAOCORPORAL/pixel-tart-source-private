from pathlib import Path
import argparse,json,hashlib,numpy as np
from rgb16_io import tiff_info,read_roi,encode
p=argparse.ArgumentParser();p.add_argument('root',type=Path);a=p.parse_args();root=a.root;out=root/'fixed-inputs';out.mkdir(exist_ok=False)
sources={r['id']:r for r in json.loads((root/'COMPANY_INPUTS.json').read_text('utf-8'))}
rois=[dict(id='skin-shoulder',source='company-02',roi=[2000,1750,384,384],subject='skin shoulder highlight',kind='authorized-stored-photo'),dict(id='skin-hair-edge',source='company-02',roi=[2450,1700,384,384],subject='skin / hair highlight boundary',kind='authorized-stored-photo'),dict(id='warm-lit-texture',source='company-02',roi=[2300,500,384,384],subject='warm illuminated textured background; lamp spectrum not measured',kind='authorized-stored-photo'),dict(id='sunset-gradient',source='company-05',roi=[300,1600,384,384],subject='warm sunset gradient control, not a warm lamp or metal reference',kind='authorized-stored-photo')]
anchors={}
for key,source in sources.items():(out/(key+'.icc')).write_bytes(tiff_info(source['path'])['icc'])
for key,box in [('company-02',[3300,2500,384,384]),('company-05',[900,900,384,384])]:
 info=tiff_info(sources[key]['path']);v=read_roi(sources[key]['path'],info,box).astype(np.float32)/np.float32(65535);v.tofile(out/(key+'-midtone.f32'));anchors[key]=dict(roi=box,file=key+'-midtone.f32',shape=[384,384,3],rule='Fixed input luminance mask 0.03 <= Y <= 0.35, minimum16 pixels; mean output linear Y matched by candidate EV only')
for r in rois:
 source=sources[r['source']];info=tiff_info(source['path']);pixels=read_roi(source['path'],info,r['roi']);encoded=pixels.astype(np.float32)/np.float32(65535);file=out/(r['id']+'.f32');encoded.tofile(file)
 r.update(sourceSha256=source['sha256'],originalRawSha256=source['rawSha256'],inputIccSha256=info['iccSha256'],file=file.name,width=r['roi'][2],height=r['roi'][3],pixelU16Sha256=hashlib.sha256(pixels.tobytes()).hexdigest(),encodedF32Sha256=hashlib.sha256(encoded.tobytes()).hexdigest(),anchor=r['source'],domain='Already rendered encoded sRGB16 D65; not sensor-linear',parameters={k:value for k,value in json.loads(Path(source['path']).parent.parent.joinpath('default-output.json').read_text('utf-8-sig')).items() if k not in ['path']})
# Synthetic supplements are neutral texture and a coloured specular-like gradient;
# neither is a physical fabric/metal photographic validation.
y,x=np.mgrid[0:384,0:384];u=x/383;v=y/383
neutral=np.clip(.50+.45*u+.025*np.sin(2*np.pi*v*12)+.006*np.sin(2*np.pi*u*45),0,1);neutral=np.repeat(neutral[:,:,None],3,axis=2)
spec=.02+.98*np.exp(-((u-.55)**2/.012+(v-.46)**2/.028));base=np.stack([.12+.3*u,.06+.16*u,.02+.04*u],axis=-1);coloured=np.minimum(1,base+spec[:,:,None]*np.array([1,.93,.82]))
for name,image,subject in [('synthetic-neutral-texture',neutral,'synthetic neutral bright texture; no real white garment'),('synthetic-coloured-specular',coloured,'synthetic coloured specular-like mixture; no verified metal material')]:
 encoded=encode(image).astype('<f4');file=out/(name+'.f32');encoded.tofile(file);anchor=encode(np.full((384,384,3),.18)).astype('<f4');anchor.tofile(out/(name+'-midtone.f32'));anchors[name]=dict(roi=[0,0,384,384],file=name+'-midtone.f32',shape=[384,384,3],rule='Independent neutral 18% linear sRGB anchor')
 rois.append(dict(id=name,source=name,roi=[0,0,384,384],subject=subject,kind='synthetic-not-photographic-acceptance',sourceSha256=hashlib.sha256(encoded.tobytes()).hexdigest(),file=file.name,width=384,height=384,encodedF32Sha256=hashlib.sha256(encoded.tobytes()).hexdigest(),anchor=name,domain='Generated display-linear sRGB then encoded; floating-point input, not camera calibration',parameters=dict(generator='NeutralTextureOrColouredSpecularV1',width=384,height=384)))
metadata=dict(version='PortraitReviewFixedRoisV1',inputs=rois,anchors=anchors,newRawDecodes=0,selection='Fixed from input-only previews before candidate comparison; no ROI resampling',missing=dict(homeDSC08015=True,realWhiteGarmentHighlights=True,verifiedMetalSpecularHighlights=True,calibratedWarmLampSpectrum=True))
(root/'FIXED_ROIS.json').write_bytes((json.dumps(metadata,indent=2)+'\n').encode());print(json.dumps(dict(photoRois=4,syntheticSupplements=2,missing=metadata['missing']),indent=2))
