# V3 修正、色彩基础与新调色页：续接最终报告

状态：**BLOCKED_DIAGNOSTIC_BUILD_WITH_V3_CORRECTIONS**。本轮开发与软件可运行诊断交付不是正式发行批准。

## 基准与独立交付

- 已fetch并独立核对旧基准分支：cd6d3ce072948d6739cd789d749c2bfaadffc59e。
- 复用原会话专用工作树 N:/pixart/kitao-v3-corrections-colorpage-20261017，没有改动其他工作树。
- 冻结生产源码：ecc41699e5af0cbcffa1d9372c4244d3fa21bb2e。后续只测试工具/证据/文档，src不漂移。
- 新交付目录：N:/pixart/kitao-v3-corrections-colorpage-20261017/artifacts/v3-corrections/07-delivery/release/win-x64。290文件、自包含win-x64，不覆盖旧Release。
- EXE SHA256：C58A927E4631FF8C4CCDDBE1E1909FAFD679620AD5F4E4F2ADBEB349ADA4E224。完整身份与每文件hash见06_regression/IDENTITY.json、FINAL_RELEASE_FILES.json。

## 已实现与本轮修复

1. Creative2 把六Womb旧公式放回明确显色节点，曝光/趾肩/柔化/扩散/乳剂/老化不再被末尾覆盖。修复前诊断56项仅Base8有效；修复后56项全有效。三授权47×39裁片保留高光扩散不激活与Canon Stock2肩部裁切情况，不为报表修改物理含义。Creative1照旧解释。
2. 独立乳剂缺陷和形态缺陷，切图重置照片参数，真实Slider/TextBox与种子通知；Defect2几何持久化。
3. Paper3窗口像素覆盖抗锯齿、双线性非整数zoom、画布外沿/窗口亮线/窗口内压暗拆分，旧Paper1/2保持。双线性不是所有极端缩放的理想抗混叠保证。
4. 明确选择第二张PNG/TIFF/JPEG，双源身份/空间/尺寸/方向、权重/裁切/缩放，归一化混合与曝光累加分开。保存重开、直接三格式、丢失/变更诊断、资源释放已测；不扫描私人目录。
5. 续接发现并修正新页V3历史按钮路由、禁用事务、历史恢复失败回滚。缺第二源redo失败保留旧配方/图像/控件与redo项，恢复文件后可重试；生成失败不留无效历史。
6. 确定阶段取消：Decode/PixelRender/DevelopmentAnimation握手再切图；EncodeStrip/OutputValidation握手再取消。检查无新版本、无半文件、旧结果仍可输出，不以IsInstantGenerating代替阶段。
7. 独立PhotoWideV1公式库：DWG/XYZ、DI、ACEScct/AP1D60、sRGB/709/2020、PQ绝对亮度、CDL明确裁切。固定colour0.4.7与OCIO2.4.2完整提交、源入口/许可证hash，最终交付Core实际字段19项外部对照通过；故意错矩阵与NaN均非零退出。它没有暗接旧V3，也不是完整ICC/场景宽域管线准入。
8. 独立新调色页真实打开/调整/前后对照/撤销重做/保存重开/选定节点批量同步/导出；大预览、胶片条、步骤列表、参数、历史/分析面板，旧页保留。当前独立RAW/V3模式不消费普通步骤时明确禁用，完整V3参数仍在旧页。焦点框不冒充蒙版，未实现的高级工具不放假入口。

## 原尺寸、资源和风险验证

最终包绑定的Sony/Canon/冻结Fuji各TIFF16、PNG16、JPEG：9次直接出口全部回读。TIFF/PNG逐码最大误差0；JPEG最大MAE0.711441，未放宽MAE5/RMSE15门槛。产品预览对原尺寸TIFF线性光面积缩图最大误差2.165013e-6，小于固定2/65535；原件最终hash不变。Fuji未新原生解码。

编码与完整输出校验分别计时，编码session/条带先释放再开校验；TIFF约10.7–17.5秒、PNG约9.8–20.1秒、JPEG约8.3–17.0秒编码，具体每图以05_risk/AUTHORIZED_FINAL_RISK.json为准。未删除完整性校验。PNG/JPEG方向检查metadata并核尺寸，不仅在回执写1。

最终进程1546点、200ms采样：最高private2,043,916,288 bytes，Windows生命周期working-set峰值2,033,135,616 bytes。不是WIC原生独占或无整图缓存证明；2GiB租约预算不是整个进程峰值保证。未测前台关闭窗口后的原生回落。

合成两尺寸20组合覆盖关闭/常用/极值及风险相互作用，7行条带和整幅float完全一致，保存完整图/预览/配方；两种多曝各三格式。查看合成接触表、原像素圆边和三授权中心裁片，范围写入VISUAL_REVIEW.md；不宣称全面实拍审美验收或相纸标定。私人照片/RAW/数据库/构建不提交。

## 回归与证据

| 测试套 | 发现/选择 | 执行通过 | 执行失败 | 条件未运行 |
|---|---:|---:|---:|---:|
| 冻结源码 Core |1713/1713|1706|0|7|
| 最终交付 Core |1713/1713|1707|0|6|
| 最终交付完整 WPF |1639/1639|1616|0|23|
| 冻结源码本轮专项 WPF |1639/11|11|0|0|
| 共用 Core 候选套 |25/25|25|0|0|
| 逻辑 DPI |91/91|91|0|0|

全套无缺失、无超时。源码本轮专项不是源码完整WPF，完整WPF是在与Release DLL哈希一致的宿主运行。

逐套详情见TEST_COUNTS.json，原始runner在存在NotExecuted时仍FAILED，保留不改绿。TRX、完整名称与参数行、源/交付DLL哈希绑定、原失败、测试ZIP及成员哈希保留。最新交付完整WPF与Core另标，不用之前构建的全回归冒充。逻辑DPI91不等于物理验收。

P3索引修复继承，最终交付当前/全库切换预热6.164/6.258/5.521ms，400ms门槛未变。历史665.788ms与长尾根因未全关继续保留，不描述为当前必现失败。

## 缺资源、未运行、发行阻断

- 缺完整LUT与第三方特殊贴图；四份Resolve原附件仅明确定位一个12成员ZIP，其余不编造。
- 真实前台操作/物理DPI/关闭后内存回落未验收，未确认安全独占，不调用Computer Use/Browser/CDP，不向用户桌面发送输入、不改ICC/DPI。
- Fuji原生重复性、显示ICC分歧、全扩展极端混叠与视觉全面验收仍开放；无校准资格。
- 不合并main、不强推；正常推送与独立完整SHA核验记录另见PUSH_STATUS.md。

操作见USER_GUIDE.md；逐能力见CAPABILITY_STATUS.md；下一步见NEXT_BLOCKERS.md。
