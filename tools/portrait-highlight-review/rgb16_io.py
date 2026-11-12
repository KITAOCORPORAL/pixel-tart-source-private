"""Read only contiguous uncompressed RGB16 TIFFs; no RAW decoder or full-frame clone."""
from pathlib import Path
import hashlib,struct,numpy as np

def sha(path):
 with Path(path).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def tiff_info(path):
 path=Path(path)
 with path.open('rb') as f:
  order=f.read(2);assert order in (b'II',b'MM');endian='<' if order==b'II' else '>'
  assert struct.unpack(endian+'H',f.read(2))[0]==42,'Only classic TIFF admitted'
  ifd=struct.unpack(endian+'I',f.read(4))[0];f.seek(ifd);count=struct.unpack(endian+'H',f.read(2))[0]
  raw=[f.read(12) for _ in range(count)];tags={};types={1:1,2:1,3:2,4:4,5:8,7:1,9:4,10:8,11:4,12:8}
  for record in raw:
   tag,kind,n=struct.unpack(endian+'HHI',record[:8]);size=types.get(kind,0)*n
   if not size:continue
   if size<=4:data=record[8:8+size]
   else:
    f.seek(struct.unpack(endian+'I',record[8:])[0]);data=f.read(size);assert len(data)==size
   if kind==3:tags[tag]=list(struct.unpack(endian+str(n)+'H',data))
   elif kind==4:tags[tag]=list(struct.unpack(endian+str(n)+'I',data))
   else:tags[tag]=data
 width=tags[256][0];height=tags[257][0]
 assert tags[258]==[16,16,16] and tags[259]==[1] and tags[262]==[2] and tags[277]==[3]
 assert tags.get(284,[1])==[1] and len(tags[273])==len(tags[279])==1
 assert tags[279][0]==width*height*6 and tags[273][0]+tags[279][0]<=path.stat().st_size
 orientation=tags.get(274,[1])[0];assert orientation in (1,8)
 icc=tags.get(34675,b'')
 return dict(width=width,height=height,orientation=orientation,offset=tags[273][0],dtype=endian+'u2',icc=icc,iccSha256=hashlib.sha256(icc).hexdigest(),bytes=path.stat().st_size)

def pixels(path,info):
 mapped=np.memmap(path,mode='r',dtype=info['dtype'],offset=info['offset'],shape=(info['height'],info['width'],3))
 return np.rot90(mapped,1) if info['orientation']==8 else mapped

def read_roi(path,info,roi):
 x,y,w,h=roi;assert 0<w<=512 and 0<h<=512
 image=pixels(path,info);assert x>=0 and y>=0 and x+w<=image.shape[1] and y+h<=image.shape[0]
 result=np.asarray(image[y:y+h,x:x+w],dtype='<u2').copy();del image;return result

def decode(values):return np.where(values<=.04045,values/12.92,((np.maximum(values,0)+.055)/1.055)**2.4)
def encode(values):return np.where(values<=.0031308,values*12.92,1.055*np.maximum(values,0)**(1/2.4)-.055)
def y(values):return values@np.array([.2126,.7152,.0722])
