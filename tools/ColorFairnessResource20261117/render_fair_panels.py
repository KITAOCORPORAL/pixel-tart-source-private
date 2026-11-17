from pathlib import Path
import json,sys,numpy as np,hashlib
from PIL import Image,ImageDraw,ImageFont
root=Path(sys.argv[1]);out=root/'fair-exact';r=json.loads((out/'RESULTS.json').read_text());plan=json.loads((root/'FROZEN_PLAN.json').read_text());original=Path('E:/Guangjing-Portrait-Highlight-Review-20261112');panels=out/'panels';panels.mkdir(exist_ok=False)
def D(v):
 v=np.asarray(v,np.float64);return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
def Y(v):return v@np.array([.2126,.7152,.0722])
def read(f):return np.fromfile(f,dtype='<f4').reshape(384,384,3)
def metric(a,s,ev):
 l=D(a);y=Y(l);x=D(s)*2**ev;m=x.max(-1)>.75;dx=np.diff(y,axis=1);dy=np.diff(y,axis=0);mx=m[:,1:]&m[:,:-1];my=m[1:]&m[:-1];n=int(mx.sum()+my.sum());rms=float(np.sqrt(((dx[mx]**2).sum()+(dy[my]**2).sum())/n)) if n else None;mean=float(y[m].mean()) if m.any() else None
 return {'highMeanY':mean,'relativeHighRms':rms/mean if rms is not None and mean>0 else None,'absoluteHighRms':rms,'highQ99':float(np.percentile(y[m],99)) if m.any() else None,'highMaxY':float(y[m].max()) if m.any() else None,'channelsAtOne':int((a>=np.float32(1)).sum()),'inputClipChannels':int((s>=1).sum()),'fullMeanY':float(y.mean()),'rgbF32Sha256':hashlib.sha256(a.tobytes()).hexdigest()}
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14);allrows=[]
for case in r['cases']:
 item=next(x for x in plan['inputs'] if x['id']==case['id']);source=read(original/'fixed-inputs'/item['file']);legacy=read(original/'roi-comparison02'/(case['id']+'-E'+str(case['ev'])+'-same')/'output.f32');candidateSame=read(case['candidateSamePath']);candidateMid=read(case['candidateMidPath']) if case['candidateMidPath'] else None
 base=metric(legacy,source,case['ev']);same=metric(candidateSame,source,case['ev']);mid=metric(candidateMid,source,case['ev']) if candidateMid is not None else None;profile=original/'fixed-inputs'/(item['source']+'.icc');profile=profile if profile.exists() else original/'fixed-inputs/company-02.icc'
 for control in case['controls']:
  for condition,key,pathkey in [('sameEV','same','sameFile'),('midtone','mid','file'),('mid+high','joint','file')]:
   cmeta=control[key];candidate=candidateSame if condition=='sameEV' else candidateMid;target=same if condition=='sameEV' else mid
   p=control['sameFile'] if condition=='sameEV' else cmeta.get('file');array=read(p) if p else None;cm=metric(array,source,case['ev']) if array is not None else None;status='SAME_EV_FIXED' if condition=='sameEV' else cmeta['status'];fullerr=None
   if condition=='mid+high' and cm and target:
    fullerr=abs(cm['highMeanY']-target['highMeanY'])/target['highMeanY'];status='JOINT_MATCHED' if fullerr<=.005 and cmeta['midError']<=2e-5 else 'FULL_ROI_HIGH_MISMATCH'
   panel=Image.new('RGB',(1152,450),'#f0f0f0');draw=ImageDraw.Draw(panel)
   for i,(title,a) in enumerate([('Legacy EV '+str(case['ev']),legacy),('Fixed V1 '+condition,candidate),(control['method']+' '+status,array if status not in ['FULL_ROI_HIGH_MISMATCH'] else None)]):
    draw.text((i*384+4,3),title,font=font,fill='black')
    im=Image.fromarray(np.rint(np.clip(a,0,1)*255).astype('uint8')) if a is not None else Image.new('RGB',(384,384),'#777777');panel.paste(im,(i*384,28))
   param='same E='+str(case['ev'])+' S='+str(control['fixedStrength']) if condition=='sameEV' else 'E='+str(cmeta.get('e'))+' S='+str(cmeta.get('s'))+' miderr='+str(cmeta.get('midError'))
   draw.text((4,416),case['id']+' | '+condition+' | '+param,font=font,fill='black');draw.text((4,433),'Full highY rel.error='+str(fullerr)+' | no post-output normalization | '+case['kind'],font=font,fill='black')
   name=case['id']+'-E'+str(case['ev'])+'-'+control['method']+'-'+condition.replace('+','_')+'.png';panel.save(panels/name,icc_profile=profile.read_bytes())
   allrows.append({'id':case['id'],'kind':case['kind'],'ev':case['ev'],'method':control['method'],'condition':condition,'status':status,'candidate':target,'control':cm,'legacy':base,'parameters':{'exposure':case['ev'] if condition=='sameEV' else cmeta.get('e'),'strength':control['fixedStrength'] if condition=='sameEV' else cmeta.get('s')},'anchorMidError':None if condition=='sameEV' else cmeta.get('midError'),'sampleHighRelativeError':cmeta.get('highRelativeError') if condition=='mid+high' else None,'fullHighRelativeError':fullerr,'panel':'panels/'+name,'panelSha256':hashlib.sha256((panels/name).read_bytes()).hexdigest(),'array':p})
(out/'FULL_ROI_METRICS.json').write_text(json.dumps({'cases':len(r['cases']),'controlConditions':len(allrows),'rows':allrows,'newCandidateRuns':0,'newRawDecodes':0,'curvesAndControlsAreExactProductClasses':True},indent=2)+chr(10),encoding='utf-8')
print(json.dumps({'panels':len(allrows),'jointPass':sum(x['status']=='JOINT_MATCHED' for x in allrows),'fullMismatch':sum(x['status']=='FULL_ROI_HIGH_MISMATCH' for x in allrows)}))
