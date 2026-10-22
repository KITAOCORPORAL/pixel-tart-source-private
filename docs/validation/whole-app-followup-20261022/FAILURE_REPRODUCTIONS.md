# 本轮原始失败与修复边界

2026-10-22 Europe/Berlin；产品/测试源码冻结 13ec9ca1c678622e5c7b884d8f69882c409c33a4。本轮没有修改 src/tests 或像素算法，没有放宽测试、超时或性能门限。

## Core首轮发布路径合同

1. 将最终发布DLL逐hash复制到测试宿主。
2. 错将 PIXEL_TART_BATCH12_RELEASE_CORE 设置为原发布目录位置。
3. ReleasedCoreContainsStrictImporterAndKeepsCreativeFilmEvidence 以加载路径字符串比较失败；原FULL_CORE完整TRX保留（1717 PASS / 1 FAIL / 5 NOT_RUN）。
4. 仅把变量改为已加载测试宿主同hash Core的路径，并给性能输出分配新目录；完整Core重新执行。不能将补验拼入原成绩。

## 驱动启动错误

首次复验包装器误把Python executable重复两次，Python读取EXE报Non-UTF-8 SyntaxError。未发现/执行测试，非产品失败。DRIVER_SYNTAX_*完整保留。移除重复argv后整套Core新run执行。

## 合成策划补验路径

1. KITAO_PLANNING3_OUTPUT 传 E:/ 正斜杠证据根。
2. 产品以规范Windows绝对路径保存附件；旧测试将未规范的attachment原字符串与回读路径直接比较，补验出现1 Failed，原完整WPF不受影响。
3. 原样保留补验；新目录通过 pathlib.Path.resolve 规范变量后，同DLL同断言15/15。没有改产品以保持非规范路径，也没有删旧证据。

## 逻辑DPI第一次取证

106状态中的最终3840×2160/200% AssetLibraryGrid 在原60秒限制超时；前105张保留，无完整manifest，不复制旧manifest刷绿。全部测试结束后，以同一个UiReview EXE、同一个生产器、同60秒限制和全新目录完整复验。复验单列，不拼接105张；若仍失败仍须阻断。

## 实体桌面

原完整包及本轮新EXE分别启动。最终EXE真实可访问性树观察到10个一级导航及工具箱/设置等；点击退出教程仍报 coordinate input geometry is unavailable。刷新窗口后专用截图仍报 FrameArrived timed out。未修改settings绕过教程、未用RaiseEvent/API/RenderTargetBitmap伪造人工实机列。

## 文档独立渲染

render_docx.py 本轮尝试失败：LibreOffice soffice.exe was not found on PATH。产品回读分页PDF及独立Poppler PNG仅用于文档布局检查，不能代替Word/WPS打开编辑往返。

## 继承修复而非重复实施

原策划ProductionPeers ValuePattern失败、R3固定目录复用失败已在现有后续祖先提交修复；本轮直接从单一后续分支冻结重建，未merge/cherry-pick。15区参考分类、只读汇总DOCX、自有图验证、keepNext与素材库缓存切换均为继承实现，逐提交见INHERITED_COMMITS.json。其他脏owned-resource/desktop-entry工作树只读，未算入包。
