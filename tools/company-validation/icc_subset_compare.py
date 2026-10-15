"""Reproduce the declared 7-tag subset profile and compare actual C# reader with LCMS."""
import runpy,ctypes as C,struct,json,hashlib,subprocess,pathlib
v=runpy.run_path(str(pathlib.Path(__file__).with_name('icc_compare.py')));out=v['out'];P=v['P'];b=bytearray(332)
def U(o,x):struct.pack_into('>I',b,o,x)
def S(o,s):b[o:o+4]=s.encode()
def F(o,x):struct.pack_into('>i',b,o,round(x*65536))
U(0,len(b));b[8]=2;S(12,'mntr');S(16,'RGB ');S(20,'XYZ ');S(36,'acsp');F(68,.9642);F(72,1);F(76,.8249);U(128,7);cursor=216
for i,(name,val) in enumerate(zip(['rXYZ','gXYZ','bXYZ','wtpt','rTRC','gTRC','bTRC'],[(.7976749,.2880402,0),(.1351917,.7118741,0),(.0313534,.0000857,.8249),(.9642,1,.8249),None,None,None])):
 S(132+12*i,name);U(136+12*i,cursor);U(140+12*i,20 if val else 12);S(cursor,'XYZ ' if val else 'curv')
 if val:
  for c,x in enumerate(val):F(cursor+8+4*c,x)
 cursor+=20 if val else 12
path=out/'declared-seven-tag-v2.icc';path.write_bytes(b);points=v['points'];(out/'points.json').write_text(json.dumps(points),encoding='utf-8');probe=pathlib.Path(__file__).parent/'SyntheticIccProbe/bin/Release/net10.0/SyntheticIccProbe.dll'
subprocess.run(['D:/AI AGENT/.dotnet/dotnet.exe',str(probe),str(path),str(out/'points.json'),str(out/'subset.json')],check=True)
profile=v['openp'](str(path).encode(),b'r');curves=(P*3)(*[v['gamma'](None,1.) for _ in range(3)]);dest=v['rgb'](C.byref(v['white']),C.byref(v['prim']),curves);v['adapt'](1.0);t=v['transform'](profile,v['RGBFLT'],dest,v['RGBFLT'],1,0x0100|0x0040|0x0004);assert t
inp=(C.c_float*(len(points)*3))(*[x for p in points for x in p]);result=(C.c_float*len(inp))();v['do'](t,inp,result,len(points));v['delete'](t);v['close'](profile);v['close'](dest);ours=json.loads((out/'subset.json').read_text());comp=[]
for i,r in enumerate(ours['results']):
 ref=[result[3*i+c] for c in range(3)];comp.append({'input':r['input'],'syntheticReader':r['linearSrgb'],'lcmsLinearSrgb':ref,'difference':[a-c for a,c in zip(r['linearSrgb'],ref)]})
(out/'subset-comparison.json').write_text(json.dumps({'researchOnly':True,'scope':'Declared 7-tag v2 linear profile only; not complete real-profile admission','profileSha256':hashlib.sha256(b).hexdigest(),'results':comp,'maxAbs':max(abs(x) for r in comp for x in r['difference']),'calibrationEligible':False},indent=2),encoding='utf-8')
