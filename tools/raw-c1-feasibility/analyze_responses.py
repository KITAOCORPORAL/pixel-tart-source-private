"""Analyze only saved uint16 production preview buffers, within each source.
Display PNGs are illustrative, never primary measurements or C1 equivalence proof.
"""
import argparse,csv,json,pathlib
import numpy as np
from PIL import Image,ImageCms,ImageDraw
from tiff_gate import LabEngine

def analyze(response,output,dll,sha):
    d=json.loads(pathlib.Path(response).read_text());root=pathlib.Path(response).parent;out=pathlib.Path(output);out.mkdir(parents=True,exist_ok=True)
    engine=LabEngine(dll,sha);icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
    # Human reviewed on saved production output. These are scene regions, not calibrated patches.
    rois={'sky_blue':(.56,.06,.78,.17),'light_building':(.62,.47,.71,.61),'building_shadow':(.08,.72,.17,.86)}
    rows=[];decoded=[x for x in d['results'] if x['status']=='decoded'];canvas=Image.new('RGB',(1500,230*len(decoded)),(35,35,35));draw=ImageDraw.Draw(canvas)
    for row,x in enumerate(decoded):
        base=next(c for c in x['cases'] if c['ev']==0);basearr=np.fromfile(root/base['file'],dtype='<u2').reshape(base['height'],base['width'],3).astype(np.float32)/65535
        for col,c in enumerate(x['cases']):
            arr=np.fromfile(root/c['file'],dtype='<u2').reshape(c['height'],c['width'],3).astype(np.float32)/65535
            for name,(x0,y0,x1,y1) in rois.items():
                ys=slice(int(y0*c['height']),int(y1*c['height']));xs=slice(int(x0*c['width']),int(x1*c['width']));v=arr[ys,xs].reshape(-1,3);b=basearr[ys,xs].reshape(-1,3)
                lab=engine.convert(v,icc);lab0=engine.convert(b,icc);mean=lab.mean(axis=0);delta=lab-lab0;ch=np.hypot(lab[:,1],lab[:,2]);hue=np.degrees(np.arctan2(lab[:,2],lab[:,1]));h0=np.degrees(np.arctan2(lab0[:,2],lab0[:,1]));dh=(hue-h0+180)%360-180;reliable=(ch>5)&(np.hypot(lab0[:,1],lab0[:,2])>5)
                rows.append({'sourceId':x['id'],'ev':c['ev'],'region':name,'roi':list(rois[name]),'pixels':len(v),'meanLabD50':mean.tolist(),'medianChroma':float(np.median(ch)),'medianHueChangeDegreesWhenChromaOver5':float(np.median(dh[reliable])) if reliable.any() else None,'p95DeltaE76VsSameSourceEV0':float(np.quantile(np.linalg.norm(delta,axis=1),.95)),'warning':'relative response only; regions are not reference gray/color/skin; exposure intentionally changes Lab'})
            im=Image.fromarray(np.clip(np.rint(arr*255),0,255).astype('uint8'));im.thumbnail((298,200));canvas.paste(im,(col*300,row*230+25));draw.text((col*300+5,row*230+5),x['id']+' EV '+str(c['ev']),fill='white')
    payload={'domain':'720px production preview uint16 output samples','c1Comparisons':0,'independentHoldoutSamples':0,'fitPerformed':False,'pcs':'LCMS relative intent1 BPCoff CIELAB D50','lcmsVersion':engine.version,'regions':rois,'rows':rows}
    (out/'relative-roi-results.json').write_text(json.dumps(payload,indent=2));canvas.save(out/'pv4-exposure-contact.png',icc_profile=icc)
    return payload
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('response');p.add_argument('--output',required=True);p.add_argument('--lcms',required=True);p.add_argument('--lcms-sha',required=True);a=p.parse_args();d=analyze(a.response,a.output,a.lcms,a.lcms_sha);print('measured relative ROI cases',len(d['rows']))
