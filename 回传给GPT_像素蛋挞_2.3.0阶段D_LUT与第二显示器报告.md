# 像素蛋挞 2.3.0 阶段D：LUT、ICC与第二显示器监看报告

## 1. 阶段D开始HEAD

- 分支：`release/2.3.0`
- 阶段D开始HEAD：`93b7cffe33c11487fe0b9baf1504d1cc9cce83c8`
- 阶段C功能提交：`b36233eeb45c8dad540d89ae1aa42bc49655bf33`
- 阶段D功能提交：`52a40a02529adf02fe8f87bca0817d7ef8fce2ad`
- 本报告生成时HEAD：`52a40a02529adf02fe8f87bca0817d7ef8fce2ad`
- 工作树：报告提交前代码工作树干净；报告与UI证据随后作为独立提交加入。

## 2. 版本、数据库与范围

- 产品版本：`2.3.0`
- 文件版本：`2.3.0.0`
- SchemaVersion：`3`
- 是否修改数据库：否。未新增LUT/ICC/显示器数据库表，未修改Schema迁移；LUT引用、显示器偏好、缓存均使用独立本地文件目录。
- 阶段D未开发RAW显影、LUT写回、LUT批量导出、完整软打样、提醒、工作台新业务或阶段E功能。

## 3. LUT解析器与安全子集

- 解析器：`CubeLutParser`，UTF-8严格文本流解析，限制文件扩展名为`.cube`，拒绝二进制、非有限数值、未知指令、重复尺寸、尺寸越界、数据数量不符和非法域范围。
- 支持子集：`TITLE`、`LUT_1D_SIZE`、`LUT_3D_SIZE`、`DOMAIN_MIN`、`DOMAIN_MAX`、注释、空行，以及纯1D或纯3D RGB数据。
- 尺寸：1D为2–65536，3D为2–65。
- 拒绝类型：1D与3D组合shaper、未知厂商扩展、二进制/私有格式、NaN/Infinity、Malformed、缺失尺寸和超限数据。
- 1D算法：逐通道线性插值。
- 3D算法：三线性插值，按声明域夹紧输入。
- LUT强度：0–100%，按原始代理图与LUT结果线性混合；默认未套用，输入色彩空间默认“未知”。
- 前后对比：支持查看LUT前/后。
- 分屏：支持原图/LUT结果左右分屏和拖动分割线。
- 失败降级：LUT丢失、损坏、超时或取消时回退未套LUT代理图，不停止看守文件夹/接片会话。
- 输入色彩空间提示：支持未知、sRGB显示LUT、Sony S-Log3、Canon Log、Nikon N-Log、Fujifilm F-Log及其他/未知标签；阶段D不执行Log显影。

## 4. 色彩管线与ICC

- 实际顺序：源代理图 → 源嵌入ICC转换至内部sRGB工作空间（代理生成阶段）→ CPU LUT → 强度混合 → 目标显示器ICC转换 → 主屏/客户屏显示。
- 未标记源：按设置中的sRGB或“保持未知并提示”处理，不假装有准确色彩管理。
- ICC检测：Windows `GetICMProfile`获取每台显示器系统默认配置，按显示器StableKey独立缓存配置和SHA-256指纹。
- ICC转换：WPF `ColorConvertedBitmap`，源上下文为内部Bgra32/sRGB，目标上下文为目标ICC。
- ICC失败降级：配置缺失、路径不可访问、文件损坏、API不支持或转换异常时安全回退sRGB，并显示“软件不能替代显示器校准”的免责声明；不修改系统色彩设置，不安装ICC，不写注册表。
- LUT缓存：仅保存不透明SHA-256键命名的PNG代理缓存，键包含资产、代理版本、LUT指纹、输入解释、强度、StableKey、ICC指纹和渲染版本；独立目录、LRU上限、损坏重建，不写SQLite、不进入诊断包。
- 性能/降级：默认CPU代理图；最多2个并发渲染；请求取消和最新请求胜出；单次渲染15秒超时回退原图；当前资产优先，不预处理全部资产。
- 是否引入GPU：否。

