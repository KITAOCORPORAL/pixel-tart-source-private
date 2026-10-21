import pathlib,argparse,json,hashlib
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader
p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--font',required=True);p.add_argument('--package',default='NOT_READY');a=p.parse_args();out=pathlib.Path(a.out);out.mkdir(parents=True,exist_ok=True);font=pathlib.Path(a.font)
pdfmetrics.registerFont(TTFont('EmbeddedChinese',str(font)))
rows=[
('S00 启动并退出首次教程','用隔离包启动器，不开日常库。确认标题含“诊断预览 / 不能发行”，亲手点击“退出教程”。回传标题与导航截图，EXE路径、系统显示缩放和分辨率；点不到或黑屏则截图停止该分支。'),
('S01 建立双合成库','在新测试目录创建 DESKTOP-A、DESKTOP-B。A导入2张合成图，B导入1张不同图。回传 S01-A / S01-B：库名、路径、实际数量和缩略图，不使用私人素材。'),
('S02 切库与查询','A按文件名查询 -> B查询B文件 -> 回A重复查询；清筛选后应A=2/B=1，不串结果。回传三个步骤截图及搜索词、库名、实际数量；失败保留原状。'),
('S03 新建策划并保存','新建 DESKTOP-合成策划，用虚构信息及T01…T15标记。镜头和资源用现有新增/编辑入口；记录实际修改与保存反馈，不把页面打开当业务通过。'),
('S04 十五区进入及保存','依次：01项目概览 02文字 03镜头清单与分镜 04参考图 05情绪板 06灯位图 07服化道与道具 08设备 09人员 10场地 11档期 12当天时间表 13预算 14文件 15文档内容。每区回传一张含分区名/标记/保存状态截图；不支持字段注明原因。'),
('S05 保存后重开','回列表重开，再正常关闭隔离EXE、同包重启重开。回传概览/正文/镜头/资源/文档内容截图及15区逐项保留或丢失清单。只选合成外链；缺失状态如实记录。'),
('S06 缩放与窗口点击','应用文档缩放100%/125%/150%各点分区与保存，最大化和约1366宽各看一次。回传倍率与按钮布局截图，记录裁切/错位/点不中。应用缩放不是物理DPI，不要求改变系统DPI。'),
('S07 NOT_READY 交包前 / NOT_RUN 交包后未执行 使用本批次新包验证','仅用本批次新包 '+a.package+'；旧包没有本功能。策划更多 -> 导出15区汇总 DOCX，另存新文件；回传菜单/成功反馈/15个标题/末区内容及缺失引用说明截图和合成DOCX。功能包交付并核对身份前保持NOT_READY；已交付不等于已执行。')]
intro='每步回传：编号 + PASS / FAIL / BLOCKED / NOT_RUN + 实际值或错误。仅合成资料；截图前关闭私聊与凭据。诊断版不能发行。'
footer='回传 S00…S07 截图、逐项结果文本和合成DOCX。失败不要删库或关闭安全保护。未亲手操作一律NOT_RUN；离屏图/可访问性树/单元测试不能算鼠标通过。'
corpus='公司手工桌面验证回传清单'+intro+footer+''.join(t+b for t,b in rows);cmap=pdfmetrics.getFont('EmbeddedChinese').face.charToGlyph;missing=sorted({ch for ch in corpus if not ch.isspace() and ord(ch) not in cmap})
if missing:raise RuntimeError('Font missing glyphs: '+repr(missing))
file=out/'MANUAL_DESKTOP_CHECKLIST.pdf';c=canvas.Canvas(str(file),pagesize=A4);w,h=A4;y=h-34;c.setTitle('公司手工桌面验证回传清单')
def text(s,size=9.5,leading=14):
 global y
 c.setFont('EmbeddedChinese',size);part='';width=0
 for ch in s:
  cw=pdfmetrics.stringWidth(ch,'EmbeddedChinese',size)
  if width+cw>w-64:c.drawString(32,y,part);y-=leading;part='';width=0
  part+=ch;width+=cw
 if part:c.drawString(32,y,part);y-=leading
text('公司手工桌面验证回传清单',16,24);text(intro);y-=6
for title,body in rows:text(title,10.8,17);text(body);y-=5
text(footer,9,13)
if y<28:raise RuntimeError('One-page checklist overflow '+str(y))
c.showPage();c.save();reader=PdfReader(file);embedded=[]
for ref in reader.pages[0]['/Resources']['/Font'].values():
 f=ref.get_object();desc=f.get('/FontDescriptor');desc=desc.get_object() if desc else None
 if desc:embedded.append({'name':str(f.get('/BaseFont')),'FontFile2':'/FontFile2' in desc,'ToUnicode':'/ToUnicode' in f})
if len(reader.pages)!=1 or not any(x['FontFile2'] and x['ToUnicode'] for x in embedded):raise RuntimeError('Chinese font not embedded')
(out/'CHECKLIST_FONT_VALIDATION.json').write_text(json.dumps({'fontFileName':font.name,'fontSha256':hashlib.sha256(font.read_bytes()).hexdigest(),'missingGlyphs':missing,'pages':1,'bottomY':y,'fonts':embedded,'S07':'NOT_READY until package delivery; NOT_RUN after delivery until user executes','package':a.package},ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'pages':1,'embedded':embedded,'bottomY':y},ensure_ascii=False))
