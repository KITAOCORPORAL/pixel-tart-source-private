"""Freeze authorized RGB16 ROI without decoding RAW or allocating a full-frame raster."""
import argparse,hashlib,json
from pathlib import Path
import numpy as np,tifffile
from PIL import Image
EXPECTED='5c06a14160c9b57015cfebd9cd1e882620ffc817cfdca061596746cdd82d1ca7'
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def freeze(source,root):
 root.mkdir(exist_ok=True,parents=True)
 assert sha(source)==EXPECTED,'Authorized frozen source hash mismatch'
 with tifffile.TiffFile(source) as t:
  p=t.pages[0]
  assert p.shape==(4688,7028,3) and p.dtype==np.dtype('uint16') and p.compression==1 and len(p.dataoffsets)==1 and p.tags[274].value==8 and p.is_contiguous and t.byteorder=='<'
  icc=p.tags[34675].value
  (root/'source-srgb.icc').write_bytes(icc)
  # EXIF8: output(x,y) corresponds to stored (width-1-y,x), no resampling.
  box=(2400,3250,384,384);x,y,w,h=box
  def read():
   mapped=np.memmap(source,mode='r',dtype='<u2',offset=p.dataoffsets[0],shape=p.shape)
   crop=np.rot90(mapped,1)[y:y+h,x:x+w].copy();del mapped
   return crop
  first=read();second=read();assert np.array_equal(first,second)
  tifffile.imwrite(root/'portrait-crop.rgb16.tif',first,photometric='rgb',iccprofile=icc,metadata=None)
  assert np.array_equal(tifffile.imread(root/'portrait-crop.rgb16.tif'),first)
  # Reproduce production U16 / 65535f rounding, not double normalization.
  encoded=first.astype(np.float32)/np.float32(65535);encoded.tofile(root/'portrait-encoded.f32')
  Image.fromarray(np.rint(encoded*255).astype('uint8')).save(root/'portrait-crop.png')
  meta=dict(version='PortraitHighlightFrozenInputV1',authorization='Previously authorized DSC08015 derivative; original binding retained from instant-v3 review-inputs.json, no new RAW read/decode',source=str(source.resolve()),sourceSha256=EXPECTED,originalRawSha256='1fe22be78c66ab0ddccf753d10187f142b980146fff7f5d188533905b7f38b93',sourceStorageSize=[7028,4688],sourceOrientation=8,orientedSize=[4688,7028],cropXYWH=list(box),cropOrientation=1,domain='Already rendered sRGB16 D65, WB/camera matrix/display shoulder/gamut/OETF already applied; NOT sensor-linear',iccSha256=hashlib.sha256(icc).hexdigest(),cropFileSha256=sha(root/'portrait-crop.rgb16.tif'),cropPixelSha256=hashlib.sha256(first.tobytes()).hexdigest(),encodedFloatSha256=sha(root/'portrait-encoded.f32'),repeatReadExact=True,tiffReadbackExact=True,newRawDecodes=0,maxCropBytes=384*384*12,maxFullFrameCloneBytes=0,hashReadBlockBytes=1048576)
 (root/'input-manifest.json').write_text(json.dumps(meta,indent=2));return meta
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--local-root',type=Path,required=True);a=p.parse_args();print(json.dumps(freeze(a.source,a.local_root),indent=2))
