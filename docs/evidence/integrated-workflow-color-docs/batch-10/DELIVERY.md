# 第十批交付总表

工作树：D:/AI AGENT/pixel-tart-batch10-20261012。
分支：feature/pixel-tart-batch10-raw-current-edits-20261012。
起点：51b3f4f891a36acb6bd4f8a3a2e0dd83340bd1c2。
发布源码：061c4cf57a72e59f676142ae629ff7b11cd5fb99。

状态：旧RAW CurrentEdits已接通并验证；真实输入覆盖和性能质量仍为PARTIAL，不把单机成功称为全部闭环。

|项目|实际交付|状态|
|---|---|---|
|环境保全|新D盘工作树；其他dirty/ahead不动，无强推/自动合并|完成|
|旧RAW当前编辑|PV6独立已渲染输入；完整Look/stack/old film/来源版本/输入合同；初次复用冻结源，重开完整重建|合成及Canon真实验证通过|
|历史兼容|PV4/PV5及即时V1/V2计算未改；JPEG来源冻结回归修正|完整回归见TEST_MATRIX|
|完整测试|Core1651/0/6，WPF1540/0/19；发布Core全量1651/0/6|通过，跳过不当通过|
|拒绝与保全|MatchV4Beta、缺Look参考节点、非法版本/参数/源身份显式拒绝；取消不迟到提交|专项通过|
|真实输入|Canon5496x3670三格式/HQ/块/保存重开/Publishing；Sony解码失败；Fuji7752x5184重建预算拒绝|PARTIAL：并非两种相机全链成功|
|真实JPEG输入|授权项目目录没有JPEG输入；RAW→JPEG输出独立列出|NOT_RUN|
|大图多VM|两个真实工作区VM使用Canon，HQ/块排队、重开、发布、淘汰、预算拒绝、切图、关闭导出、释放|路径验证通过；不是桌面窗口验收|
|内存|账本/工作集/private/峰值分列；最终账本0，进程峰值4905312256 bytes|实际内存边界仍开放，不是2GiB OS上限|
|性能|分离hash/decode/rebuild/instant/HQ/block及组合写出/验证；无参考节点省无用分析图；取消轮询|未完成纯codec拆分/旧二进制受控前后基准|
|关键帧|SX-70/i-2各6张，96dpi离屏，合成图；未改相机/4秒时序|离屏证据完成；人眼/物理DPI未批准|
|Release|win-x64自包含，主DLL/Core/模块与隔离发布测试hash相同|发布专项5/0/0；EXE未启动|

EXE：D:/AI AGENT/pixel-tart-batch10-20261012/artifacts/batch10/release-final/win-x64/KitaoPhotoSelector.exe。
SHA256：844503D31AFEB5897F44C0215C4CA7557ABEF1EAF6BAC57C7C8562A5ED36B8D1。
产品版本：2.3.0-batch10+061c4cf57a72e59f676142ae629ff7b11cd5fb99。

没有浏览器/WebView/Computer Use/产品EXE自动启动。无照片、数据库、凭据或发布二进制提交。关键帧仅含合成渐变。

真实标定资料继续缺失；创作预设不升级为准确相纸复刻。专业HDR/高光恢复与ICC、仿色能力、策划中心Word和自由画布、浏览器扩展、手机APP、档期日历三端同步继续保留路线。
