# 固定参考和产品分发决定
只读核验，非法律批准。实现为现有WPF模型的独立功能适配，没有复制/翻译参考源码进产品。

- LightTable-Windows commit cb8f4c89d845634688627f1c01225cb5d851248f；LICENSE MIT Copyright2026 fuyuan9972-svg，完整hash见REFERENCE_FILES。读TableInput.cs、LoupePower.cs、ProjectionPlayback.cs及DESIGN；行为是拖动/缩放/旋转/局部放大/取消代际。App csproj是WinUI + WindowsAppSDK2.5.1 / Win2D1.4.0，Core仅net10。其NOTICE对Win2D二进制EULA核对仍有未闭合，本产品不引入上述依赖/资源。
- 原LightTable commit 8e1868df40e69e6b1a08e207d7ab05cf463532ab；根MIT和NOTICE/Package.swift实际读取。Sparkle2.10、PlayCanvas、OOOSplat有独立许可；SHARP模型非商业科研限制、原音频不属于MIT、第一方EDR专有二进制均不得从Root MIT推导商业再分发。本产品均不引入。
- SpektraLab commit c1447c6ab618fa4b1cb11fd2fe12826855007059；根LICENSE.txt GPLv3，hash3972dc97...；resources_product NOTICE hash44425f1d...明确原数据来源与拟合假设，不自动构成全部资源可商业授权。最新研究ce292f2a报告代码/数据许可仍未批准，BT边界失败、grain_seed缺失。当前只约定隔离适配合同，界面禁用；不交付其代码、胶片/相纸数据、LUT、shader或DLL。进程隔离不改变许可证审查要求。
- 本轮未新增第三方NuGet；已有LibRaw、LCMS等按既有固定版本和随包告知。不存在新参考项目MIT代码的复制分发，因而没有把其LICENSE当作授权所有二进制的捷径。
