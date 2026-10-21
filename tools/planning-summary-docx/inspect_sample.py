import pathlib,zipfile,xml.etree.ElementTree as E,json,argparse,hashlib
p=argparse.ArgumentParser();p.add_argument('--sample',required=True);p.add_argument('--missing',required=True);p.add_argument('--out',required=True);a=p.parse_args();ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
def read(path):
 with zipfile.ZipFile(path) as z:
  root=E.fromstring(z.read('word/document.xml'));headings=[''.join(p.itertext()) for p in []]
  headings=[]
  for p in root.findall('w:body/w:p',ns):
   style=p.find('w:pPr/w:pStyle',ns)
   if style is not None and style.get('{'+ns['w']+'}val')=='Heading1':headings.append(''.join(t.text or '' for t in p.findall('.//w:t',ns)))
  return {'headings':headings,'text':'\n'.join(t.text or '' for t in root.findall('.//w:t',ns)),'embeddedImages':len([n for n in z.namelist() if '/media/' in '/'+n])}
normal=read(a.sample);missing=read(a.missing);names=['项目概览','文字','镜头清单与分镜','参考图','情绪板','灯位图','服化道与道具','设备','人员','场地','档期','当天时间表','预算','文件','文档内容'];expected=[f'{i+1:02} {name}' for i,name in enumerate(names)]
if normal['headings']!=expected or missing['headings']!=expected:raise RuntimeError('15 heading order mismatch')
for text in ['转身标记','SYNTH85mm','灯位资源标记','服化道资源标记','设备资源标记','人员资源标记','场地资源标记','时间表资源标记','预算资源标记','关联档期标记','实际支出标记','富文本正文标记','基础表格标记','文档内容尾标记','缺失文件','跨机可能缺失']:
 if text not in normal['text']:raise RuntimeError('Missing content '+text)
if '文档图片不可用' not in missing['text'] or '待缺失图片' not in missing['text']:raise RuntimeError('Missing image status not present')
if 'KitaoShotTableV1:' in normal['text']:raise RuntimeError('Summary accidentally claims old roundtrip marker')
result={'normal':{'headings':normal['headings'],'embeddedImages':normal['embeddedImages'],'sha256':hashlib.sha256(pathlib.Path(a.sample).read_bytes()).hexdigest()},'missing':{'headings':missing['headings'],'missingImageDeclared':True,'sha256':hashlib.sha256(pathlib.Path(a.missing).read_bytes()).hexdigest()},'status':'PASS','scope':'Direct OOXML inspection of actual output; not Word/WPS physical acceptance'}
pathlib.Path(a.out).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
