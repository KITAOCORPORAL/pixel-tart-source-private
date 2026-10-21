from PIL import Image,ImageDraw
import pathlib,argparse
p=argparse.ArgumentParser();p.add_argument('--dir',required=True);a=p.parse_args();root=pathlib.Path(a.dir);files=sorted(root.glob('page-*.png'),key=lambda p:int(p.stem.split('-')[-1]));tilew,tileh=360,540;canvas=Image.new('RGB',(tilew*3,tileh*((len(files)+2)//3)),'#cccccc')
for i,f in enumerate(files):
 im=Image.open(f).convert('RGB');im.thumbnail((tilew-12,tileh-25));x=(i%3)*tilew+(tilew-im.width)//2;y=(i//3)*tileh+22;canvas.paste(im,(x,y));ImageDraw.Draw(canvas).text(((i%3)*tilew+8,(i//3)*tileh+5),f.name,fill='black')
canvas.save(root/'CONTACT.png')
