# Integer-only output ownership proof for original512/496 tile loops.
from pathlib import Path
import sys,json
T=512;stride=T-16

def axis(length):
 blocks=[]
 for p in range(3,length-19,stride):
  initial=min(p+T,length-3)-p;m=(length-p+2) if length-p<T+4 else initial
  blocks.append({'origin':p,'readHalfOpen':[p,p+initial],'writeHalfOpen':[p+min(p,8),p+m-8],'initialLocalLength':initial,'adjustedLocalLength':m})
 return blocks

def check(n):
 b=axis(n);assert b
 cursor=6
 for q in b:
  lo,hi=q['writeHalfOpen'];assert lo==cursor and hi>lo and lo>=0 and hi<=n;cursor=hi
 assert cursor==n-6
 return b
lengths=list(range(512,4097))+[5184,7752,8192,65535]
for n in lengths:check(n)
cases=[]
for w,h in [(512,512),(518,518),(519,521),(1007,1015),(1014,1014),(1015,1015),(1024,1024),(1553,1007),(7752,5184)]:
 ys=check(h);xs=check(w);area=sum(y['writeHalfOpen'][1]-y['writeHalfOpen'][0] for y in ys)*sum(x['writeHalfOpen'][1]-x['writeHalfOpen'][0] for x in xs);assert area==(w-12)*(h-12)
 cases.append({'width':w,'height':h,'tileRows':len(ys),'tileColumns':len(xs),'writtenPixels':area,'outputWriteOverlapPixels':0,'writtenRectangleHalfOpen':[6,6,w-6,h-6],'lastRowTile':ys[-1],'lastColumnTile':xs[-1],'untouchedOutsideRegion':'Output seeded from originalRGB3; commitfullRGB3 leaves slots3 unchanged, then originalborder_interpolate8 runs once'})
report={'version':'XTransStableInputGeometryV1','integerLengthsChecked':len(lengths),'T':T,'stride':stride,'axisIntervalsContiguousDisjoint':True,'cases':cases,'fullFujiExtraBytes':7752*5184*6,'MiB':7752*5184*6/1048576,'uninitializedOutputRead':'None: every3channel output initialized from stable image beforetile region; finaltiles onlyoverwrite uniquevalidsubrectangles','newRawOrPixelRuns':0,'limit':'OriginalT512 valid dimensions>=512 only. This is bounds/ownership arithmetic, not aRAWdecoder test.'}
with Path(sys.argv[1]).open('x',encoding='utf-8') as f:json.dump(report,f,indent=2)
print(json.dumps({'lengths':len(lengths),'cases':len(cases),'pass':True,'extraMiB':report['MiB']}))
