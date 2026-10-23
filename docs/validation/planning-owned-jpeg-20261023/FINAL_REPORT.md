# 项目自有JPEG策划呈现

2026-10-23 Europe/Berlin。**限定产品自动验证PASS；原生JPEG预览及镜头副本保存/退出/新进程重开BLOCKED，整体PARTIAL，不能发行、无安装包。**

## 基线与实现

fetch+独立ls-remote核实company/planning-shot-link-lifecycle-20261023=d44b48fe0d0825e067d9dc909cf48d3481226935；已读FINAL_REPORT/RELEASE_GATE及预览/汇总代码。新工作树E:/Kitao-Worktrees/planning-owned-jpeg-company-20261023，分支company/planning-owned-jpeg-20261023；48旧树、423脏文件、旧数据根和ZIP保全，无reset/clean/stash/rebase/main合并/强推。

产品 **4997c3059c5737405921d88ba6fd74eb2be2496e**：PlanningOwnedReferenceV1统一自有参考读入，原ProjectCopy当前根路径/身份/重解析/1..64MiB/SHA-256先校验，再做PNG/JPEG头和尺寸准入。没有存在原外链就绕过副本验证，没有改源文件/receipt/旧PNG文档往返。JPEG baseline/progressive8位灰度/RGB白名单；最长边16384、总像素4000万、单帧。EXIF IFD0有界端序/偏移/类型/重复/值1..8校验，整数像素置换，不重采样。方向5..8宽高交换。

文档参考、镜头独有收据、异地包导入从同一PlanningProjectCopy判断来源。参考区缓存每次先重新验副本/解码，再写PlanningOwnedReferenceV1目录的方向归一化PNG；损坏/变化/丢失不显示旧缓存、不回退外链。镜头网格和抽屉使用相同预览。放大自有图不再从OriginalPath直接LoadImage，而是同一安全reader重新校验。其他格式/坏EXIF/坏JPEG保持副本、明确“已保存但无法预览”，不会阻止项目打开。

15区汇总自有PNG/JPEG使用同reader/方向结果，实际图片由原DOCX Adapter编码入包；普通图片附件仍只有名称/状态，不冒充参考图。旧文档PNG路径与PlanningDocxAdapter/Pages/PDF往返组件未改变。Color Studio/RAW/PV4/PV5/PV6未改，不作通用JPEG色彩/ICC精度承诺。

## 准入与独立对照

见NODE_COMPARISON.md与REFERENCE_PROVENANCE.json。Pillow11.3.0固定commit89f1f4626a2aaf5f3d5ca6437f41def2998fbe09、ImageMagick7.1.2-34固定commita0c4bf729a26a5c2feb28b0365b47a8db736078a，具体entry/许可/执行identity已记录，研究工具不入发布包。

合成80×48四象限RGB JPEG，quality95/无色度子采样、EXIF1..8；与两独立成熟实现16组比较最终maxAbsolute8bit=0/meanAbsolute8bit=0。仅几何/合成8位显示证据，不扩大为真实RAW/ICC/物理颜色科学通过。CalibrationEligible=false。原始参考解包/网络失败日志本地保留，最终运行版本已成功固定。

## 最终发布组件测试

|范围|程序集发现|选择/实际|通过|失败|跳过|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|受影响Planning/ProjectShot Core|1758|63/63|63|0|0|0/0/0|
|受影响Planning/ProjectShot/JPEG WPF|1740|95/95|95|0|0|0/0/0|

29发布DLL与测试宿主hash绑定及最后读回一致；新4WPF包含在95内，不重复计分。原始adapter发现/计划/testId-FQN-displayName多重集合=结果，TRX/runsettings/stdout/stderr留RAW_TESTS.zip。只相关回归，无重复全盘产品扫描。

新专项覆盖八方向、原PNG、文档参考/镜头收据/缩略图、保存VM重开、项目包异地根导入、坏文件、超限/截断头、EXIF错误、附件身份拒绝、hash被替换/副本丢失且原外链仍存在不回退。首轮3/4失败为新测试只修改小端EXIF但样本大端，实际未改坏；原失败保留。修正双端序变异并断言确已变更后4/4通过，无旧断言放宽。

## 实际DOCX读回

最终输出jpeg-png-15-section-summary.docx：15个Heading1完整顺序，4个实际媒体/drawing，分别镜头区1图、参考区3图。JPEG方向6/8的媒体48×80，PNG80×48；独立Pillow读取DOCX媒体逐像素匹配固定参考PNG（非只计数），JPEG附件没有额外嵌图。OpenXML validator无错误。异地包导入自有JPEGhash一致、本机外链批准清除；VM重建不当进程重启。见DOCX_READBACK.json/VERIFIED_OUTPUTS.json。

## 一次真实EXE尝试

按要求先用本轮最终EXE PID25732与native-jpeg/runtime隔离根尝试，已备合成JPEG种子包和方向6替代文件。实际控件树读取成功；退出教程真实点击报coordinate input geometry is unavailable，刷新选择后唯一一次专用截图报FrameArrived timed out。之后停止空跑，继续自动回归。7实际加载模块hash匹配最终包。

原生JPEG副本预览、镜头副本保存/退出/新PID重开、原生导出/异地导入均NOT_RUN/BLOCKED；Word/WPS未执行。未改settings绕过教程、未模拟点击刷人工列，未继承家用“继续外链”模式旧实机成绩。

## 交付与限制

完整Release win-x64 self-contained主程序，WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，290app文件/264运行资产缺失0，统一隔离UserData启动器，不含DB/私人输入。样例含合成JPEG/PNG、镜头独有缺源v1包、自有图包及实际汇总DOCX。独立公司产品/证据分别提交推送，读回另列。

CMYK/12bit/多帧或其它图片格式目前不可靠呈现，明示已保存但无法预览；不做杀毒或所有JPEG变体认证。64MiB/40MP内解码仍有内存成本，UI缩略图当前使用受限全解码后缓存，不声称超大图库性能验收。既有源读/预览显示与专业ICC全链不同；正式发行/安装/物理DPI仍未闭合。

## 最终诊断ZIP

328文件，74959514字节，SHA256 72c3a9aa0561d0bd3d246297b7793892e2bfc66d22e9345c68cf585274b4dd4c，全部条目读回hash一致。ZIP内报告是打包时快照，外部报告后附ZIP身份避免自引用。不含DB/UserData/私人输入或独立参考运行器。
