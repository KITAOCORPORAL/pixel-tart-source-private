# 自由画布拼贴首闭环＋宽域影调收尾 — PARTIAL / 不能发行
日期：2026-10-28 America/New_York。

## 接续与提交
同一独立树E:/Kitao-Worktrees/r53-color-tools-film-20261028、分支company/r53-color-tools-film-20261028；fetch确认8816b5f2无新增。基线产品2814d49b继承，不reset/clean/stash/rebase旧树，不合main/强推/整分支回灌C1。
产品首提交2c6697b78bdcbcbefa6268d550b89228ad9a5a1d，最终产品 **eeb5edae19870bb9da151c6c15164f381faf4136**（补空白用户入口）；最终远端见PUSH_READBACK.json。

## 实际完成
1. 调色：固定darktable5.2.1/RawTherapee5.12原tone-stage函数编译执行51/42点，域/参数/输出转换和曲线差异留TONE_NATIVE_REFERENCES/REFERENCE_CURVE_DIFFERENCES，不拿产品公式当成熟参考。部分RT域外NaN保留，不凑滑块相等，也不声称完整双相机引擎科学通过。
2. 找到V1极端负对比+正阴影灰阶反转及暖向问题；**新增WideToolVersion2**修正，27极端组合×3000灰阶单调、18%灰/正负方向/暗高亮/负和>1/色比/蒙版回归；旧V1数学和继续编辑不变，颜色管理提供另建新版变体，不偷偷改旧项目。高光/阴影始终不是前端恢复。
3. 高频饱和边界、细硬mask、强WB/tone、多节点、旋转/拉直风险主动精细回退，固定快速误差预算不抬；场景分别测试，fallback逐位与精细相同。RawWidedefaultoff、XTrans/内存guard不动，RAW_FRONTEND_NEXT写明实际clip阶段/接口/兼容代价。C1仍NOT_RUN、不拟合LUT。
4. **画布真实产品闭环**：素材库更多→新建自由画布无需选素材；原放入选择/已保存入口保留。顶栏导入PNG/照片、自有copy/hash身份、选择/移动/等比或Alt自由缩放/旋转、图层顺序/显隐/锁/透明度、手势undo/redo、原子保存/新进程重开、按文档尺寸透明PNG+sRGB ICC导出。Zoom/Pan不写文档坐标；锁定快捷键/手柄保护，缺源清旧图/阻止导出、同SHA恢复。
5. **最小普通画笔**：独立同CanvasObject/CanvasEditor层，颜色/大小/不透明度/硬度、1笔1历史、保存重开/导出。schema2/StrokeV1明确版本；同笔折线max覆盖、回折不反复乘alpha，不同笔source-over；非纹理笔刷。
6. PNG不走照片白底。float premultiplied bilinear/source-over最后一次unpremultiply修复低alpha黑白边；原边缘断言未放宽。源嵌ICC→sRGB，显示器转换不进输出；画布64MP/16384显式预算、逐层解码不全图无限缓存。旧.ptplan v1不改，不能说包含canvas。
7. 胶片只修最近资源真正MRU排序，搜索不改选中seed/冻结配方；既有草稿/显影锁/seed/取消/重拍继承并跑相关5项，不重造。授权扫描/联名单列，不算占位正式资源。

## 本机验证
- 最终发布DLL29项绑定一致；受影响 **Core13PASS/WPF23PASS/0FAIL/0NOT_RUN**（不是全历史全套）。基础WPF严格22PASS/1NOT_RUN状态FAILED保留，第二独立testhost补跑1PASS才去重统计。
- 画布firstPID32012、独立reopenPID34904，透明输出像素hash精确相同。160×120文档PNG实际解码width/height/alpha/sRGBICC验证，屏幕960×640/不同Zoom不改变文档或导出；贴纸边缘色彩、组合变换、锁、一次gestureundo、缺源/恢复、取消无半成品、1笔128alpha/2笔192alpha实际测。
- 53工作树中当前实施树之外52旧树HEAD/status/dirtyhash全部保全。完整最终EXE仅OS窗口/进程身份+WM_CLOSE退出0，不是原生鼠标编辑。canvas-offscreen.png仅RenderTargetBitmap。
- 没有ComputerUse/Browser/WebView/CDP；物理鼠标/DPI/多屏ACM/HDR未验。科学准入OPEN、CalibrationEligible=false，不把PNG/TIFF一致性当色准。

## 完整交付
E:/Kitao-Deliveries/r53-canvas-tone-closure-20261028/Guangjing-R53-Canvas-Tone-Final-Diagnostic-win-x64.zip
同名解压根Start-Guangjing-Diagnostic.cmd（默认RawWideoff）；研究CMD保留明确开启。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全部模块和策划15板块保留，包内UserData隔离，不改策略，不生成Setup。
295文件、74420892bytes，逐项SHA读回一致；ZIP SHA256 **6fb090f9fdab910ab38e91e94be4d42c0f6605a02cde812ff79c9101f90abad0**。APP_FILES含路径/类型/大小/hash，不含UserData/数据库/原素材。RAW_TESTS.zip原TRX/失败/脚本与manifest；研究GPL源码/编译器不随产品包。

## 限制与下一步
首闭环可用但非R5.3全部完成：大画布性能/纹理笔刷/跨机根重定位/完整视觉精修和实机验收仍缺。继续纹理素材笔刷/更复杂mask须接同对象/历史模型；影调完整相机/profile科学准入与RAW浮点前端是明确后续，不扩大成无尽泛调查。旧教程保持退役，稳定后统一产品引导。

## 普通推送读回
产品eeb5edae与独立证据20d745ac已正常push；ls-remote读回LOCAL_HEAD=REMOTE_HEAD=20d745ace4e0bb7c6306d88cb5a938aaceca6d12，工作树干净。后续仅封存本记录，不改变产品src/tests。
