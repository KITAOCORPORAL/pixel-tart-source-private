from PIL import Image,ImageDraw
import pathlib,json,hashlib
r=pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023/large-fixtures");r.mkdir(exist_ok=True)
for ext in ['jpg','png']:
 for index in range(3):
  im=Image.new('RGB',(6000,4000),(20+index*40,40,70));d=ImageDraw.Draw(im)
  for y in range(0,4000,100):d.rectangle((0,y,5999,y+49),fill=(y%255,180,index*70))
  d.rectangle((0,0,2999,1999),fill=(230,20,30));d.rectangle((3000,2000,5999,3999),fill=(20,220,70))
  p=r/f'large-{index}.{ext}'
  if ext=='jpg':ex=Image.Exif();ex[274]=6;im.save(p,quality=92,subsampling=0,exif=ex)
  else:im.save(p)
files=[dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(r.glob('*')) if p.suffix in ['.jpg','.png']]
(r/'MANIFEST.json').write_text(json.dumps(dict(width=6000,height=4000,pixels=24000000,files=files),indent=2));print(files)
