"""Create a row-streamed RGB16 TIFF at the R3 storage dimensions; never decode RAW."""
from pathlib import Path
import argparse, hashlib, json, struct
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[2]
out=root/'artifacts/tiff-memory-20261115'; out.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('--profile-from',type=Path,default=root.parent/'kitao-instant-film-evidence-v3-20261016/.validation/instant-v3/visual-review-final/sony-dsc08015-isolated-frozen.tif');args=parser.parse_args();home=args.profile_from.resolve()
with Image.open(home) as im:
    profile=im.info['icc_profile'];assert hashlib.sha256(profile).hexdigest()=='2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e', 'Use the frozen sRGB ICC identity for an exact stress-input replay'; home_info={'path':str(home),'storedSize':[int(im.tag_v2[256]),int(im.tag_v2[257])],'orientation':int(im.tag_v2.get(274,1))}
w,h=7752,5184
path=out/'synthetic-rgb16-7752x5184.tif'
if not path.exists():
    count=12; extra=8+2+12*count+4; bits_at=extra; icc_at=bits_at+6; pixels_at=(icc_at+len(profile)+3)&~3
    tags=[(256,4,1,w),(257,4,1,h),(258,3,3,bits_at),(259,3,1,1),(262,3,1,2),(273,4,1,pixels_at),(274,3,1,1),(277,3,1,3),(278,4,1,h),(279,4,1,w*h*6),(284,3,1,1),(34675,7,len(profile),icc_at)]
    with path.open('xb') as f:
        f.write(b'II'+struct.pack('<HIH',42,8,count))
        for tag,kind,n,val in tags:f.write(struct.pack('<HHII',tag,kind,n,val))
        f.write(struct.pack('<I3H',0,16,16,16)); f.write(profile); f.write(b'\0'*(pixels_at-f.tell()))
        x=np.arange(w,dtype=np.uint32);row=np.empty((w,3),dtype='<u2')
        for y in range(h):
            row[:,0]=(x*17+y*5)%65536;row[:,1]=(x*7+y*31)%65536;row[:,2]=(x*43+y*11)%65536
            f.write(row.tobytes())
with path.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest().upper()
task={'task':'HOME_SYNTHETIC_RGB16_R3_SIZE','inputKind':'already-rendered-tiff','inputPath':str(path),'fileSha256':digest,'maximumNewRawDecodes':0,'guardBytes':2**31,'width':w,'height':h,'bitsPerChannel':16,'orientation':1,'iccSha256':hashlib.sha256(profile).hexdigest(),'inputDifference':'Company DSCF0347 unavailable. Synthetic RGB16 uses identical 7752x5184 storage dimensions, deterministic channel ramps and the existing authorized home TIFF sRGB ICC. Scene content, compression and camera history differ.','authorizedHomeTiff':home_info}
(out/'INPUT_TASK.json').write_text(json.dumps(task,indent=2)+'\n',encoding='utf-8');print(json.dumps(task))
