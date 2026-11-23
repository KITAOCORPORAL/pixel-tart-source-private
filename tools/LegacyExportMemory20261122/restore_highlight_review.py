from pathlib import Path
import json,html,shutil,hashlib
repo=Path.cwd();ref=repo/'artifacts/legacy-export-memory-20261122/research-reference/docs/research';out=repo/'artifacts/legacy-export-memory-20261122/highlight-review02';out.mkdir(exist_ok=False)
d=json.loads((ref/'color-fairness-resource-20261117/FAIR_FULL_ROI_RESULTS.json').read_text())
choices=[('skin-shoulder',0,'post-curve','肩部 EV0：原曲线已能接近柔化；候选仍有压平光泽代价'),('warm-lit-texture',1,'post-curve','暖纹理 EV+1：候选保留裁切前增量；白点分布仍不同'),('warm-lit-texture',1,'basic-highlights','暖纹理 EV+1：同输入 BasicTone 已接近'),('warm-lit-texture',0,'post-highlights','暖纹理 EV0：现有高光控制已足够')]
selected=[]
for name,ev,method,note in choices:
 rows=[x for x in d['rows'] if x['id']==name and x['ev']==ev and x['method']==method and x['condition']=='mid+high'];assert len(rows)==1,(name,ev,method)
 selected.append({**rows[0],'reviewNote':note,'panelAvailableAtHome':False})
manifest=json.loads((repo.parent/'PortraitHighlights-20261111/input-manifest.json').read_text())
crop=repo.parent/'PortraitHighlights-20261111/portrait-crop.png';shutil.copyfile(crop,out/'authorized-home-source.png')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>高光研究·少量只读复核</title><style>body{margin:auto;max-width:1050px;background:#17191c;color:#e6e6e3;font:16px/1.7 system-ui;padding:32px}article{background:#24272b;padding:20px;margin:18px 0;border-radius:10px}small{color:#b5babf}img{max-width:384px;width:100%;image-rendering:auto}summary{cursor:pointer}code{overflow-wrap:anywhere}</style><h1>高光研究·只读收敛页</h1><p>沿用 2026-11-17 公平对照；不新增候选运行，不调整 Knee/Amount，不开发 V2。高光候选保持不接入，锐化 V1 封存。</p><p>以下仅选 4 项代表性对照。公司冻结面板不在家庭机器，原 180 面板未重做。数值来自已提交的固定 ROI 结果，缺图项明确保留；本页不显示伪造的补图。</p>'''
for row in selected:
 page+='<article><h2>'+html.escape(row['reviewNote'])+'</h2><p>状态：'+html.escape(str(row['status']))+'；冻结面板待公司私有文件。</p><p>候选高亮均值 '+str(row['candidate']['highMeanY'])+'；原控制高亮均值 '+str(row.get('control',{}).get('highMeanY'))+'</p><details><summary>查看固定结果与限制</summary><pre style="white-space:pre-wrap">'+html.escape(json.dumps(row,ensure_ascii=False,indent=2))+'</pre></details></article>'
page+='''<article><h2>家庭已有授权源裁片</h2><p>下面是原有 Sony 显影 TIFF 的源裁片，只供确认已有材料；它不是 11-17 公司 ROI 的候选／BasicTone 公平对照，不据此新增结论。</p><img src="authorized-home-source.png"><p>原图存储 7028×4688，方向8，裁片384×384。已有肩部不能外推面部、白衣或金属。</p></article><h2>缺项</h2><p>公司肩部／暖纹理／发丝／Canon 冻结 ROI、V1 f32 和对应公平控制面板；0806D680 RGB48 payload；白色哑光及光泽织物、确认金属、真实人脸亮部、可追溯日光与暖／混合光、不同输入 ICC。Canon 固定中间调锚点不足仍不可比。缺项不触发 RAW 解码。</p></html>'''
(out/'index.html').write_text(page,encoding='utf-8');(out/'data.json').write_text(json.dumps({'selectedRows':selected,'newCandidateRuns':0,'newRawDecodes':0,'availableHomeSource':manifest,'homeSourcePngSha256':hashlib.sha256(crop.read_bytes()).hexdigest(),'missingCompanyPanels':True},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'viewer':str(out/'index.html'),'selectedRows':len(selected),'newCandidateRuns':0,'missingCompanyPanels':True}))
