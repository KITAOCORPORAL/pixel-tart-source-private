from pathlib import Path
import argparse,csv,json,numpy as np,matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('root',type=Path);a=p.parse_args();r=a.root;out=r/'review-figures';out.mkdir(exist_ok=False)
with (r/'synthetic-review/EV0_CURVES.csv').open(encoding='utf-8',newline='') as f:rows=list(csv.DictReader(f))
neutral=[v for v in rows if v['color']=='neutral'];m=np.array([float(v['maxInputLinear']) for v in neutral]);old=np.array([float(v['originalY']) for v in neutral]);new=np.array([float(v['candidateY']) for v in neutral])
fig,axes=plt.subplots(1,3,figsize=(14,4.6),layout='constrained')
axes[0].plot(m,old,label='Original EV0',color='#6a7480');axes[0].plot(m,new,label='Frozen candidate EV0',color='#b96b37');axes[0].axvline(.75,color='#8c7d72',linestyle=':');axes[0].scatter([1],[.875],color='#b96b37');axes[0].annotate('White: 1 -> 0.875\n-0.193 EV',xy=(1,.875),xytext=(.52,.93),arrowprops={'arrowstyle':'->'},fontsize=9);axes[0].set_xlabel('Input neutral linear Y');axes[0].set_ylabel('Output linear Y');axes[0].set_title('EV0 is not identity above knee');axes[0].legend(fontsize=8);axes[0].grid(alpha=.2)
axes[1].plot(m,[float(v['originalLocalSlope']) for v in neutral],label='Original',color='#6a7480');axes[1].plot(m,[float(v['candidateLocalSlope']) for v in neutral],label='Measured candidate',color='#b96b37');axes[1].plot(m,np.where(m<=.75,1,.0625/np.maximum(m-.5,1e-6)**2),'--',linewidth=1,color='#2b8a83',label='Analytic derivative');axes[1].set_xlabel('Input neutral linear Y');axes[1].set_ylabel('dYout / dYin');axes[1].set_ylim(0,1.1);axes[1].set_title('Absolute local-contrast compression');axes[1].legend(fontsize=8);axes[1].grid(alpha=.2)
roi=json.loads((r/'roi-comparison02/ROI_RESULTS.json').read_text('utf-8'))
keys=['skin-shoulder','warm-lit-texture','synthetic-neutral-texture','synthetic-coloured-specular'];labels=['Skin\nphoto','Warm texture\nphoto','Neutral texture\nSYNTHETIC','Coloured specular\nSYNTHETIC'];lum=[];contrast=[]
for key in keys:
 v=next(v for v in roi['comparisons'] if v['id']==key and v['baselineEv']==0);lum.append(v['candidateSame']['highMeanY']/v['original']['highMeanY']);contrast.append(v['candidateSame']['highRelativeLocalRms']/v['original']['highRelativeLocalRms'])
xx=np.arange(4);axes[2].bar(xx-.18,lum,.36,label='Highlight mean Y ratio',color='#b96b37');axes[2].bar(xx+.18,contrast,.36,label='Relative gradient RMS ratio',color='#2b8a83');axes[2].axhline(1,color='#6a7480',linestyle='--');axes[2].set_xticks(xx,labels,fontsize=8);axes[2].set_ylim(0,1.15);axes[2].set_ylabel('Candidate / original at EV0');axes[2].set_title('Fixed-ROI brightness and texture cost');axes[2].legend(fontsize=8);axes[2].grid(axis='y',alpha=.2)
fig.suptitle('Frozen MaxChannelShoulderV1 review: Knee=0.75, rendered sRGB input, no RAW decode',fontsize=13)
fig.text(.01,-.025,'Real garment and verified metal highlights are missing. Synthetic supplements are numerical texture controls, not photographic material acceptance.',fontsize=8)
fig.savefig(out/'EV0_CURVES_AND_ROI_EFFECTS.png',dpi=160,bbox_inches='tight');plt.close(fig)
paths=[r/'roi-comparison02/skin-shoulder-E0-comparison.png',r/'roi-comparison02/warm-lit-texture-E1-comparison.png',r/'roi-comparison02/synthetic-neutral-texture-E0-comparison.png']
images=[Image.open(p).convert('RGB') for p in paths];sheet=Image.new('RGB',(max(i.width for i in images),sum(i.height for i in images)),'white');y=0
for i in images:sheet.paste(i,(0,y));y+=i.height
sheet.save(out/'FIXED_REVIEW_SELECTION.png');print(out)
