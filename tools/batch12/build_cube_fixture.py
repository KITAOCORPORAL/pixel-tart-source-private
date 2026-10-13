import csv,json,struct,zlib,hashlib,math,pathlib
root=pathlib.Path(__file__).resolve().parents[2];out=root/'tests/RAWSelectionAssistant.Tests/TestData/Batch12';p=out/'00_0010-simple.png'
b=p.read_bytes();assert b[:8]==b'\x89PNG\r\n\x1a\n';pos=8;payload=b''
while pos<len(b):
 n=struct.unpack_from('>I',b,pos)[0];tag=b[pos+4:pos+8];v=b[pos+8:pos+8+n];assert zlib.crc32(tag+v)==struct.unpack_from('>I',b,pos+8+n)[0]
 if tag==b'IHDR':w,h,depth,kind,compression,filter_method,interlace=struct.unpack('>2I5B',v);assert (depth,kind,compression,filter_method,interlace)==(16,2,0,0,0)
 if tag==b'IDAT':payload+=v
 pos+=12+n
raw=zlib.decompress(payload);stride=w*6;rows=[];prior=bytearray(stride);pos=0
for y in range(h):
 f=raw[pos];pos+=1;row=bytearray(raw[pos:pos+stride]);pos+=stride
 for i in range(stride):
  a=row[i-6] if i>=6 else 0;bb=prior[i];c=prior[i-6] if i>=6 else 0
  if f==1:predict=a
  elif f==2:predict=bb
  elif f==3:predict=(a+bb)//2
  elif f==4:
   q=a+bb-c;dist=[abs(q-a),abs(q-bb),abs(q-c)];predict=[a,bb,c][dist.index(min(dist))]
  else:assert f==0;predict=0
  row[i]=(row[i]+predict)&255
 rows.append(struct.unpack('>'+str(w*3)+'H',row));prior=row
# Uniform sparse sample, excluding the removed SpyderCube rectangle and saturated/dark entries.
black=[2041,2050,2059];white=14582;selected=[]
for y in range(6,h,24):
 for x in range(6,w,24):
  v=rows[y][3*x:3*x+3]
  if all(black[i]<v[i]<white for i in range(3)):selected.append([x,y,*v])
with (out/'00_0010-sensor-grid.csv').open('w',newline='') as f:
 cw=csv.writer(f,lineterminator='\n');cw.writerow(['x','y','r','g','b']);cw.writerows(selected)
gt=json.loads((out/'00_0010-gt.json').read_text())['mean'];mean=[sum((s[2+i]-black[i])/(white-black[i]) for s in selected)/len(selected) for i in range(3)];norm=lambda v:[x/sum(v) for x in v];angle=lambda a,b:math.degrees(math.acos(max(-1,min(1,sum(x*y for x,y in zip(a,b))/math.sqrt(sum(x*x for x in a)*sum(x*x for x in b))))))
balanced=[mean[i]*gt[1]/gt[i] for i in range(3)];metric=dict(algorithmVersion='PublicSensorBlackWbV1',metric='Gray-world angular error vs published mean illuminant (not a calibrated image metric)',sampleId='00_0010',sensorModel='Canon EOS 550D',datasetImageVersion='1.0 (Cube+ inherited within Cube++)',orientation=1,blackLevels=black,greenBlackPolicy='Mean of two EXIF greens (2041+2059)/2; explicit evaluation choice, not measured per-pixel black truth',whiteLevel=white,width=w,height=h,gridCount=len(selected),groundTruth=gt,grayWorld=norm(mean),angularErrorDegrees=angle(mean,gt),afterGroundTruthWbNeutralErrorDegrees=angle(balanced,[1,1,1]),syntheticGroundTruthBalanceErrorDegrees=angle([gt[i]*gt[1]/gt[i] for i in range(3)],[1,1,1]),limits=['SimpleCube++ is downscaled, target-cropped camera RGB, not native RAW or sRGB','One inherited Cube+ sample, high ISO; not broad camera accuracy','Published mean is illumination chromaticity, not scene Lab or spectral truth'],sourcePngSha256=hashlib.sha256(b).hexdigest(),gridSha256=hashlib.sha256((out/'00_0010-sensor-grid.csv').read_bytes()).hexdigest())
(out/'00_0010-metrics.json').write_text(json.dumps(metric,indent=2)+'\n');print(json.dumps(metric,indent=2))
