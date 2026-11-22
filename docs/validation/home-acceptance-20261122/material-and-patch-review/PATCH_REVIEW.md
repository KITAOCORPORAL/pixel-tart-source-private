# 内存补丁准入审查：暂缓合入

当前产品 `f84f8b2d9f68d668c6387089832e6f52cdd24dab`；已审家庭文档 d4965956。研究补丁顺序为 `cf5a63d38365a0093524d87ed7670e3736b7bb24` → `11a424c90199b32cbf73cc6a14eefd651612b213`，共同基线 d07e4f43。最新研究回执提交 `eb5229eb1a06792162108fa1c3bed56c2ac249e1` 相对11a仅改文档，产品代码无新增变化。

## 文本与当前修复

只在产品树的ignored审查目录中提取Git对象副本，用 `git merge-file -p` 对三份文本进行预演；未checkout、cherry-pick、合并、编译或启动任一版本，也未写研究工作树/索引。

研究增量为8个产品文件+2个Core测试；当前f84相对d07为3个独立改动文件。直接共同修改文件数0，10份文本预演冲突0。**这仅证明文本预演干净，不是容量、行为或合入门禁通过。**

f84必须保留的三个路径：

- `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs`：拒绝预备选片时调用既有可见提示；研究分支仍是d07版本，不能用分支整文件覆盖回去。
- `src/Guangjing.Schedule.Server/wwwroot/calendar-client.js`：历史预约受保护链接直达逻辑。
- `tests/RAWSelectionAssistant.WpfTests/SelectionRejectionVisibilityTests.cs`：对应WPF回归。

直接把 `f84..11a` 的差异当合入补丁，或用研究树整目录替换产品树，会回退上述提示和预约逻辑并删除该测试。将来只能审查显式cf5→11a产品增量，保留当前修复；目前不执行。各文件blob与合并文本摘要见 PATCH_TEXT_REVIEW.json。预演只覆盖产品/测试增量，不是整批合并研究历史文档；将来研究旧回执只追加为带source的历史，不覆盖当前入口或通过状态。

## 共享产品路径和影响范围

| 研究改动文件/组 | 已读代码行为 | 产品接入时必须复验 |
|---|---|---|
| `ColorStudioRenderPipeline.cs` | Render保留公开入口，拆出Processing结果；显示转换异常时释放其结果。是共享渲染入口，不只影响导出。 | 中性、点操作、空间/graph/混合回退；取消与异常；预览/保存重开/导出一致 |
| `ColorStudioRenderPipeline.Export.cs`、`HighPrecisionExportRows.cs` | 中性借用、受限点节点16行条带；借用输入不由导出Dispose；显示直接float→RGB24，TIFF按原RGB48舍入。graph/非身份合成等不准入条带。 | 共享master继续可用，后段失败/取消，尾部条带，全行RGB16与RGB24、ICC；不能外推空间节点容量 |
| `RawMatchTiff16ProductPipeline.cs` | 新upright入口；旧decode默认签名仍用旧方向模式；新RenderDisplay使用条带/拥有的回退结果，导出按行写入。 | 解码参数/方向、失败释放、首次/重复导出、同进程重开、独立进程重开 |
| `RawImageOrientation.cs` | 已有RGB48方向置换与ushort/65535f合成一次float分配，未调用新的相机解码。 | 方向1–8、尺寸/全float/元数据，方向8实样，源数组不改写 |
| `ReferenceColorWorkspaceViewModel.RawOrientation.cs`、`.Thumbnails.cs` | 同一workspace partial类的方向入口和缩略图改用新管线。 | 缩略图/视口/分析/导出坐标一致；任务取消、切片、关闭等待、源持有者生命周期 |
| `TetherReferenceModeViewModel.cs` | 旧Render(...).Pixels改为RenderDisplay。暗室/联机参考共用此编辑器。 | 处理后100%预览、撤销重做、保存/关闭取消、同进程与独立进程重开；实际持有者/提交峰值 |

没有同文件文本冲突，仍存在这些共享调用与生命周期风险。当前B1关闭协议、PNG选片准备与客户所选导出都应按实际调用链选择回归；手机/日历JS不被内存增量修改，保留已验摘要，不自动重跑手机或签发凭据。

## 当前门禁：未满足，拒绝接入

研究回执的已测范围：Core75/WPF20，Sony方向8完整周期、40MP中性完整周期、处理后40MP100%首次导出已通过。这里是复读研究回执，未在本轮执行，不能换写成f84集成或普通EXE通过。

最终11a二进制的 `verified-40-processed100-cycle` 仍在 **payload-read-2 STOP**：PrivateBytes **2493468672 bytes / 2377.96 MiB**，OS峰值工作集 **2181640192 bytes / 2080.57 MiB**，均越过2GiB。证据绑定Core `0b9eca09…` 与App `ef401fc4…`；不是仅候选旧构建的失败，也不能拼接“原图100%且处理预览关闭”的先前结果代替真正处理图。

这证明该剩余容量门禁失败，**不单独证明内存泄漏或某个具体强根**。本产品线不代研究线跑新的大图诊断，不强制GC、清全局缓存、销毁未证明独占的master、提高预算或启用新RAW解码。

## 以后何时才可接入

研究线先补齐相同最终二进制、相同受控输入和2GiB/50ms条件下的处理后40MP同进程完整周期，并保持之前的像素/方向/ICC与取消/输入可用性门禁。产品线复读完整原始回执与源码绑定后，在产品集成分支审查显式cf5→11a（或当时新增增量）与最新稳定源，构建全模块同源码包，完成受影响普通EXE验收。旧f84完整包保留至统一交付通过；不从研究树发布只有暗室的包，不直接替换当前二进制。
