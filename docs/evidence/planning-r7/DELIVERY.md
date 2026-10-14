# Kitao Studio 策划中心 R7 交付

日期：2026-10-14。**状态：PARTIAL，可运行审阅构建；不是“全部完成”，完整 WPF 门禁未关闭。**

## 基线、工作树、提交
- fetch 核验指定远端 `fix/kitao-planning-rich-flow-r6-20261014` 为 `62f06f3b5d9691e605b88c8944346bb96d59a3ac`；结束前再次核验未前进。
- 独立工作树 `D:/AI AGENT/kitao-planning-r7-20261014`；分支 `feature/kitao-planning-r7-20261014`。原R6及其余工作树用户改动保留。
- 页面合同：`1db6d0f`；正文查找替换：`e76e081`；原生表格合并：`c20b6ef`；持久化页面与共用分页器：`fd906bb`；面板/Esc/打印回归：`9446c19`。
- 最终产品源码 `9446c1997ea88575072f0f9a8b72e4bc95eb8687`。其后仅证据文档提交。
- 未发现适用AGENTS.md；已读取实际源码、R6交付、Grok本地decisions/spec最新条款。没有执行原型HTML。

## 实现结果
1. 非A4实际转A4，保留横竖和比例边距；A4、Letter、A3横向断言实际值与提示。
2. 正文Ctrl+F/H、上一处/下一处、大小写、全词、单次/全部；跨Run、中文、表格、嵌套列表；未改部分格式保留，一次撤销，零命中无历史。范围明确不含摄影结构字段。
3. 合法矩形合并/拆分与内容顺序；不规则拒绝；DOCX原生gridSpan/vMerge；重复表头tblHeader及持久化语义。复杂表内内容明确拒绝，不静默丢弃。
4. 持久化页眉页脚、首页不同、起始页码、首页隐藏、固定第N页/共M页；布局快照进入正文同一撤销栈；原生header/footer/PAGE/NUMPAGES；预览/打印/PDF共用分页器。
5. R6的24镜/22资源/图文表格/5页样例继续验证；增加设置取消、失败草稿、撤销、重开和DOCX往返。R6编辑器代次和字段合并机制保留。

## 门禁结果（不合并掩盖失败）
|轮次|通过|失败|条件跳过|说明|
|---|---:|---:|---:|---|
|R6基线完整Core|1682|0|7|原始TRX保留|
|R6基线完整WPF隔离汇总|1573|2|24|补齐首轮枚举遗漏；1599例|
|最终冻结完整Core|1682|0|7|源码9446c19|
|最终冻结完整WPF|1583|2|23|209类、1608例、无未完成fixture|
|最终Release实际组件策划专项|32|0|0|7个产品DLL逐项hash一致|
|最终Release实际Core全量|1682|0|7|用发布Core替换独立testhost|

完整WPF两个失败：
- `ClosingOwnerWithSavedCanvasDefersCloseAndCoalescesRequests`：SQLite `no such table: Projects`，R6基线已有，未改色彩或画布产品合同来绕过。
- `A4LetterAndA3LandscapeConvertActualSizeMarginsAndMessage`：隔离Start-Process方式下覆盖原DOCX时`UnauthorizedAccessException`。同源码直接单例与发布专项通过，但隔离方式可复现，原因未闭合。尝试新路径可通过，不接受这个弱化替代，最终源码仍保留覆盖原文件断言。

基线剪贴板用例也曾失败，最终全量通过；不删除历史。初始单进程WPF挂起、60秒超时、发布并发锁等原始失败记录在TEST_EVIDENCE.zip；详情FAILURES_AND_REPAIRS。

## 发布
- 完整目录：`D:/AI AGENT/kitao-planning-r7-20261014/artifacts/r7/release-final/win-x64`。
- EXE：上述目录 `KitaoPhotoSelector.exe`；必须保留完整运行目录。
- 版本：`2.3.0-planning-r7+9446c1997ea88575072f0f9a8b72e4bc95eb8687`。
- EXE SHA256：`242fa0a9d155d1b301cbc2168eecaeeb09becb35da03a7f6e7b3e3b4861f2fd7`。
- win-x64自包含、非单文件；没有安装器；发布二进制不提交；中间release目录不是最终身份。
- `RELEASE_IDENTITY.json` / `RELEASE_FILES.json`为最终发布身份与完整哈希。

## 未关闭范围
- 正文+摄影结构字段复合批量替换及联合撤销未实现，仍按明确范围隔离。普通字段编辑沿用既有同步/保存合同。
- 重复表头与纵向RowSpan同时分页拒绝；超高单行拒绝；普通续表强制另起页，与Grok连续版式有留白差异。
- 表内列表/图片/嵌套表格全面往返未实现。基础页眉不含奇偶页、左右位置、复杂域/胶囊token、复杂分节。完整七页签没有伪造。
- UI使用内嵌设置面板，不是Grok中央modal像素复刻；R6的74项r5差异原样保留，新增对照见VISUAL_REVIEW。
- **Windows实操、物理DPI、WPS实际编辑、真实打印机操作均NOT_RUN**。部分既有完整WPF自动用例创建合成HWND，不等于人工实机验收。XPS、内置图像PDF及OpenXML往返单独报告。
- Color Studio PV4/PV5/PV6及历史计算源码零改动；全量测试结果不被替换为样例成功。

## 交付索引
- FEATURES：FEATURE_STATUS.md
- 迁移/数据/支持边界：MIGRATION_CONTRACT.md
- 文件类型：FILES_AND_TYPES.json
- 失败与原始测试：FAILURES_AND_REPAIRS.md、TEST_RUNS.json、TEST_EVIDENCE.zip、FINAL_WPF_FAILURES.json
- 合成DOCX/PDF：synthetic/；离屏：offscreen/；这些不是生产数据
- 参考身份/差异：REFERENCE_IDENTITY.json、R6_R5_DIFFERENCES_PRESERVED.csv、VISUAL_REVIEW.md
- 重现：REPRODUCE.md

正常推送工作分支，不强推、不自动合并main，不触碰其他工作树；最终LOCAL_HEAD/REMOTE_HEAD以交付回复核验为准。
