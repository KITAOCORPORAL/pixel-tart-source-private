import numpy as np
from pathlib import Path
import sys,json
from PIL import Image
from render_compare import display
from stage_validation import sha,validate_stage,validate_candidate
root=Path(sys.argv[1]);candidate=Path(sys.argv[2]);out=Path(sys.argv[3]);m=validate_stage(root);h=m['sizes']['IHeight'];w=m['sizes']['IWidth'];validate_candidate(candidate,m);out.mkdir(exist_ok=False);a=np.memmap(root/'matrix_after.u16',dtype='<u2',mode='r',shape=(h,w,4));b=np.memmap(candidate,dtype='<f4',mode='r',shape=(h,w,3));x,y=6637,83;results={}
for ev in [0,-2,-4]:
    lo=(a[y,x,:3].astype(np.float32)/np.float32(65535)).astype(float);hi=b[y,x].astype(float); da=display(lo,ev);db=display(hi,ev);results[str(ev)]={'integer_linear':(lo*2**ev).tolist(),'float_linear':(hi*2**ev).tolist(),'display_integer':da.tolist(),'display_float':db.tolist()}
for label,x,y in [('negative',1600,2328),('overflow',6637,83),('wb_clipped',6637,83)]:
    left=max(x-64,0);top=max(y-64,0);right=min(x+64,w);bottom=min(y+64,h)
    for ev in [0,-2]:
        for name,img in [('A',(a[top:bottom,left:right,:3].astype(np.float32)/np.float32(65535)).astype(float)),('B',b[top:bottom,left:right])]:
            arr=np.rint(display(img,ev)*255).astype(np.uint8);Image.fromarray(arr).save(out/f'{label}_{name}_EV{ev}.png')
print(json.dumps({'coordinate':[6637,83],'observations':results,'rawRecovery':'NOT_CLAIMED','candidateHash':sha(candidate)},indent=2))
