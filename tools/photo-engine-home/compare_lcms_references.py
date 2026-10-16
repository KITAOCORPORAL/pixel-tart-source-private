"""Reference-only LCMS run against current existing native dependency and Pillow's independent LCMS build.
No production allowlist/profile/default changes. Inputs are original synthetic fixture samples only.
"""
import argparse,ctypes as C,pathlib,json,hashlib,struct
from PIL import Image,ImageCms,_imagingcms
p=argparse.ArgumentParser();p.add_argument('--source-root',type=pathlib.Path,required=True);p.add_argument('--wic-result',type=pathlib.Path,required=True);p.add_argument('--native',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
if a.output.exists():raise RuntimeError('New evidence path required')
r=json.loads(a.wic_result.read_text());source=(a.source_root/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes();target=(a.wic_result.parent/'output-srgb.icc').read_bytes();sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(source)==r['sourceProfileSha256'].lower() and sha(target)==r['outputProfileSha256'].lower()
expected_native='87bf94f27f345384420383f247178f825c0101928fedd3a4887ff60e9d4a76eb'
assert sha(a.native.read_bytes())==expected_native,'Existing native component identity changed'
dll=C.CDLL(str(a.native.resolve()));P=C.c_void_p;U=C.c_uint
for n,res,args in [('cmsGetEncodedCMMversion',U,[]),('cmsOpenProfileFromMem',P,[P,U]),('cmsCloseProfile',C.c_int,[P]),('cmsCreateTransform',P,[P,U,P,U,U,U]),('cmsDoTransform',None,[P,P,P,U]),('cmsDeleteTransform',None,[P])]:
 f=getattr(dll,n);f.restype=res;f.argtypes=args
assert dll.cmsGetEncodedCMMversion()==2120
sb=C.create_string_buffer(source);tb=C.create_string_buffer(target);s=dll.cmsOpenProfileFromMem(sb,len(source));t=dll.cmsOpenProfileFromMem(tb,len(target));assert s and t
fmt=(4<<16)|(3<<3)|2;tr=dll.cmsCreateTransform(s,fmt,t,fmt,1,0x140);assert tr
samples=[tuple(x) for x in r['samples']];original=list(samples)
# Independent 8bit same-code reference inputs so precision/rounding are not conflated with profile differences.
samples += [tuple(v*257 for v in row) for row in [(0,0,0),(1,1,1),(46,46,46),(128,128,128),(255,255,255),(255,0,0),(0,255,0),(0,0,255),(110,70,45)]]
array=(C.c_ushort*(len(samples)*3))(*(v for row in samples for v in row));result=(C.c_ushort*len(array))()
try:dll.cmsDoTransform(tr,array,result,len(samples))
finally:dll.cmsDeleteTransform(tr);dll.cmsCloseProfile(t);dll.cmsCloseProfile(s)
def oetf(v):v=v/65535;return 12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055
rows=[]
for i,row in enumerate(samples):
 got=list(result[i*3:i*3+3]);want=[round(oetf(v)*65535) for v in row];rows.append(dict(input=list(row),lcms212=got,analyticSrgb=want,maxAbsCodeError=max(abs(x-y) for x,y in zip(got,want))))
# Pillow uses its own LCMS2.19 engine via ImageCms, not the product native DLL.
inprofile=ImageCms.ImageCmsProfile(__import__('io').BytesIO(source));outprofile=ImageCms.ImageCmsProfile(__import__('io').BytesIO(target))
input8=[tuple(v//257 for v in row) for row in samples[len(original):]];im=Image.new('RGB',(len(input8),1));im.putdata(input8);transform=ImageCms.buildTransform(inprofile,outprofile,'RGB','RGB',renderingIntent=1,flags=0);pillow=list(ImageCms.applyTransform(im,transform).getdata())
comparisons=[]
for index,values in enumerate(pillow):
 c=rows[len(original)+index];from16=[round(x/257) for x in c['lcms212']];comparisons.append(dict(input=input8[index],lcms212Quantized8=from16,pillowLcms219=list(values),maxAbs8=max(abs(x-y) for x,y in zip(from16,values))))
# Correct independent profile reference: both RGB colorant matrices are already PCS D50.
# Do NOT apply chad a second time. Output is this exact profile's table TRC, not ideal analytic OETF.
def tags(blob):
 n=struct.unpack_from('>I',blob,128)[0];return {struct.unpack_from('>4s',blob,132+12*i)[0]:struct.unpack_from('>II',blob,136+12*i) for i in range(n)}
def matrix(blob):
 table=tags(blob);cols=[]
 for key in [b'rXYZ',b'gXYZ',b'bXYZ']:
  off,count=table[key];assert blob[off:off+4]==b'XYZ ' and count==20;cols.append([struct.unpack_from('>i',blob,off+8+4*k)[0]/65536 for k in range(3)])
 return [[cols[c][r] for c in range(3)] for r in range(3)]
def inverse(m):
 a,b,c=m[0];d,e,f=m[1];g,h,i=m[2];det=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g)
 return [[(e*i-f*h)/det,(c*h-b*i)/det,(b*f-c*e)/det],[(f*g-d*i)/det,(a*i-c*g)/det,(c*d-a*f)/det],[(d*h-e*g)/det,(b*g-a*h)/det,(a*e-b*d)/det]]
def mul(m,v):return [sum(row[j]*v[j] for j in range(3)) for row in m]
def inverse_trc(blob,key,value):
 off,size=tags(blob)[key];assert blob[off:off+4]==b'curv';n=struct.unpack_from('>I',blob,off+8)[0];value=max(0,min(1,value))
 if n==0:return value
 if n==1:return value**(1/(struct.unpack_from('>H',blob,off+12)[0]/256))
 points=struct.unpack_from('>'+str(n)+'H',blob,off+12);assert all(x<=y for x,y in zip(points,points[1:]));import bisect
 pos=bisect.bisect_left(points,value*65535)
 if pos==0:return 0
 if pos>=n:return 1
 return ((pos-1)+(value*65535-points[pos-1])/(points[pos]-points[pos-1]))/(n-1)
source_matrix=matrix(source);inverse_target=inverse(matrix(target));exact_rows=[]
for index,row in enumerate(samples):
 xyz=mul(source_matrix,[v/65535 for v in row]);linear=mul(inverse_target,xyz);reference=[round(inverse_trc(target,key,v)*65535) for key,v in zip([b'rTRC',b'gTRC',b'bTRC'],linear)];got=list(result[index*3:index*3+3]);exact_rows.append(dict(input=row,sourcePcsD50=xyz,targetLinear=linear,independentExactProfileCodes=reference,lcms212=got,maxAbsCodeError=max(abs(x-y) for x,y in zip(reference,got))))
exact_error=max(x['maxAbsCodeError'] for x in exact_rows);cross_error=max(x['maxAbs8'] for x in comparisons)
observed=dict(status='PASSED_REFERENCE_SUBSET' if exact_error<=12 and cross_error<=1 else 'FAILED_REFERENCE_SUBSET',nativePath=str(a.native.resolve()),nativeSha256=expected_native,nativeVersion=2120,pillowVersion=__import__('PIL').__version__,pillowLcmsVersion=ImageCms.core.littlecms_version,pillowModuleSha256=sha(pathlib.Path(_imagingcms.__file__).read_bytes()),sourceProfileSha256=sha(source),targetProfileSha256=sha(target),intent=1,bpc=False,adaptation='default1',lcmsFlags='NOOPTIMIZE|NOCACHE, 0x140',rows=rows,independentExactProfile=exact_rows,sourcePcsMatrix=source_matrix,targetPcsMatrix=matrix(target),independent8bit=comparisons,wicStatus=r['status'],productAdmitted=False,limits='Known synthetic linear-sRGB -> exact Windows sRGB profile only; ideal OETF mismatch not erased. PCS matrices already D50, no repeated chad; table-TRC inverse explicit. Pillow8/native16 rounding separate. Not arbitrary ICC, monitor compensation or reviewed LCMS2.16 identity.')
a.output.write_text(json.dumps(observed,indent=2));print(observed['status'],exact_error,cross_error);raise SystemExit(observed['status'].startswith('FAILED'))
