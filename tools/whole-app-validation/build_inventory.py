import pathlib,xml.etree.ElementTree as E,re,json,csv,argparse,hashlib
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True);a=p.parse_args();root=pathlib.Path(a.root);out=pathlib.Path(a.out);out.mkdir(parents=True,exist_ok=True)
rows=[];ns={'x':'http://schemas.microsoft.com/winfx/2006/xaml'}
def local(s):return s.rsplit('}',1)[-1]
def module(path):
 path=path.replace('RAWSelectionAssistant','ProductHost')
 if 'FreeCanvas' in path:return '自由画布'
 if 'AssetLibrary' in path or 'PixelTart.Modules.AssetLibrary' in path:return '素材库'
 for k,v in [('Planning','策划中心'),('Tether','联机拍摄'),('Reference','Color Studio/参考仿色'),('PhotoGrade','暗室R3'),('Darkroom','暗室R3'),('Finance','摄影收支'),('Selection','在线选片'),('Calendar','日历'),('Publishing','发布导出'),('Collage','拼图'),('Compress','压缩'),('Raw','RAW转档'),('Organize','整理图片'),('Grouping','整理图片'),('Watermark','水印'),('Workbench','工作台'),('History','项目历史')]:
  if k in path:return v
 return '主壳/共享/工具箱'
def add(path,line,kind,label,target,visibility='CONDITIONAL',context=''):
 rows.append(dict(ActionID='ACT-%05d'%(len(rows)+1),Module=module(path),Page=path,Kind=kind,Label=label,Target=target,Source=path+':'+str(line),Context=context,BuildIncluded='PARTIAL',InclusionReason='Source compiled/resource candidate; dynamic visibility/feature gate and route reachability require per-entry runtime confirmation',Operation='NOT_RUN',AutoTest='NOT_RUN',ManualDesktop='NOT_RUN',Evidence='',Issue='VAL-003',Blocker='Not exercised in current full EXE; source binding is not business PASS',Status='NOT_RUN'))
for f in sorted((root/'src').rglob('*.xaml')):
 if any(x in f.parts for x in ['bin','obj']):continue
 path=f.relative_to(root).as_posix();text=f.read_text(encoding='utf-8-sig');lines=text.splitlines()
 try:tree=E.fromstring(text)
 except E.ParseError:continue
 parents={child:parent for parent in tree.iter() for child in parent}
 for element in tree.iter():
  tag=local(element.tag);attrs={local(k):v for k,v in element.attrib.items()};label=attrs.get('Content') or attrs.get('Header') or attrs.get('ToolTip') or attrs.get('Name') or tag
  for event,target in attrs.items():
   if event in ['Command','Click','MouseDoubleClick','PreviewKeyDown','KeyDown','Drop','SelectionChanged','Checked','Unchecked','ValueChanged','TextChanged','ContextMenuOpening'] or event.endswith('.Command'):
    token=event+'="'+target+'"';line=next((i+1 for i,s in enumerate(lines) if token in s),1)
    add(path,line,tag+':'+event,label,target,attrs.get('Visibility','CONDITIONAL'),attrs.get('CommandParameter',''))
    ancestors=[];current=element
    while current is not None:
     ancestors.append({local(k):v for k,v in current.attrib.items()});current=parents.get(current)
    contexts=' '.join(str(x) for x in ancestors)
    for marker,domain in [('IsWorkbenchPage','工作台'),('IsWorkflowPage','归片工作区'),('IsLocalSplitPage','新建分片'),('IsHistoryPage','项目历史'),('IsWorkCalendarPage','日历'),('IsPlanningPage','策划中心'),('IsTetherPage','联机拍摄'),('IsFinancePage','摄影收支'),('IsOnlineSelectionPage','在线选片'),('IsRawToJpegPage','RAW转档'),('IsBatchCompressPage','压缩'),('IsPublishingPage','发布导出'),('IsReferenceColorPage','Color Studio/参考仿色'),('IsWatermarkPage','水印'),('IsPhotoGroupingPage','整理图片'),('IsPhotoOrganizePage','整理图片'),('IsCollagePage','拼图'),('IsToolboxPage','工具箱'),('IsSettingsModalOpen','设置'),('IsActivationPage','授权'),('IsHelpPage','帮助')]:
     if marker in contexts:rows[-1]['Module']=domain;break
    rows[-1]['Context']='; '.join(x.get('Visibility','')+' '+x.get('DataContext','') for x in ancestors if 'Visibility' in x or 'DataContext' in x)
for f in sorted((root/'src').rglob('*.cs')):
 if any(x in f.parts for x in ['bin','obj']):continue
 path=f.relative_to(root).as_posix()
 if not any(x in path for x in ['View','Page','Menu','Catalog','Policy','Registry']):continue
 for n,line in enumerate(f.read_text(encoding='utf-8-sig').splitlines(),1):
  if re.search(r'new (?:MenuItem|Button|KeyBinding)|(?:AsyncAction|ActionButton|AddAction|MenuAction)\(',line):
   label=re.search(r'(?:Header|Content)\s*=\s*"([^"]+)"',line);target=re.search(r'Command\s*=\s*([\w.]+)',line)
   add(path,n,'Dynamic control candidate',label.group(1) if label else '',target.group(1) if target else line.strip()[:250],context='Candidate only; never promote an internal method into a visible action')
# stable source candidates, not test-derived product status
(out/'ACTIONS.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
with (out/'ACTIONS.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
print(json.dumps({'actions':len(rows),'modules':sorted(set(x['Module'] for x in rows))},ensure_ascii=False))
