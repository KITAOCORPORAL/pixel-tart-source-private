"""Summarise passive local stage taps; never copy full images into Git."""
from pathlib import Path
import argparse,json,hashlib,numpy as np
p=argparse.ArgumentParser();p.add_argument('trace',type=Path);p.add_argument('original',type=Path);p.add_argument('output',type=Path);p.add_argument('--reference',type=Path);a=p.parse_args();report={'version':'NeutralDeclineLocalisationV2','physicalVisibility':'NOT_MEASURED','cases':[]}
for film in ['kodak_portra_400','kodak_ektar_100']:
 d=a.trace/('neutral_log-'+film);stage={};inventory=[]
 for f in sorted(d.glob('*.f32')):
  parts=f.stem.split('-');h,w,c=map(int,parts[-3:]);name='-'.join(parts[1:-3]);stage[name]=np.fromfile(f,dtype='<f4').reshape(h,w,c);inventory.append({'path':f.name,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
 if not stage:raise ValueError('No measured stage taps')
 result=np.load(a.original/('neutral_log-'+film+'.npy'));
 if a.reference is not None and not np.array_equal(result,np.load(a.reference/('neutral_log-'+film+'.npy'))):raise ValueError('Trace changed render output')
 declines=np.argwhere(np.diff(result[result.shape[0]//2,:,:3].astype(int),axis=0)<0);items=[]
 for i,c in declines:
  neighborhoods={name:{'left':arr[arr.shape[0]//2,i].astype(float).tolist(),'right':arr[arr.shape[0]//2,i+1].astype(float).tolist(),'delta':(arr[arr.shape[0]//2,i+1].astype(float)-arr[arr.shape[0]//2,i].astype(float)).tolist()} for name,arr in stage.items()}
  items.append({'index':int(i),'channel':int(c),'outputU16':[int(result[result.shape[0]//2,i,c]),int(result[result.shape[0]//2,i+1,c])],'stageNeighbourhoods':neighborhoods})
 report['cases'].append({'film':film,'traceDoesNotChangeOutput':True if a.reference else None,'stages':inventory,'declines':items})
with a.output.open('x') as f:json.dump(report,f,indent=2)
