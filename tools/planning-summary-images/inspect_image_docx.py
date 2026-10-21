import pathlib,zipfile,json,argparse,hashlib,xml.etree.ElementTree as E
p=argparse.ArgumentParser();p.add_argument('--sample',required=True);p.add_argument('--identity',required=True);p.add_argument('--status',required=True);p.add_argument('--out',required=True);a=p.parse_args();identity=json.loads(pathlib.Path(a.identity).read_text(encoding='utf-8-sig'));ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'};rows=[];headings=[]
with zipfile.ZipFile(a.sample) as z:
 root=E.fromstring(z.read('word/document.xml'));text='\n'.join(t.text or '' for t in root.findall('.//w:t',ns));section=0
 for block in root.find('w:body',ns):
  style=block.find('w:pPr/w:pStyle',ns)
  if style is not None and style.get('{'+ns['w']+'}val')=='Heading1':
   title=''.join(t.text or '' for t in block.findall('.//w:t',ns));headings.append(title);section=int(title[:2])
  for drawing in block.findall('.//w:drawing',ns):
   prop=next(x for x in drawing.iter() if x.tag.rsplit('}',1)[-1]=='docPr');extent=next(x for x in drawing.iter() if x.tag.rsplit('}',1)[-1]=='extent');meta=json.loads(prop.get('descr')[len('KitaoImageV1:'):]);rows.append(dict(section=section,imageId=meta['DocumentImageId'],title=meta['Title'],caption=meta['Caption'],documentId=meta['DocumentId'],width=float(extent.get('cx'))/9525,height=float(extent.get('cy'))/9525))
 images=[n for n in z.namelist() if '/media/' in '/'+n]
 names=['项目概览','文字','镜头清单与分镜','参考图','情绪板','灯位图','服化道与道具','设备','人员','场地','档期','当天时间表','预算','文件','文档内容']
 if headings!=[f'{i+1:02} {name}' for i,name in enumerate(names)]:raise RuntimeError('15-section heading order differs')
 expected={2:[identity['a'],identity['a'],identity['b'],identity['c']],4:[identity['b'],identity['a']],5:[identity['c'],identity['a']],6:[identity['a']]}
 if len(rows)!=9 or len(images)!=9:raise RuntimeError('Unexpected picture count')
 for section,ids in expected.items():
  if [r['imageId'] for r in rows if r['section']==section]!=ids:raise RuntimeError('Section picture identity/order mismatch '+str(section))
 if any(r['width']>520.01 or r['height']>360.01 or r['documentId']!=identity['document'] for r in rows):raise RuntimeError('Image size/document identity mismatch')
 for token in ['长正文开头标记','长正文末尾标记','中文图注甲','中文图注乙','中文图注丙','缺失灯位图','文档图片不可用','外链只列状态','未读取库当前原件']:
  if token not in text:raise RuntimeError('Missing picture/text disclosure '+token)
with zipfile.ZipFile(a.status) as z:
 status=E.fromstring(z.read('word/document.xml'));statusText=''.join(status.itertext())
 for title in ['变化副本','不支持格式','超大副本','跨文档副本']:
  if title not in statusText:raise RuntimeError('Missing explicit refusal '+title)
 if any('/media/' in '/'+n for n in z.namelist()):raise RuntimeError('Unsafe sample unexpectedly embedded image')
result=dict(status='PASS',scope='Direct inspection of actual OOXML; not physical Word/WPS acceptance',headings=headings,drawingCount=len(rows),mediaParts=len(images),drawingPositions=rows,statusSampleExplainsAllFourRefusals=True,docxSha256=hashlib.sha256(pathlib.Path(a.sample).read_bytes()).hexdigest());pathlib.Path(a.out).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='drawingPositions'},ensure_ascii=False))
