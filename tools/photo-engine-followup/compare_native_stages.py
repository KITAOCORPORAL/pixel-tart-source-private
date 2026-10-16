"""Numerical distribution only; never publish actual photo pixels or render a photo."""
import argparse,json,pathlib,numpy as np
p=argparse.ArgumentParser();p.add_argument('--first',type=pathlib.Path,required=True);p.add_argument('--second',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
if a.output.exists():raise RuntimeError('new evidence path required')
x=json.loads((a.first/'result.json').read_text(encoding='utf-8-sig'));y=json.loads((a.second/'result.json').read_text(encoding='utf-8-sig'));rows=[]
for first in x['rows']:
 stage=first['stage'];second=next((v for v in y['rows'] if v['stage']==stage),None)
 if not second or 'sha256' not in first or 'sha256' not in second:continue
 f=a.first/(first.get('localFile') or 'processed-rgb16.private-pixels.bin');g=a.second/(second.get('localFile') or 'processed-rgb16.private-pixels.bin')
 width=first.get('width') or first.get('Width');height=first.get('height') or first.get('Height');channels=first.get('channels') or first.get('Channels');aa=np.memmap(f,dtype='<u2',mode='r').reshape(height,width,channels);bb=np.memmap(g,dtype='<u2',mode='r').reshape(height,width,channels)
 hist=[0]*7;changed=0;maximum=0;coords=[];channel_counts=np.zeros(channels,dtype=np.int64);grid=np.zeros((6,6,channels),dtype=np.int64);tile_y=np.zeros(496,dtype=np.int64);tile_x=np.zeros(496,dtype=np.int64)
 for row in range(height):
  delta=np.abs(aa[row].astype(np.int32)-bb[row].astype(np.int32));mask=delta!=0;n=int(mask.sum());changed+=n;maximum=max(maximum,int(delta.max()));channel_counts+=mask.sum(axis=0)
  if not n:continue
  for col,ch in np.argwhere(mask):
   if len(coords)<64:coords.append(dict(x=int(col),y=row,channel=int(ch),absoluteDelta=int(delta[col,ch])))
  for mod in range(6):grid[row%6,mod]+=mask[mod::6].sum(axis=0)
  tile_y[(row-3)%496]+=n
  for col in np.flatnonzero(mask.any(axis=1)):tile_x[(int(col)-3)%496]+=int(mask[col].sum())
  values=delta[mask];bounds=[1,4,16,64,256,1024,65536];prev=0
  for i,bound in enumerate(bounds):hist[i]+=int(((values>prev)&(values<=bound)).sum());prev=bound
 rows.append(dict(stage=stage,identical=first['sha256']==second['sha256'],changedChannelSamples=changed,maximum16BitDifference=maximum,channelCounts=channel_counts.tolist(),amplitudeBins=dict(zip(['1','2-4','5-16','17-64','65-256','257-1024','1025-65535'],hist)),first64DifferenceCoordinates=coords,xtrans6x6Counts=grid.tolist(),tile496RowResidues=tile_y.tolist(),tile496ColumnResidues=tile_x.tolist(),coordinatesAreNotImages=True))
r=dict(status='STAGE_DIFFERENCES_MEASURED',sourceSha256=x['sourceSha256'],nativeSha256=x['nativeSha256'],firstThreads=x['OMP_NUM_THREADS'],secondThreads=y['OMP_NUM_THREADS'],earliestObservedDifferentStage=next((v['stage'] for v in rows if not v['identical']),None),rows=rows,rootCauseProven=False)
a.output.write_text(json.dumps(r,indent=2)+'\n');print({k:v for k,v in r.items() if k!='rows'})
