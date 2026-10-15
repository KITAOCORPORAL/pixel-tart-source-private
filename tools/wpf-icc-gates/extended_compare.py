import pathlib,ctypes as C,subprocess,json
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-icc-gates';L=C.CDLL(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');P=C.c_void_p;U=C.c_uint;F=C.c_float
for n,r,a in [('cmsOpenProfileFromFile',P,[C.c_char_p,C.c_char_p]),('cmsCloseProfile',C.c_int,[P]),('cmsCreateTransform',P,[P,U,P,U,U,U]),('cmsDoTransform',None,[P,P,P,U]),('cmsDeleteTransform',None,[P])]:f=getattr(L,n);f.restype=r;f.argtypes=a
pts=[[-.2,-.2,-.2],[0,0,0],[.18,.18,.18],[1,1,1],[1.2,1.2,1.2],[4,2,.5],[1.5,-.1,.2],[.65,.35,.22],[.8,.45,.3],[.95,.9,.8],[0,1,0],[1,0,1]];profiles=[R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc',R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc'];rows=[];fmt=(1<<22)|(4<<16)|(3<<3)|4
for src,dst in [(profiles[0],profiles[1]),(profiles[1],profiles[0]),(profiles[0],profiles[0])]:
 s=L.cmsOpenProfileFromFile(str(src).encode(),b'r');d=L.cmsOpenProfileFromFile(str(dst).encode(),b'r');tf=L.cmsCreateTransform(s,fmt,d,fmt,1,0x144);tu=L.cmsCreateTransform(s,fmt,d,(4<<16)|(3<<3)|2,1,0x144);assert tf and tu
 proc=subprocess.run([str(A/'extended_refs.exe'),str(src),str(dst)],input='\n'.join(' '.join(map(str,p)) for p in pts)+'\n',text=True,capture_output=True,timeout=30);assert proc.returncode==0;sk=[[float(v) for v in l.split()] for l in proc.stdout.splitlines()];items=[]
 for p,other in zip(pts,sk):
  x=(F*3)(*p);y=(F*3)();u=(C.c_ushort*3)();L.cmsDoTransform(tf,x,y,1);L.cmsDoTransform(tu,x,u,1);items.append(dict(input=p,lcmsFloat=list(y),skcmsFloat=other[:3],lcmsU16=list(u),skcmsU16=other[3:],maxAbs=max(abs(a-b) for a,b in zip(y,other))))
 L.cmsDeleteTransform(tf);L.cmsDeleteTransform(tu);L.cmsCloseProfile(s);L.cmsCloseProfile(d);rows.append(dict(source=src.name,target=dst.name,intent=1,bpc=False,adaptation=1,values=items))
print(json.dumps(dict(lcmsCommit='453bafeb85b4ef96498866b7a8eadcc74dff9223',skcmsCommit='97adcbb67e98d633a65b29fd405941970f3b476d',rows=rows,warning='integer clipping is not perceptual gamut mapping; float preservation/encoding differences must remainexplicit'),indent=2))
