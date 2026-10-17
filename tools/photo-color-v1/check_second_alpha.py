"""External alpha-over-white oracle, no product dependencies or private image scans."""
import argparse, hashlib, json, pathlib, subprocess
from PIL import Image, __version__ as pillow_version

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--output-dir',type=pathlib.Path,required=True);args=parser.parse_args();root=args.output_dir;root.mkdir(parents=True,exist_ok=False)
 rgba=[]
 for y in range(2):
  for x in range(8):rgba.extend([(x*71)%256,(x*43+y*90)%256,(255-x*29)%256,[0,64,128,255][x%4]])
 front=bytes(rgba);back=bytes([255]*64);(root/'fg.rgba').write_bytes(front);(root/'bg.rgba').write_bytes(back)
 pil=Image.alpha_composite(Image.frombytes('RGBA',(8,2),back),Image.frombytes('RGBA',(8,2),front)).convert('RGB').tobytes()
 cmd=['ffmpeg','-v','error','-f','rawvideo','-pixel_format','rgba','-video_size','8x2','-i',str(root/'bg.rgba'),'-f','rawvideo','-pixel_format','rgba','-video_size','8x2','-i',str(root/'fg.rgba'),'-filter_complex','[0:v][1:v]overlay=format=rgb:alpha=straight','-frames:v','1','-pix_fmt','rgb24','-f','rawvideo','-y',str(root/'ffmpeg.rgb')]
 run=subprocess.run(cmd,capture_output=True,timeout=30);(root/'ffmpeg.stderr.log').write_bytes(run.stderr)
 if run.returncode:raise RuntimeError('ffmpeg reference failed')
 actual=(root/'ffmpeg.rgb').read_bytes();want=bytes(round(front[i*4+c]*front[i*4+3]/255+255-front[i*4+3]) for i in range(16) for c in range(3))
 if len(actual)!=len(want):raise RuntimeError('Incomplete reference')
 result=dict(pillowVersion=pillow_version,ffmpegVersion=subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],inputSha256=hashlib.sha256(front).hexdigest(),pillowMax=max(abs(a-b) for a,b in zip(pil,want)),ffmpegMax=max(abs(a-b) for a,b in zip(actual,want)),limit8bit=1,domain='Encoded sRGB straight alpha over white')
 (root/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n');print(result);return int(result['pillowMax']>1 or result['ffmpegMax']>1)
if __name__=='__main__':raise SystemExit(main())
