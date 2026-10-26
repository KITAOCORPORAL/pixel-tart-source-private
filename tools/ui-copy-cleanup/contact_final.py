from PIL import Image,ImageDraw
import pathlib
r=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");pairs=[('Planning-project-1920','Planning-project-1920'),('Settings-1920','Settings-1920'),('Tether-1920','Tether-1920'),('Toolbox-1920','Toolbox-1920'),('Finance-1600','Finance-1600')];out=Image.new('RGB',(1400,len(pairs)*370),'#dce0e5');draw=ImageDraw.Draw(out)
for i,(left,right) in enumerate(pairs):
 for col,(folder,name,label) in enumerate([('routes-finalcandidate-before',left,'BEFORE'),('routes-sealed',right,'AFTER')]):
  im=Image.open(r/folder/'images'/(name+'.png')).convert('RGB');im.thumbnail((690,340));out.paste(im,(col*700,i*370+24));draw.text((col*700+8,i*370+5),name+' '+label+' / OFFSCREEN',fill='black')
out.save(r/'BEFORE_AFTER_FINAL.png')
