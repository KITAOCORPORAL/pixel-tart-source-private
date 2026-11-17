from pathlib import Path
import json,sys,datetime
from reportlab.graphics.shapes import Drawing,String,Line
from reportlab.graphics.charts.lineplots import LinePlot
from reportlab.lib import colors
from reportlab.graphics import renderSVG
root=Path(sys.argv[1]);out=Path(sys.argv[2]);oldroot=Path('E:/Guangjing-RawReopen001-ProductRegression-20261110');old=json.loads((oldroot/'ui-steps/A-cold/desktop-02/RESOURCE_SAMPLES.json').read_text());new=[]
for run in ['resource-vm-e818-01','resource-vm-e818-reopen-proxy-02']:new.append([json.loads(x) for x in (root/run/'RESOURCE_SAMPLES.jsonl').read_text().splitlines()])
launch=json.loads((oldroot/'ui-steps/A-cold/desktop-02/LAUNCH.json').read_text());events=[json.loads(x) for x in (oldroot/'path-evidence/A-cold-2408.jsonl').read_text().splitlines()];offset=(datetime.datetime.fromisoformat(events[-1]['atUtc']).timestamp()-launch['startedUtc'])*1000
buckets={}
for x in old:
 k=int((x['elapsedMs']-offset)/1000)
 if -15<=k<=205 and (k not in buckets or x['privateBytes']>buckets[k]['privateBytes']):buckets[k]=x
series=[([(x['elapsedMs']-offset)/1000,x['privateBytes']/1048576,x['workingSetBytes']/1048576] for _,x in sorted(buckets.items()))]+[([x['elapsedMs']/1000,x['privateBytes']/1048576,x['workingSetBytes']/1048576] for x in arr) for arr in new]
d=Drawing(1110,385);d.add(String(20,364,'Frozen X-Trans RGB48 replay resource evidence (no new RAW/native worker)',fontName='Helvetica-Bold',fontSize=13))
titles=['Original parent: late STOP; exact allocation unknown','Zero-RAW detail/export: STOP','Zero-RAW proxy export/reopen: STOP']
for i,s in enumerate(series):
 rows=list(s);x=40+i*365;lp=LinePlot();lp.x=x;lp.y=72;lp.width=310;lp.height=240;lp.data=[[(r[0],r[1]) for r in rows],[(r[0],r[2]) for r in rows],[(min(r[0] for r in rows),2048),(max(r[0] for r in rows),2048)]];lp.lines[0].strokeColor=colors.HexColor('#a84e32');lp.lines[1].strokeColor=colors.HexColor('#39716c');lp.lines[2].strokeColor=colors.black;lp.lines[2].strokeDashArray=[3,3];lp.lines[0].strokeWidth=1.5;lp.lines[1].strokeWidth=1;lp.xValueAxis.labels.fontSize=8;lp.yValueAxis.labels.fontSize=8;lp.xValueAxis.valueMin=-15 if i==0 else 0;lp.xValueAxis.valueMax=205 if i==0 else max(r[0] for r in rows);lp.yValueAxis.valueMin=0;lp.yValueAxis.valueMax=2600;lp.yValueAxis.valueStep=512;d.add(lp);d.add(String(x,330,titles[i],fontSize=9));d.add(String(x,45,'Seconds relative to import end (approx.)' if i==0 else 'Seconds from host monitor start',fontSize=8));d.add(String(x,316,'MiB',fontSize=8))
d.add(String(20,22,'Red: PrivateBytes; green: WorkingSet; dashed: 2 GiB guard. 50 ms target sampling; no forced GC.',fontSize=8));d.add(String(20,8,'Original 26,626 samples preserved. New runs are controlled failures, not ordinary product acceptance or old STOP attribution.',fontSize=8));renderSVG.drawToFile(d,str(out/'RESOURCE_PEAKS.svg'));print(json.dumps({'chart':'RESOURCE_PEAKS.svg','originalPoints':len(buckets),'newPoints':[len(x) for x in new]}))
