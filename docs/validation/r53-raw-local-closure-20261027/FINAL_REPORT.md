# 本轮交付 — PARTIAL / 不能发行

日期：2026-10-27 America/New_York。接续同一分支，不回退家庭，不合main/强推。

## 实际完成
- 线性专业RAW原变体接通ProfessionalRawLocalEdits V1：LibRaw线性ProPhoto D65 → 原PV4曝光/显示映射 → 非线性sRGB共享点工具/蒙版/强度/Normal-Multiply-Screen → 输出。源RAW/master/DecodeKey不变，不转JPEG绕过；旧PV4/5/6缺省扩展null，旧输出不暗迁。
- 同Editor状态贯通简易/专业、变体复制隔离、统一局部/几何撤销、保存重开、单图预览/对比/逐行TIFF/PNG/JPEG输出。裁剪/90度旋转/水平翻转映射回正向未裁源图像素中心，EXIF1–8测试。
- 画笔输入33ms合并、冻结后台、取消/目标/变体/节点/代次保护，覆盖max768；BVH精确距离，限制明确反馈，失焦/换图/锁定取消，移出图像结束笔迹。流量/密度保持V1每笔一次，不偷改连续喷涂。
- 变体对比请求WIC解码期缩放，4帧32MiB缓存/HQ96MiB预算；RAW复用同一个全尺寸master，1200不再被描述成解码内存硬上限。
- 正确R5.3设计包已本机核验SHA，33文件文档/原型读取及截图对照；不执行网页，不采用文档中的旧分支指令。
- Sony样本定位为ILCE-7M4 firmware6.00、15bit三通道YCbCr tiled lossless JPEG，70tiles范围有效，当前LibRaw原生-2；结构完整不等相机校验，未盲更库，不推断全部Sony不可用。

## 源码及完整包
- 基线41265eb4e69569d61c192177eaecca64dd455e51。
- 主产品5fbecc7ecce014bdaa489f5551d985b694488af4，最终性能产品 **091163b12aa29e5c8561b8fa3b3675580d70e885**。
- 分支company/r53-stage-a-completion-20261027；普通push/最终远端见PUSH_READBACK.json。
- 完整包E:/Kitao-Deliveries/r53-raw-local-closure-20261027/Guangjing-R53-RawLocal-Final-Diagnostic-win-x64.zip。
- CMD/BAT同名解压根；完整app/KitaoPhotoSelector.exe，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，包内UserData隔离、不改策略、不是Setup。
- 294文件/74338981bytes，SHA256 **ad1e14ab39594bf21e5c1d88e3227249d4496d28847267b52c54d2de7b28d75f**；逐项哈希读回一致，APP_FILES.json含类型/大小/hash，无UserData/原RAW/数据库。

## 实测（最终发布DLL）
- 24MP坐标域、20笔20k点、512×342覆盖：旧11933.55ms → BVH约379ms（编译约53ms），总采样差约3e-11。覆盖worker不能等同完整图像处理延迟。
- 24MP真实渲染：短20点约7.15s；长2000点6.92s；20k点双层42.04s；TIFF约1.29–1.40s。输入/输出各288MB；5ms采样峰值约1.39–1.69GB，包含运行时/编码开销而非硬上限。
- Canon20MP真实线性RAW：输入处理0.524ms，局部完整预览3.974s，TIFF7.889s，独立读回6.388s。保存重开master哈希一致，预览差0；不是色彩科学准确结论。
- RAW2/3/4全冷对比6.09/8.97/11.84s，热14.61/12.49/12.77ms。优化前四图29.79s；同定义、最多4有界行带、求和次序不变。
- 24MP/48MP普通图2/3/4冷/热完整数值PERFORMANCE.json；48MP四图冷约0.9s、热约10ms。原工作区载入/合成夹具峰值可达数GB，不隐瞒；对比期间起始/采样峰值/分配另列，不能宣称1200限制整进程。
- darktable固定falloff与GIMP固定累积/擦除参考实际执行；相同子集与离散羽化差异分别保留，不硬凑完全一致。旧混合1200组对照保留；NODE_COMPARISON.md/COLOR_SUPPORT_MATRIX.json明确空间/负值/超范围/ICC边界。

## 验证与未完成
最终去重 **Core376PASS/0FAIL/0NOT_RUN；WPF464PASS/0FAIL/4NOT_RUN**。完整范围以TEST_SUMMARY.json/NONPASS.json为准；严格runner对NOT_RUN仍返回FAILED，不写全绿。已自行生成6张24MP夹具补跑策划两项和相关隔离路由；旧授权真实照片/整批样本不改名伪造替代。离屏/testhost与OS进程启动都不作鼠标/DPI验收。

**阶段A仍PARTIAL**：多笔双层42s、RAW冷四图11.84s仍慢；完整R5.3视觉余项、通用ICC/完整独立brush/profile准入、真实指针/DPI未完成。诊断限制未删除。A尚有非外部真实性能/视觉/准入余项，因此B–E未虚称推进，不是把所有后续阶段绑在鼠标门禁上。教程退役/品牌/全部模块和策划15板块保留；51旧树HEAD/dirty/hash未变。
