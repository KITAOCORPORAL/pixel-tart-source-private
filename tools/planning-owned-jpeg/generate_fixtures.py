import sys,pathlib,json,hashlib,subprocess
sys.path.insert(0,"E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/orientation-references/python")
from PIL import Image,ImageOps
r=pathlib.Path("E:/Kitao-Worktrees/planning-owned-jpeg-company-20261023/tests/fixtures/planning-owned-jpeg");r.mkdir(parents=True,exist_ok=True)
im=Image.new('RGB',(80,48));pix=im.load()
for y in range(48):
 for x in range(80):pix[x,y]=((240,20,30) if x<40 and y<24 else (15,220,40) if x>=40 and y<24 else (30,50,235) if x<40 else (220,180,15))
im.save(r/'plain.png');im.save(r/'base.jpg',quality=95,subsampling=0)
rows=[];magick="E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/orientation-references/magick/magick.exe"
for orientation in range(1,9):
 ex=Image.Exif();ex[274]=orientation;p=r/f'orientation-{orientation}.jpg';im.save(p,quality=95,subsampling=0,exif=ex)
 with Image.open(p) as img:ImageOps.exif_transpose(img).convert('RGB').save(r/f'pillow-{orientation}.png')
 subprocess.run([magick,str(p),'-auto-orient','-depth','8',str(r/f'magick-{orientation}.png')],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
 rows.append(dict(orientation=orientation,input=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(r/'FIXTURES.json').write_text(json.dumps(dict(synthetic=True,pillow='11.3.0',magick='7.1.2-34',rows=rows),indent=2));print(json.dumps(rows))