## 5. 显示器StableKey与客户窗口

- StableKey：由显示设备标识与EDID身份规范化后哈希生成，不使用显示器数组索引。
- 客户窗口：独立`ClientMonitorWindow`/`ClientMonitorViewModel`/`ClientMonitorCoordinator`，可无边框填充目标显示器边界，Esc/F11只关闭客户窗口。
- 三种跟随模式：跟随主选中、跟随最新、独立锁定；默认跟随主选中。
- 客户屏隐私：默认隐藏文件名、路径、完整EXIF、相机序列号、客户姓名/电话和私人备注；可选显示简化编号、技术摘要、星级；客户屏不提供文件操作。
- 客户收藏和备注：复用现有`TetherAnnotations`保存；备注清空需确认；不记录到日志，不包含完整路径或原始文件名。
- 显示器断开：安全撤回/关闭客户窗口，保留Watch Folder、联机会话、任务和主屏；重连后刷新拓扑，需手动恢复客户窗口。
- 混合DPI：按每台显示器DPI将物理边界换算为WPF逻辑边界，支持横竖屏、4K/1080p及不同DPI缩放。
- 真实物理双屏：当前机器只检测到一个物理显示器；未进行物理双屏测试。自动化显示器拓扑与独立窗口验证通过，真实物理双屏留待阶段E。

## 6. 修改和新增文件

- Core模型、LUT解析/验证/插值、显示器拓扑/ICC偏好及路径：`src/RAWSelectionAssistant.Core/...`。
- WPF CPU LUT/ICC转换、Windows显示器API、色彩ViewModel、客户窗口和联机拍摄页面：`src/RAWSelectionAssistant/...`。
- 阶段D专项测试：`tests/RAWSelectionAssistant.Tests/Version230StageDColorCoreTests.cs`（48）、`tests/RAWSelectionAssistant.WpfTests/Version230StageDColorWpfTests.cs`（28）、`tests/RAWSelectionAssistant.DpiTests/Version230StageDColorDpiTests.cs`（14）。
- UI证据工具：`tools/StageDColorReview/Invoke-StageDColorReview.ps1`、`tools/StageDColorReview/create_contact_sheet.py`。

## 7. 测试与构建

- 新增测试：90项（Core 48、WPF 28、DPI 14）。
- 原1089项：全部保留；最终测试总数：1179项（Core 944、WPF 161、DPI 74）。
- Core并行：3轮，每轮944/944通过、0跳过。
- Core非并行：3轮，每轮944/944通过、0跳过。
- Debug全量：3轮，每轮1179/1179通过，0失败、0跳过、0警告、0错误。
- Release全量：3轮，每轮1179/1179通过，0失败、0跳过、0警告、0错误。
- Debug构建：0警告、0错误。
- Release构建：0警告、0错误。
- 所有文件/LUT/ICC测试使用独立临时目录、合成图片、合成`.cube`或可再分发测试数据；未使用真实照片、客户资料或真实LocalAppData数据库。

## 8. UI证据

- 截图目录：`artifacts/ui-review/2.3.0-stage-d/`
- 截图：22张真实WPF RenderTarget场景，22个唯一SHA-256，布局元数据全部通过，合成源资产前后SHA-256一致。
- 总览：`artifacts/ui-review/2.3.0-stage-d/像素蛋挞_2.3.0阶段D_LUT与客户监看UI总览.png`
- 证据索引：`artifacts/ui-review/2.3.0-stage-d/evidence-index.json`
- 真实双屏限制：索引明确记录`PhysicalSecondMonitorTested=false`，验证范围为自动化显示器拓扑与独立WPF窗口。

## 9. Git与停止条件

- 是否下载厂商SDK：否。
- 是否Publish：否。
- 是否生成安装包：否。
- 是否合并main：否。
- 是否创建Tag：否。
- 是否建议进入阶段E：否，不自动进入；阶段D完成后按要求停止。若未来需要实体双屏验收，须另行人工批准阶段E。

