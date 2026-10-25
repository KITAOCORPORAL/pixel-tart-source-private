# 自有策划参考发送自由画布 · 家用交付

2026-10-24 America/Phoenix（客户端日期；实验目录沿用20261025，原始主机/UTC时间保留原值）。**限定产品/自动回归完成；原生桌面闭环受工具阻断，未执行；不能发行，无安装包。** 原始主机日志的本地时区与UTC/epoch原值保留。

## 起点与保全
已fetch并独立ls-remote核对company/planning-bounded-thumbnails-20261023 = dad3dd4c76d99df8b93805843cbe6cd69d1abe36；公司产品e6ddb8d1018713fb48fbaa7e7cf3a3b23df0cc30，产品/HEAD的src/tests完全相同。已读FINAL_REPORT、TEST_SUMMARY、PERFORMANCE_COMPARISON和指定实现。从此HEAD建立独立N:/pixart/guangjing-home-owned-canvas-20261025、分支home/planning-owned-canvas-20261025。

19旧工作树HEAD/branch/status读回不变，原主树211项未提交条目清单保留；fetch后未发现本地分支独有未推送提交。只做状态核对，不重复旧ZIP/全盘ignored文件扫描。旧树、数据根、诊断包未改写；无reset/clean/stash/rebase、main合并或强推。

## 产品
产品提交5fb701cc268d5d054224014c2c670fbab023806b；最终修正8886226d07185ed13db1c4cd5dfce3e9ccdd8417。

- CanvasObject新增CanvasPlanningSource：保存明确的本地PlanningRoot及PlanningProjectCopy（项目/文档/引用ID、规范相对路径、大小、SHA-256、原来源仅作记录）。自有来源SourcePath为空，不能混入原外链或PreviewPath。
- SendToCanvas重新取当前项目当前引用；发送前核对项目/文档/引用与实际副本大小/hash。保留标题、Library/Asset身份、引用ID及原320×240布局/350×280网格；重复发送生成独立画布，不覆写已有画布。非自有参考不再将缩略缓存当原来源。
- 画布加载、放大和策划快照通过同一公司有界PNG/JPEG解码器、同一副本读lease校验，不经过通用离线preview provider。副本缺失/大小或hash变化会清除旧图并显示“项目自有副本不可用（未回退原外链/缓存）”。重开时仍从持久化身份重新绑定。
- 画布异步预览增加来源key及请求代次，迟到的外链结果不能覆盖切换后的自有图；保留128项/192MiB已有内存预算。不能仅凭ObjectId复用不同来源。
- CanvasDocument的运行时StorageRoot不进JSON，由CanvasDocumentStore.Load绑定；从策划打开画布仍保存原策划画布目录，不再落到素材库另一默认目录。用户可以继续调整画布关联项目；对象仍保存明确的原自有来源身份。
- 自有画布对象不能被“从画布添加策划参考”旧入口悄悄降级为外链，明确提示回原策划使用；素材库/账本/档期不伪装成已重连。

公司PlanningOwnedReferenceImage实现下移到WPF模块OwnedPlanningReferenceImage，主程序保留兼容facade。独立文本核对：除namespace/type名称外与e6ddb8d1实现逐字相同；未改JPEG EXIF、尺寸准入、WIC参数/重采样、ICC/RAW/ColorStudio节点。现有固定Pillow/ImageMagick16组方向回归继续通过，不引入新库/算法。详见DECODER_EQUIVALENCE与公司NODE_COMPARISON。

## 项目包边界
.ptplan v1只包含策划/镜头/已拥有图片副本；自由画布工作区JSON不在包内。现有VisualLinks仅列出“自由画布依赖未包含”，本轮明确不扩展包范围。CanvasPlanningSource里的绝对PlanningRoot是同机持久引用，不是跨机可移植承诺。测试实际导出并确认无canvas条目，排除清单仍有自由画布。副本/数据根搬家需后续显式重定位，本轮不扫描私人目录。

## 最终构建与回归
Windows x64完整自包含主程序，WholeAppDiagnosticBuild=true，PhotographerTrialBuild=false；SDK10.0.401。290应用文件/264运行时资产独立读回无缺失，Core/WPF测试中的产品DLL绑定发布目录逐hash一致。

|受影响范围|通过|失败|条件未执行|
|---|---:|---:|---:|
|Core策划/自由画布/存储|65|3|0|
|WPF策划/自由画布/存储|133|0|4|

新增Core2、WPF5（其中1项为合成样例生成）均包含在表内，不重复相加。覆盖原外链删除、预览缓存删除、保存重开、副本变化/丢失清旧图、通用preview provider零调用、同一参考重复发送、两个明显不同项目切换、同ObjectId迟到外链结果不能覆盖新自有来源，以及包不包含画布。

Core3失败均在创建合成符号链接时IOException“客户端没有所需的特权”，未改策略、不记PASS。WPF4未执行是已有原生/物理DPI和大图性能opt-in，未为了凑绿开启/重测公司全部性能。

首轮真实发现：副本InvalidDataException未被画布捕获，导致错误抛出而非清图提示；已修。早期测试引用不存在CanvasEditor.Load导致构建失败，改用现有Transform/项目切换。一次重复生成样例命中已有.ptplan，产品正确拒绝覆盖，保留失败并在全新final-r2目录完成最终测试。全部失败和原TRX保留RAW_EVIDENCE，未覆盖旧目录。

## 原生桌面：BLOCKED，不能用VM替代
工具原始错误：`unsupported call: mcp__cua_repljs`。尝试工具发现/重置也返回unsupported，故未循环截图/盲点。最终EXE可实际启动PID29788，OS读回响应窗口、完整诊断标题和实际加载模块hash；在新final-native-runtime数据根启动，仅对本轮进程CloseMainWindow并确认退出。

这不是原生鼠标验收。当前根导入、普通列表打开、JPEG参考/镜头缩略图/放大、镜头外链导入副本、编辑保存/正常UI退出/新进程重开、15区导出、另一隔离根导入查看及快速A/B切换均未执行。已提供MANUAL_RETEST和两份明显不同合成项目。自动测试/ViewModel重建不替代本闭环。

## 家用Scenario11待项
仓库已有有效家用原始复测，已独立打开RAW_EVIDENCE.zip的TRX与identity/performance.json：source4f57ca56、10000次切换、30000ms原门槛、实际1292.9921ms、Passed，100000元数据/90000unique位置未变。本轮引用，不重复，也不把它说成当前canvas提交的耗时；原历史卷指纹限制仍保持。HISTORICAL_HOME_SCENARIO11记录原文件hash/路径。

## 交付与限制
完整自包含诊断ZIP、隔离启动器、合成A/B包、公司源码测试在家生成的JPEG种子/自有包和15区DOCX、原始结果及改动清单附带。公司原始包二进制不在该批Git树里，交付中的JPEG包是本机运行公司提交测试重建，非公司原包逐字节副本。无用户数据库/私人素材，无Setup。

画布来源为本机持久绝对根，工作区不随ptplan跨机；PNG/JPEG白名单不是杀毒/通用色彩精度认证；副本变化只在重新请求加载/重开时重新校验，不宣称实时文件监视。自有图画布当前用1024px有界预览，大图窗口上限4096，未重做公司24MP性能比较，不宣称更快。已有素材抽屉列表的自有来源只保留身份/名称，缩略绑定不回退私有路径；后续可扩展专用抽屉缩略适配。原生闭环仍未完成，不能发行。
