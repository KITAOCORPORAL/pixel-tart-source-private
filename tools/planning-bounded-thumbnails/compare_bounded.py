import pathlib,json,sys,subprocess,hashlib
sys.path.insert(0,'E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/orientation-references/python')
from PIL import Image,ImageOps,ImageStat,ImageChops
r=pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023");magick='E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/orientation-references/magick/magick.exe';out=r/'independent-bounded-comparison';out.mkdir(exist_ok=True);rows=[]
for source in sorted((r/'large-fixtures').glob('*')):
 if source.suffix not in ['.jpg','.png']:continue
 actual=Image.open(r/'perf-final-after-0'/(source.name+'.thumbnail.png')).convert('RGB')
 with Image.open(source) as image:
  expected=ImageOps.exif_transpose(image).convert('RGB');expected.thumbnail((512,512),Image.Resampling.LANCZOS);expected.save(out/(source.name+'.pillow.png'))
 subprocess.run([magick,str(source),'-auto-orient','-resize','512x512>','-depth','8',str(out/(source.name+'.magick.png'))],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
 for label,ref in [('Pillow11.3.0',expected),('ImageMagick7.1.2-34',Image.open(out/(source.name+'.magick.png')).convert('RGB'))]:
  assert actual.size==ref.size,(actual.size,ref.size);diff=ImageChops.difference(actual,ref);stats=ImageStat.Stat(diff);mae=sum(stats.mean)/3;maximum=max(x[1] for x in stats.extrema);rows.append(dict(input=source.name,inputSha256=hashlib.sha256(source.read_bytes()).hexdigest(),reference=label,size=actual.size,meanAbsolute8bit=mae,maxAbsolute8bit=maximum,scope='Different resampling/codec kernels: report actual errors, no bit-equivalence or general color claim'))
(r/'BOUNDED_REFERENCE_COMPARISON.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows))
