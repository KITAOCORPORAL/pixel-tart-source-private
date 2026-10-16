# 显示器色彩管理基础：可测试合同/缓存，真实转换仍研究隔离

## 实查与状态

EnumDisplayDevices设备接口DISPLAY#AUS27E1#...UID4625对应DISPLAY1；WmiMonitorID同实例EDID PA279CRV。DISPLAY1实际GetICMProfileW关联ASUS PA279CV-12026625.icm，SHA D1CA53DF625D4BB2EA73A6B1D541819DFBE1F9474DB621BF8C8FE331EC96A0D0。DISPLAY2对应AOC2710/Q27P10，profile691FDDD2773EEF6F13032B0D34CA89B76E472676E494D06DA95D3B75BE806E57。只读映射证据，不能凭文件名判profile好坏，未修改系统设置。

真实WPF offscreen RGB24 DrawImage→RenderTargetBitmap实测码值保留。**该结果不是DWM合成器/真实屏幕行为**；桌面独占未获确认，前台像素抓取/跨屏仍NOT_RUN。既不假设OS自动补偿，也不据此假设真实合成器必定不补偿。

## 本轮产品基础实现

新增DisplayPreviewContract/Key/Cache和IDisplayPreviewTransform边界，独立于编辑与导出；输入类型ReadOnlySpan<ushort> RGB16，仅有限0..65535、不接受负值/>1浮点、opaque only；目标是monitor device RGB16，不是新的编辑工作域。

版本DisplaySrgb16ToMonitorV1：encoded sRGB/D65→profile PCS→显示器profile编码；relative colorimetric intent1、BPC=false、adaptation1；越界在量化前拒绝，不静默裁切；无premultiplied alpha支持。无效/缺profile、无monitor、预算不足分别返回明确未校准/拒绝状态；默认关闭。

缓存键包含设备、源profile与monitor profile内容SHA、源像素SHA、geometry、engine身份及全部合同参数。最多64MiB单项；防御复制、取消、显式Invalidate；不按profile文件名缓存。不将输出写回源数据。3项基础测试通过，数值测试double不充当颜色参考。

**WPF viewport尚未接入，非完整产品显示补偿**。LCMS具体绑定留tools/PhotoEngineFollowupAudit，仅显式运行、固定旧LCMS2.12 SHA和已检查sRGB/两个显示器profile哈希；未扩展原ControlledIccTransform研究allowlist，不宣传任意ICC。未接导出和普通入口，因此不能声称已完成移动窗口后的物理导出隔离验收。

## 实际参考运行及失败

灰阶/近肤色码值/饱和RGB/边界：LCMS2.12 RGB16两次确定、缓存命中；独立Pillow12.3.0内LCMS2.19同profile/intent/flags的8bit对照最大1码，限定门槛2码，记录舍入精度，不推成16bit等价。WIC RGBA64同profile转换可执行但与LCMS最大差820个16bit码，**未消除，禁止将WIC当已配平参考或接生产**。

初次LCMS同profile严格identity失败单列；已按完全相同profile内容直返原码，避免引擎往返量化误差。初次失败不覆盖。

WIC40种输入/目标/profile组合：14可执行/26拒绝。RGB48→RGB48线性profile到sRGB仍拒绝0x88982F80；同profileidentity可执行；RGBA64→RGBA64的线性到sRGB可执行。说明是具体WIC格式/变换能力边界，不是所有高位转换都不支持。未将16bit请求降8bit；尚未完成alpha/逐码/多个profile完整评估，不改StudioQuickExport生产路径。

## 开源先行/发行限制

LittleCMS2.16参考索引commit453bafeb85b4ef96498866b7a8eadcc74dff9223/src/cmsxform.c、RawTherapee5.11 db575c6690d30ce6428c0e1383bec8b8f1fb8d21/rtengine/improcfun.cc/iccstore.cc为待独立实现对照。实际运行的是本机LCMS2.12与Pillow2.19（同引擎两个构建），WIC第三参考差异仍开，**未满足两成熟独立实现完整准入**；不复制GPL代码，不把框架/同引擎交叉验证当生产像素批准。

准入OPEN_RESEARCH_ONLY：仍需配平WIC意图/引擎差异、独立成熟实现、alpha/高精度、真实屏幕合成器与跨屏、所有原图/对比/细节块一致路径、导出不变的实际集成测试。未过这些门禁不接WPF预览开关。PV4/PV5/PV6数学和golden不改。

## 追加独立精确profile数值定位

本机两个显示profile都是XYZ matrix+单值gamma TRC；sRGB源profile为1024点TRC。独立按实际源TRC→PCS矩阵→目标逆矩阵→目标逆TRC计算，已在INDEPENDENT_PROFILE_ORACLE.json记录与LCMS、WIC的逐样本差异。PCS矩阵已D50，不重复chad。这仅覆盖三份固定profile，不是任意ICC实现；不改变WIC差异或真实合成器未验收边界。

独立oracle本次实际FAILED：最大133码>预定16码（源输入1/65535近黑处；其余本组最大2码），与WIC仍最大820码。完整失败保留，未放宽阈值或删近黑样本。精确profile模型/原生暗部量化仍须定位，故显示转换继续不接生产。
