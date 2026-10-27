"""Read-only TIFF gates and same-source relative Lab differences via existing LCMS.
No image is downconverted for measurement. Uncompressed chunky RGB uint16 only;
unsupported encodings are refused, never silently measured as 8 bit.
"""
import argparse, ctypes as C, hashlib, json, pathlib
import numpy as np
from PIL import Image

def digest(p):
    with open(p,'rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def inspect(path, expected_icc=None):
    with Image.open(path) as im:
        t=im.tag_v2
        bits=tuple(t.get(258,())); sample=t.get(339,(1,1,1)); sample=(sample,) if isinstance(sample,int) else tuple(sample)
        profile=im.info.get('icc_profile',b'')
        v={'path':str(pathlib.Path(path).resolve()),'width':im.width,'height':im.height,'bits':bits,'compression':t.get(259),'samples':t.get(277),'planar':t.get(284,1),'photometric':t.get(262),'orientation':t.get(274,1),'sample_format':sample,'icc_bytes':len(profile),'icc_sha256':hashlib.sha256(profile).hexdigest() if profile else None,'file_bytes':pathlib.Path(path).stat().st_size}
        if bits!=(16,16,16) or v['samples']!=3 or v['planar']!=1 or v['photometric']!=2 or any(x!=1 for x in sample): raise ValueError('Require unsigned16 chunky RGB TIFF; no 8-bit fallback')
        if v['orientation']!=1: raise ValueError('Normalize orientation explicitly before comparison; no implicit rotation')
        if not profile: raise ValueError('Missing embedded ICC')
        if expected_icc and v['icc_sha256'].lower()!=expected_icc.lower():raise ValueError('Output ICC content differs from frozen recipe')
        return v,profile

def roi_rgb16(path,roi):
    with Image.open(path) as im:
        t=im.tag_v2;w,h=im.size
        if t.get(259)!=1:raise ValueError('This probe requires explicitly uncompressed TIFF; no lossy/8-bit fallback')
        x,y,r,b=roi
        if not 0<=x<r<=1 or not 0<=y<b<=1:raise ValueError('ROI must be normalized x0,y0,x1,y1')
        x0,y0,x1,y1=int(x*w),int(y*h),int(r*w),int(b*h)
        if x0==x1 or y0==y1:raise ValueError('Empty ROI')
        offsets=t[273];counts=t[279];rows=int(t.get(278,h)); offsets=(offsets,) if isinstance(offsets,int) else offsets;counts=(counts,) if isinstance(counts,int) else counts
        with open(path,'rb') as f:
            magic=f.read(2);endian='<' if magic==b'II' else '>' if magic==b'MM' else None
            if endian is None:raise ValueError('Not TIFF')
            lines=[]
            for line in range(y0,y1):
                strip=line//rows;within=line%rows;skip=within*w*6+x0*6;needed=(x1-x0)*6
                if strip>=len(offsets) or skip+needed>counts[strip]:raise ValueError('Truncated strip')
                f.seek(offsets[strip]+skip);raw=f.read(needed)
                if len(raw)!=needed:raise ValueError('Truncated pixels')
                lines.append(np.frombuffer(raw,dtype=endian+'u2').reshape(-1,3).astype(np.float32)/65535)
        return np.concatenate(lines)

class LabEngine:
    def __init__(self,dll,expected_sha):
        if digest(dll).lower()!=expected_sha.lower():raise ValueError('LCMS binary SHA mismatch')
        self.l=C.CDLL(str(pathlib.Path(dll).resolve()));l=self.l;P=C.c_void_p;U=C.c_uint
        for name,args,res in [('cmsGetEncodedCMMversion',[],U),('cmsOpenProfileFromMem',[P,U],P),('cmsCreateLab4Profile',[P],P),('cmsCreateTransform',[P,U,P,U,U,U],P),('cmsDoTransform',[P,P,P,U],None),('cmsDeleteTransform',[P],None),('cmsCloseProfile',[P],C.c_int)]:
            f=getattr(l,name);f.argtypes=args;f.restype=res
        self.version=l.cmsGetEncodedCMMversion()
    def convert(self,rgb,profile):
        l=self.l;buf=C.create_string_buffer(profile);a=l.cmsOpenProfileFromMem(buf,len(profile));b=l.cmsCreateLab4Profile(None);t=None
        try:
            if not a or not b:raise ValueError('LCMS refused profile')
            t=l.cmsCreateTransform(a,(1<<22)|(4<<16)|(3<<3)|4,b,(1<<22)|(10<<16)|(3<<3)|4,1,0x140)
            if not t:raise ValueError('LCMS refused float RGB->Lab transform')
            src=np.ascontiguousarray(rgb,dtype=np.float32);out=np.empty_like(src)
            l.cmsDoTransform(t,src.ctypes.data,out.ctypes.data,len(src))
            if not np.isfinite(out).all():raise ValueError('Nonfinite Lab')
            return out
        finally:
            if t:l.cmsDeleteTransform(t)
            if a:l.cmsCloseProfile(a)
            if b:l.cmsCloseProfile(b)

def compare(left,right,roi,engine,source_sha,geometry_confirmed,icc_sha=None):
    if not geometry_confirmed:raise ValueError('Alignment/crop/lens/orientation must be explicitly verified')
    if not source_sha or source_sha=='0'*64 or len(source_sha)!=64 or any(c not in '0123456789abcdefABCDEF' for c in source_sha):raise ValueError('Same RAW source SHA256 identity required')
    a,pa=inspect(left,icc_sha);b,pb=inspect(right,icc_sha)
    if (a['width'],a['height'])!=(b['width'],b['height']):raise ValueError('Size mismatch: align/crop explicitly first')
    x=engine.convert(roi_rgb16(left,roi),pa);y=engine.convert(roi_rgb16(right,roi),pb);delta=np.linalg.norm(y-x,axis=1)
    return {'left':a,'right':b,'source_sha256_asserted_by_operator':source_sha,'roi':roi,'pixels':len(x),'pcs':'CIELAB D50 via LCMS relative colorimetric intent1 BPCoff','metric':'DeltaE76 difference between outputs, NOT real-color error','median_deltaE76':float(np.median(delta)),'p95_deltaE76':float(np.quantile(delta,.95)),'mean_deltaLab':(y-x).mean(axis=0).tolist(),'lcms_version':engine.version,'fitPerformed':False}

def main():
    p=argparse.ArgumentParser();p.add_argument('left');p.add_argument('--right');p.add_argument('--roi',type=float,nargs=4,default=[.2,.2,.3,.3]);p.add_argument('--lcms');p.add_argument('--lcms-sha');p.add_argument('--source-sha');p.add_argument('--geometry-confirmed',action='store_true');p.add_argument('--icc-sha');p.add_argument('--output',required=True);a=p.parse_args()
    result=compare(a.left,a.right,a.roi,LabEngine(a.lcms,a.lcms_sha),a.source_sha,a.geometry_confirmed,a.icc_sha) if a.right else inspect(a.left,a.icc_sha)[0]
    target=pathlib.Path(a.output).resolve()
    if target in [pathlib.Path(a.left).resolve()]+([pathlib.Path(a.right).resolve()] if a.right else []):raise ValueError('Refuse source overwrite')
    target.parent.mkdir(parents=True,exist_ok=True)
    with target.open('x',encoding='utf-8') as f:json.dump(result,f,indent=2)
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()
