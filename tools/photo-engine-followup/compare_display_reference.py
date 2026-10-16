"""Cross-engine monitor-profile reference; reads only exact generated reference ICC files."""
import pathlib,json,hashlib,io
from PIL import Image,ImageCms,_imagingcms
root=pathlib.Path('artifacts/followup/04-display-reference-fixed');d=json.loads((root/'DISPLAY_REFERENCE.json').read_text());rows=[]
for index,row in enumerate(d['rows'],1):
 if 'lcmsRgb16' not in row:continue
 source=(root/'source.private.icc').read_bytes();target=(root/f'display{index}.private.icc').read_bytes();values=row['inputRgb16'];rgb=[tuple(round(v/257) for v in values[i:i+3]) for i in range(0,len(values),3)];image=Image.new('RGB',(len(rgb),1));image.putdata(rgb)
 transform=ImageCms.buildTransform(ImageCms.ImageCmsProfile(io.BytesIO(source)),ImageCms.ImageCmsProfile(io.BytesIO(target)),'RGB','RGB',renderingIntent=1,flags=ImageCms.Flags.NOOPTIMIZE|ImageCms.Flags.NOCACHE)
 result=list(ImageCms.applyTransform(image,transform).getdata());expected=[tuple(round(v/257) for v in row['lcmsRgb16'][i:i+3]) for i in range(0,len(values),3)];maximum=max(abs(a-b) for x,y in zip(result,expected) for a,b in zip(x,y));rows.append(dict(device=row['device'],sourceSha256=hashlib.sha256(source).hexdigest(),monitorSha256=hashlib.sha256(target).hexdigest(),input8=rgb,pillowLcms219=result,nativeLcms212Quantized8=expected,max8bitDifference=maximum,limit=2,status='PASSED_SUBSET' if maximum<=2 else 'FAILED'))
r=dict(status='PASSED_REFERENCE_SUBSET' if all(x['status']=='PASSED_SUBSET' for x in rows) and len(rows)==2 else 'FAILED',pillowVersion=Image.__version__,lcmsVersion=ImageCms.core.littlecms_version,pillowModuleSha256=hashlib.sha256(pathlib.Path(_imagingcms.__file__).read_bytes()).hexdigest(),rows=rows,precision='8bit comparison only, not 16bit equivalence',wpfCompositor='NOT_RUN',productionDisplayEnabled=False)
pathlib.Path('artifacts/followup/04-display-cross-reference.json').write_text(json.dumps(r,indent=2)+'\n');print(r['status'],[(x['device'],x['max8bitDifference']) for x in rows])
