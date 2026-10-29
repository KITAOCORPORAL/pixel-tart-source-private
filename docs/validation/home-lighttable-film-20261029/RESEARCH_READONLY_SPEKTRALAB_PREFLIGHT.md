# SpektraLab 独立研究：预检（未完成）

目标提交 `c1447c6ab618fa4b1cb11fd2fe12826855007059`，Git tree `f2d76eec2f35583f42783fd0b0bc723b69915b70`。结论 **暂缓接入，技术与许可均未完成验收**；此报告不代替三胶片实际试验。

## 已读文件及 SHA256
- ARCHITECTURE `BB2EA87B562A1AD3453230CF23C7FFD374E3ED1156992CF619A0B21702B73983`
- engine/WINDOWS.md `10542D70D4DA781D5321E8DC55CA1380B220EFD26061DCBE1875587249E03B08`
- engine/WINDOWS_FEATURE_COVERAGE.md `0475A885C5FF25DEA9D7FF79E39D8C61DE2CC254A8C6C37A6348B3D7B938F2CB`
- engine/include/spektrafilm/spk_engine.h `5DD33AB8AAFF9CD98031ABA4F2B601C31617E4894380D91170CD5D6079769DFB`
- 根 LICENSE.txt（GPL-3.0 文本） `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986`
- engine/resources_product/NOTICE.md `44425F1DBF29983482087F2B439730DB389866BBF19CF90159CF915B2956B96C`

NOTICE 描述 engine/resources 28个 spektrafilm 原始资料为 CC BY-SA 4.0；product 目录另述 Tri-X 与 Multigrade 数据来自 Kodak/Harman 资料表数字化和拟合，NOTICE 说明来源不等于已经取得全部资料表衍生与商业使用授权。需逐资源核查作者、完整许可、原始数据权利、归属与修改适用条款。代码 GPL-3.0 分发前须审查对应源码、许可告知及整个组合分发义务；CC BY-SA 数据需核查署名、许可链接、变更标记及适用的相同方式共享条件。离线烘焙 LUT 是否构成改编/衍生、与软件打包的义务、单独进程/动态库边界均不能在本次以工程设计断定；寻求权利人明确商业/双重授权及法务审查，或使用可证实许可兼容、独立制作/获授权数据的方案。以上不是法律意见，不等于许可已批准。

## 本机核查和阻点
仅通过 GitHub API 获取指定文件，完整仓库 clone 长时间无响应后中止，未取得资源、完整依赖清单及其 hash。系统无可用 cmake、MinGW C++20 或 glslangValidator，未实际构建；Vulkan loader 1.4.341 可见 NVIDIA RTX 5060 Ti、驱动610.88，**不是渲染或性能实测**。无实测资源组合；Portra 400、Ektar 100、Tri-X 400/Multigrade 三组均 NOT_RUN，不能从作者功能表当本机通过。

源码文档要求 Windows Vulkan 的输出 `spk_result.rgba16` 属于结果句柄的独立 CPU RGBA16，按 `row_stride_px` 读取，以 `spk_result_free` 释放，不能把 texture 当 VkImage。线性宽域须准确转换至引擎真实输入域并核查曝光、WB、转移、输出，处理过的 sRGB 仅能作创意模拟；双重 WB/tone/色域映射未测。颗粒和光晕不是 LUT 可以完整承载的空间过程，边界和性能 NOT_RUN。

A 完整后端：GPL/数据权利和最小 ABI 渲染全部开放门禁，暂缓。B 获许可的轻量颜色预设 + 独立空间效果：只能作为另行设计的替代，不能用未经授权的本项目资源/LUT自动规避许可；精确效果及证据仍待验证。未来保持现有布局与普通胶片配方模型，宝丽来另线；不改UI或默认产品。
