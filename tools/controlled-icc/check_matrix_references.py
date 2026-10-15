import pathlib,ctypes as C,subprocess,json,sys,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/controlled-icc';old=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc');L=C.CDLL(str(old/'lcms/Library/bin/lcms2.dll'));P=C.c_void_p;U=C.c_uint;F=C.c_float;D=C.c_double
def fn(n,r,a):f=getattr(L,n);f.restype=r;f.argtypes=a;return f
op=fn('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]);xyz=fn('cmsCreateXYZProfile',P,[]);tr=fn('cmsCreateTransform',P,[P,U,P,U,U,U]);do=fn('cmsDoTransform',None,[P,P,P,U]);cl=fn('cmsCloseProfile',C.c_int,[P]);dt=fn('cmsDeleteTransform',None,[P]);pts=json.loads((R/'docs/evidence/company-validation-20261015/icc/COMPARISON.json').read_text())['points'];pts=[p for p in pts if all(0<=x<=1 for x in p)];rows=[]
for n in ['linear-srgb.icc','linear-prophoto.icc']:
 p=A/n;a=op(str(p).encode(),b'r');b=xyz();t=tr(a,(1<<22)|(4<<16)|(3<<3)|4,b,(1<<22)|(9<<16)|(3<<3),1,0x144);assert t;values=[]
 for pt in pts:
  x=(F*3)(*pt);y=(D*3)();do(t,x,y,1);values.append(list(y))
 dt(t);cl(a);cl(b);pr=subprocess.run([str(old/'skcms_probe.exe'),str(p),'1','f'],input='\n'.join(' '.join(map(str,p)) for p in pts)+'\n',text=True,capture_output=True,timeout=30);sk=[[float(x) for x in l.split()] for l in pr.stdout.splitlines()];assert pr.returncode==0 and len(sk)==len(pts)
 delta=max(abs(a-b) for l,r in zip(values,sk) for a,b in zip(l,r));rows.append(dict(profile=n,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),points=pts,lcms=values,skcms=sk,maxAbs=delta,scope='linear-matrix relative XYZD50 only',skcmsCommit='97adcbb67e98d633a65b29fd405941970f3b476d'));assert delta<1e-6
print(json.dumps(rows,indent=2))
