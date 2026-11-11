"""Create shareable scientific curves from committed numerical CSV; no photo pixels."""
import argparse,csv,json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

def main():
 p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();rows=list(csv.DictReader((a.local_root/'candidate-validation/curves.csv').open()));a.output.mkdir(exist_ok=True,parents=True)
 fig,axes=plt.subplots(2,3,figsize=(15,8),constrained_layout=True)
 styles={'output':('production channel clip','#d64b49'),'candidate':('candidate max shoulder V1','#167b89'),'basic-tone-output':('production BasicTone','#9558b2'),'dt':('darktable exposure','#2e3338'),'rt-exposure':('RT exposure HC=0','#ce8b27'),'rt-highlight60':('RT highlights HC=60','#488e46')}
 for col,name in enumerate(['neutral','skin-approximation','warm']):
  r=[v for v in rows if v['color']==name];x=np.array([float(v['ev']) for v in r]);before=np.array([float(v['inputY']) for v in r])
  for stage,(label,color) in styles.items():
   yy=np.array([float(v[stage+'_Y']) for v in r]);axes[0,col].plot(x,yy,label=label,color=color,lw=1.5,ls='--' if stage=='rt-exposure' else '-')
  axes[0,col].axhline(1,color='#777',lw=.7);axes[0,col].set(yscale='log',ylim=(.002,32),title=name,xlabel='EV relative to Y=0.18',ylabel='linear sRGB Y');axes[0,col].grid(alpha=.2)
  for stage in ['output','candidate','basic-tone-output']:
   for ch,color in zip('RGB',['#c53b3b','#398b43','#3365b4']):
    y=np.array([float(v[stage+'_'+ch]) for v in r]);axes[1,col].plot(x,y,color=color,ls='-' if stage=='candidate' else '--' if stage=='output' else ':',lw=1.3,label=stage+' '+ch)
  axes[1,col].set(xlabel='EV relative to Y=0.18',ylabel='encoded sRGB channel',ylim=(-.02,1.03));axes[1,col].grid(alpha=.2)
 axes[0,0].legend(fontsize=8);axes[1,0].legend(fontsize=7,ncol=3);fig.suptitle('Frozen 193-step linear exposure ladders | identical product EV +5 input bridge\nCPU stage comparisons; references are not full-application rendering',fontsize=12)
 for ext in ['png','pdf']:fig.savefig(a.output/('highlight-curves.'+ext),dpi=160)
 plt.close(fig)
 # RAW display trace: exact float64 observations vs materialized float32 output.
 r=a.local_root/'candidate-validation/raw-double-product';fig,axes=plt.subplots(1,3,figsize=(14,4),constrained_layout=True);x=np.linspace(-6,6,193)
 for k,name in enumerate(['neutral','skin-approximation','warm']):
  for stage in ['matrix-linear-srgb','luminance-shoulder','neutral-axis-gamut','display-oetf']:
   data=np.fromfile(r/(stage+'.f64'),dtype='<f8').reshape(3,193,3);delta=np.abs(np.diff(data[k],axis=0)).max(axis=1);axes[k].semilogy(x[:-1],np.maximum(delta,1e-18),label=stage)
  data=np.fromfile(r/'output.f32',dtype='<f4').reshape(3,193,3);delta=np.abs(np.diff(data[k],axis=0)).max(axis=1);axes[k].semilogy(x[:-1],np.maximum(delta,1e-18),'k--',label='actual float32 output');axes[k].set(title=name,xlabel='EV relative to Y=0.18',ylabel='max RGB difference to next step');axes[k].grid(alpha=.2)
 axes[0].legend(fontsize=7);fig.suptitle('Synthetic scene-linear branch: compression and finite-precision plateaus (0 shown at 1e-18)')
 for ext in ['png','pdf']:fig.savefig(a.output/('raw-stage-increments.'+ext),dpi=160)
 print(json.dumps(dict(matplotlib=matplotlib.__version__,files=[p.name for p in a.output.glob('*')]),indent=2))
if __name__=='__main__':main()
