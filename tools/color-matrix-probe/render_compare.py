import argparse, hashlib, json
from pathlib import Path
import numpy as np
from PIL import Image
from stage_validation import sha,validate_stage,validate_candidate

P=np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]])
I=np.linalg.inv(P)
def display(a,ev):
    rgb=np.asarray(a,dtype=np.float64)*2**ev @ I.T
    y=rgb @ np.array([.2126,.7152,.0722]); mapped=np.where(y<=.75,y,.75+.25*(1-np.exp(-np.maximum(y-.75,0)/.25)));scale=np.divide(mapped,y,out=np.zeros_like(y),where=y>0);rgb*=scale[...,None];d=rgb-mapped[...,None]; c=np.ones_like(y)
    for i in range(3):
        t=d[...,i];c=np.minimum(c,np.divide(1-mapped,t,out=np.ones_like(y),where=t>0));c=np.minimum(c,np.divide(-mapped,t,out=np.ones_like(y),where=t<0))
    x=np.clip(mapped[...,None]+d*c[...,None],0,1);return np.where(x<=.0031308,12.92*x,1.055*np.power(x,1/2.4)-.055)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('stage');ap.add_argument('candidate');ap.add_argument('output');args=ap.parse_args();root=Path(args.stage);out=Path(args.output);m=validate_stage(root);sizes=m['sizes'];w=sizes['IWidth'];h=sizes['IHeight'];cand=Path(args.candidate);validate_candidate(cand,m);out.mkdir(exist_ok=False)
    a=np.memmap(root/'matrix_after.u16',dtype='<u2',mode='r',shape=(h,w,4));b=np.memmap(cand,dtype='<f4',mode='r',shape=(h,w,3)); results={}
    for ev in (0,-2):
        n=0;sumabs=0.;maxabs=0.;preview_a=np.zeros((h,w,3),np.uint8);preview_b=np.zeros_like(preview_a)
        for y in range(0,h,32):
            z=min(y+32,h);aa=display((a[y:z,:,:3].astype(np.float32)/np.float32(65535)).astype(np.float64),ev);bb=display(b[y:z],ev);delta=np.abs(aa-bb);sumabs+=delta.sum();maxabs=max(maxabs,float(delta.max()));n+=delta.size;preview_a[y:z]=np.rint(aa*255).astype(np.uint8);preview_b[y:z]=np.rint(bb*255).astype(np.uint8)
        for label,arr in [('integer',preview_a),('float',preview_b)]:
            img=Image.fromarray(arr,'RGB');img.save(out/f'{label}_ev{ev}_full.png');img.thumbnail((1600,1600));img.save(out/f'{label}_ev{ev}_preview.png')
        for label,x,y in [('negative',1600,2328),('overflow',6637,83),('wb_clipped',6637,83)]:
            l=max(0,x-64);t=max(0,y-64);r=min(w,x+64);d=min(h,y+64)
            for name,arr in [('integer',preview_a),('float',preview_b)]:Image.fromarray(arr[t:d,l:r],'RGB').save(out/f'{label}_{name}_ev{ev}.png')
        results[str(ev)]={'meanAbsoluteDisplayDifference':sumabs/n,'maxAbsoluteDisplayDifference':maxabs,'nChannelSamples':n,'pixelsDifferentIn8bit':int(np.any(preview_a!=preview_b,axis=2).sum())}
    print(json.dumps({'domain':'same product Display mathematical transform, fixed EV and sRGB display encoding; research implementation','ev':results,'candidateSha256':sha(cand),'humanPreference':'NOT_RUN','rawRecovery':'NOT_CLAIMED'},indent=2))
if __name__=='__main__':main()
