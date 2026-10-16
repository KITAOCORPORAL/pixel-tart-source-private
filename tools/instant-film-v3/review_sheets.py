"""Local authorized output review; never downloads or redistributes private photos."""
import argparse,json,hashlib
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
import numpy as np

def main():
 p=argparse.ArgumentParser();p.add_argument("root",type=Path);a=p.parse_args();root=a.root
 rows=json.loads((root/"review.json").read_text(encoding="utf8"));metrics=[]
 for sample in sorted({r["input"] for r in rows}):
  group=[r for r in rows if r["input"]==sample];d=root/sample
  sheet=Image.new("RGB",(1020,len(group)*395),(25,26,29));draw=ImageDraw.Draw(sheet)
  for y,r in enumerate(group):
   stem=r["profile"]
   for col,(name,title) in enumerate([("original.tif","Original"),(stem+"-legacy.png","Legacy V2"),(stem+"-candidate.png","Candidate V3 (research)")]):
    with Image.open(d/name) as src:
     # Display-only contact sheet conversion. Quantitative statistics read lossless 16-bit via TIFF file values separately.
     im=src.convert("RGB");thumb=ImageOps.contain(im,(324,330));sheet.paste(thumb,(col*340+(340-thumb.width)//2,y*395+45))
    draw.text((col*340+10,y*395+9),stem+" / "+title,fill=(240,240,240))
   # Crops are literal 1:1 pixels at REVIEW resolution, not claims about original RAW details.
   detail=Image.new("RGB",(720,240),(25,26,29));dd=ImageDraw.Draw(detail)
   for col,(name,title) in enumerate([(stem+"-legacy.png","V2"),(stem+"-candidate.png","V3")]):
    with Image.open(d/name) as src:
     im=src.convert("RGB");cx,cy=im.width//2,im.height//2;box=(max(0,cx-115),max(0,cy-95),min(im.width,cx+115),min(im.height,cy+95));crop=im.crop(box);detail.paste(crop,(col*360+10,35));dd.text((col*360+10,9),title+" 1:1 review-resolution center",fill="white")
   path=d/(stem+"-detail.jpg")
   if path.exists():raise FileExistsError(path)
   detail.save(path,quality=95)
   # Parse product TIFF16 uncompressed strips through tifffile if installed is not required: original product validation records exact full RGB16.
   # Cropping statistics use preserved PNG16 via ffmpeg rgb48le, not PIL's RGB8 conversion.
   import subprocess
   for variant in ["legacy","candidate"]:
    path=d/(stem+"-"+variant+".png")
    with Image.open(path) as im:w,h=im.size
    raw=subprocess.run(["ffmpeg","-v","error","-i",str(path),"-f","rawvideo","-pix_fmt","rgb48le","-"],check=True,capture_output=True).stdout
    data=np.frombuffer(raw,dtype="<u2").reshape(h,w,3)
    x0,y0=w//4,h//4;x1,y1=3*w//4,3*h//4
    for region,arr in [("full",data),("center-review-crop",data[y0:y1,x0:x1])]:
     metrics.append(dict(input=sample,profile=stem,variant=variant,region=region,regionBounds=[0,0,w,h] if region=="full" else [x0,y0,x1,y1],samples=arr.size,zeroCodeSamples=int((arr==0).sum()),maxCodeSamples=int((arr==65535).sum()),belowOnePercent=int((arr<656).sum()),above99Percent=int((arr>64879).sum()),meanCode=float(arr.mean()),minCode=int(arr.min()),maxCode=int(arr.max()),pixelSha256=hashlib.sha256(arr.tobytes()).hexdigest()))
  dest=d/"comparison.jpg"
  if dest.exists():raise FileExistsError(dest)
  sheet.save(dest,quality=95)
 (root/"crop-statistics.json").write_text(json.dumps(metrics,indent=2),encoding="utf8")
 print("contactSheets",len({r["input"] for r in rows}),"losslessRegionStatistics",len(metrics))
if __name__=="__main__":main()
