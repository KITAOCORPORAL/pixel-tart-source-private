# 第八批逐项交付总表

日期：2026-10-12。源码commit：c3c1ed758f413dde9dd4a9f7c630b9db3bd1c99c。

## 起点与保全

N盘真实可用，实际工作树N:/pixart/pixel-tart-integrated-workflow-color-docs。fetch后LOCAL/REMOTE均4c1ae3b2d8dbb1fdcf1b8fa1a310883580e911d9；其生产源码396e9ab4628d6095d97cc501b8fe3a76db588bf3。没有远端新增差异需要合并，没有reset/clean/强推。

起点非干净：6个tracked修改、3个未跟踪实现（InstantFilm/StudioGlobalResources/StudioDetailTile）。完整起点patch和三文件保存在本地.validation/batch08-start，先读取再续接。原集成树a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc及两个审计目录、未提交研究原件原地保留。递归未发现额外AGENTS；遵守用户禁止Browser/WebView/Computer Use/CDP/自动Explorer。既有研究只读，关键文件hash见RESEARCH_INPUTS.json。不提交任何照片、生产DB、凭据或构建二进制。

## 逐项状态（自动结果与人工批准分开）

|需求|实际结果|状态与剩余|
|---|---|---|
|可见视口多块原像素|512×512迭代补齐、64MiB合计cache/display限制、取消/revision、完整参数key；未补代理明确|代码/合成EXIF1–8和DPI输入矩阵PASS；真实桌面DPI/平移观感NOT_RUN|
|HQ重复工作|连续源行复用、两项完整key缓存、串行后二次命中、保留full面积参照|PASS；同Sony实测约33%下降，native块25–27ms比旧14–16ms慢；未声称全部提速|
|跨工作区合计资源|进程级2GiB准入账本，Master1GiB/Detail192MiB，decoder/preview/export/Publishing工作预留|实现PASS；完整应用OS峰值/旧路径所有缓存并未硬约束，跨真实工作区压力PARTIAL|
|Publishing缩放水印独立oracle|TIFF16 bilinear参照、EXIF6尺寸、96/144/240DPI×3透明度×9位置图像水印oracle、失败保全|这些自动项PASS；任意照片纹理/文字水印像素独立oracle未穷尽，生产verifier对resize/watermark仍metadata-only，单列PARTIAL|
|独立处理合同|RAW PV4基础后sRGB即时层V1；JPEG完整输入显式PV5；白平衡不重复|PASS；嵌入ICC转sRGB，不宣称全ICC|
|关闭与历史|null保原JSON/fingerprint，关闭identity、黄金/旧回归，SpatialVersion1/2未改|PASS；旧照片视觉NOT_RUN|
|八预设|i-Type/600/SX70 Color/BW、Reclaimed Blue、Blue Duochrome，三层独立配置|代码PASS；均创作近似，不是准确相纸标定|
|研究扩展及许可|8个research-only id，不在可用UI；未引入Kunkles/GPL/外部纹理/厂商数据|PASS；研究物理缺口继续开放|
|骰子与快照|关闭黑白、开启彩色；严格三档；显式重采样、独立子流、存档几何|PASS；程序控制/存档/分块/空间复现，UI视觉NOT_RUN|
|相机入口|原创几何古着SX70/680与现代i-2可点击外观；不换film id|代码PASS；美术审美与真实外观辨识NOT_RUN|
|吐纸→中央显影|完整吐出后相机覆盖消失；两路径默认4秒，3–5秒边界、不可跳过|状态与实耗时PASS；真实桌面动画/颜色轮廓审美NOT_RUN|
|冻结/失效/恢复|ready前不计时、冻结seed/定义；禁重复/改配/导出；生命周期取消与失败保旧|自动测试PASS；多窗口重入压力仍需人工|
|快速/Publishing/保存重开|同冻结definition；实际JPEG同seed保存重开；三格式lossless/lossy分开验证|PASS；实际RAW2机专项另列|
|完整Core/WPF/Release|详见TEST_MATRIX和TEST_SUMMARY，不以中途失败作成功|最终结果以矩阵为准|
|启动/DPI/人眼|遵守限制未自动启动EXE或控制桌面|NOT_RUN / NOT_APPROVED|

## 发布

EXE：N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch08/release/win-x64/KitaoPhotoSelector.exe

SHA256：F4F55DEAE8C68A6CEE782F694F55100BB7CAF8573AC5ED423CDB1C5DA5371881

ProductVersion：2.3.0-batch08+c3c1ed758f413dde9dd4a9f7c630b9db3bd1c99c。发布Core/主DLL和源码一致；文件全清单RELEASE_IDENTITY.json。发布二进制不纳入Git。源码正常push；后续纯证据commit不改变发布源码身份。

## 范围之外（保留路线）

HDR、高光恢复、全ICC、GPU、完整Word、手机APP、浏览器扩展本批未重写。Reclaimed Green、Go、8×10和历史系列不冒充可用。没有把“创作模拟”称为“准确复刻/真实物理标定”。

最终自动检查：Core **1643通过/0失败/6跳过**；WPF **1534通过/0失败/16跳过**；发布Core专项33/0/0、发布WPF专项13/0/3且发布真实RAW另跑3/0/0。Release构建PASS，未启动EXE。源码与证据分开提交，最终Git远端身份由末次fetch/ls-remote确认。
