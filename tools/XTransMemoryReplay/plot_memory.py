"""Plot original late growth and controlled cache replay measurements."""
from pathlib import Path
import argparse,json,matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('original',type=Path);p.add_argument('--results',default='sealed-final');a=p.parse_args();r=a.root
load=lambda path:json.loads(Path(path).read_text('utf-8'))
c=load(r/a.results/'COMPARISON.json');out=r/a.results/'MEMORY_COMPARISON.png';assert not out.exists()
fig,axes=plt.subplots(1,3,figsize=(15,4.7),layout='constrained');old=load(a.original/'ui-steps/A-cold/desktop-02/RESOURCE_SAMPLES.json')
late=[s for s in old if s['elapsedMs']>=1355400]
axes[0].plot([s['elapsedMs']/1000 for s in late],[s['privateBytes']/2**20 for s in late],color='#b85134',label='PrivateBytes')
axes[0].plot([s['elapsedMs']/1000 for s in late],[s['workingSetBytes']/2**20 for s in late],color='#567894',label='WorkingSet')
axes[0].axhline(2048,linestyle='--',color='#9d2b36');axes[0].set_title('Original desktop-02: final growth');axes[0].set_xlabel('Original sampler elapsed seconds');axes[0].set_ylabel('MiB');axes[0].legend(fontsize=8);axes[0].grid(alpha=.2)
for case,label,color in [('soak-baseline-01','Baseline','#567894'),('soak-fixed-01','Memory fix','#2a987c')]:
 samples=[json.loads(s) for s in (r/(case+'-guard')/'RESOURCE_SAMPLES.jsonl').read_text('utf-8').splitlines()];bins={}
 for s in samples:bins[int(s['elapsedMs']//1000)]=max(bins.get(int(s['elapsedMs']//1000),0),s['privateBytes'])
 axes[1].plot(list(bins),[v/2**20 for v in bins.values()],label=label,color=color)
axes[1].axhline(2048,linestyle='--',color='#9d2b36',label='2 GiB guard');axes[1].set_title('Controlled 210 s pair (initial memory fix)');axes[1].set_xlabel('Replay sampler elapsed seconds');axes[1].set_ylabel('PrivateBytes MiB');axes[1].legend(fontsize=8);axes[1].grid(alpha=.2)
metrics=c['automaticHiddenView'];labels=['Final short peak','Final short idle','Final 210 s peak'];before=[metrics['beforePeakPrivateBytes'],metrics['beforeIdlePrivateBytes'],c['soak']['baseline']['peakPrivateBytes50ms']];after=[metrics['afterPeakPrivateBytes'],metrics['afterIdlePrivateBytes'],c['finalSourceSoak']['peakPrivateBytes50ms']]
axes[2].bar([x-.18 for x in range(3)],[v/2**20 for v in before],.36,label='Baseline',color='#567894');axes[2].bar([x+.18 for x in range(3)],[v/2**20 for v in after],.36,label='Memory fix',color='#2a987c');axes[2].set_xticks(range(3),labels,rotation=12);axes[2].set_ylabel('PrivateBytes MiB');axes[2].set_title(f"Short-run peak reduced {metrics['peakReductionPercent']:.1f}%");axes[2].legend(fontsize=8);axes[2].grid(axis='y',alpha=.2)
fig.suptitle('No-RAW cache replay: hidden viewport requests removed; pixel contract preserved',fontsize=13)
fig.text(.01,-.025,'Diagnostic replay; long-curve pair is the initial fix, bars include final source. Not actual RAW/TIFF acceptance. Native/graphics residuals are not fully attributed; the original 2 GiB crossing is not uniquely explained.',fontsize=8)
fig.savefig(out,dpi=150,bbox_inches='tight');plt.close(fig);print(out)
