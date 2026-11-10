"""Streaming exact RGB-U16 comparison; no threshold and no decoder calls."""
from pathlib import Path
import hashlib
import numpy as np

def compare(a,b,width,height,expected_a=None,expected_b=None):
 a,b=Path(a),Path(b);expected=width*height*6
 if a.stat().st_size!=expected or b.stat().st_size!=expected:raise ValueError('Exact P6 RGB16 length required')
 ha,hb=hashlib.sha256(),hashlib.sha256();first=maximum=bounds=None;maxvalue=0
 regions={n:dict(pixels=0,changedChannels=0,changedPixels=0,sumAbsU16=0,maxU16=0) for n in ['whole','outerBorder8','tileSeamBand8ExcludingBorder','interiorNonSeam']}
 x=np.arange(width)[None,:];sx=np.zeros((1,width),dtype=bool)
 for seam in range(507,width,496):sx|=(x>=seam-8)&(x<seam+8)
 with a.open('rb') as fa,b.open('rb') as fb:
  for y0 in range(0,height,32):
   rows=min(32,height-y0);n=rows*width*6;ba,bb=fa.read(n),fb.read(n)
   if len(ba)!=n or len(bb)!=n:raise ValueError('Truncated stage data')
   ha.update(ba);hb.update(bb);va=np.frombuffer(ba,dtype='<u2').reshape(rows,width,3);vb=np.frombuffer(bb,dtype='<u2').reshape(rows,width,3);delta=vb.astype(np.int32)-va.astype(np.int32);d=np.abs(delta);changed=d!=0;pixel=changed.any(axis=2)
   y=np.arange(y0,y0+rows)[:,None];outer=np.broadcast_to((x<8)|(x>=width-8)|(y<8)|(y>=height-8),(rows,width));sy=np.zeros((rows,1),dtype=bool)
   for seam in range(507,height,496):sy|=(y>=seam-8)&(y<seam+8)
   seamMask=(sx|sy)&~outer;masks={'whole':np.ones((rows,width),dtype=bool),'outerBorder8':outer,'tileSeamBand8ExcludingBorder':seamMask,'interiorNonSeam':~outer&~seamMask}
   for name,mask in masks.items():
    r=regions[name];values=d[mask];r['pixels']+=int(mask.sum());r['changedChannels']+=int(changed[mask].sum());r['changedPixels']+=int(pixel[mask].sum());r['sumAbsU16']+=int(values.sum(dtype=np.uint64));r['maxU16']=max(r['maxU16'],int(values.max(initial=0)))
   if pixel.any():
    yy,xx=np.nonzero(pixel);current=dict(left=int(xx.min()),right=int(xx.max()),top=y0+int(yy.min()),bottom=y0+int(yy.max()))
    if bounds is None:bounds=current
    else:
     for k in ['left','top']:bounds[k]=min(bounds[k],current[k])
     for k in ['right','bottom']:bounds[k]=max(bounds[k],current[k])
    def coord(index):
     ry,rx,c=np.unravel_index(index,d.shape);return dict(x=int(rx),y=y0+int(ry),channel=int(c),first=int(va[ry,rx,c]),second=int(vb[ry,rx,c]),signedDelta=int(delta[ry,rx,c]))
    if first is None:first=coord(int(np.flatnonzero(changed)[0]))
    localmax=int(d.max())
    if localmax>maxvalue:maxvalue=localmax;maximum=coord(int(d.argmax()))
 firstHash,secondHash=ha.hexdigest(),hb.hexdigest()
 if expected_a and firstHash.lower()!=expected_a.lower():raise ValueError('First payload SHA mismatch')
 if expected_b and secondHash.lower()!=expected_b.lower():raise ValueError('Second payload SHA mismatch')
 for r in regions.values():r['meanAbsU16']=r['sumAbsU16']/(r['pixels']*3) if r['pixels'] else None
 assert sum(regions[k]['pixels'] for k in ['outerBorder8','tileSeamBand8ExcludingBorder','interiorNonSeam'])==width*height
 return dict(width=width,height=height,channels=3,firstSha256=firstHash,secondSha256=secondHash,bitExact=regions['whole']['changedChannels']==0,firstDifference=first,maxDifference=maximum,bounds=bounds,regions=regions,borderDefinition='outer8 pixels',seamDefinition='[seam-8,seam+8), seam507+496*k; union of row/column bands excluding outer8')
