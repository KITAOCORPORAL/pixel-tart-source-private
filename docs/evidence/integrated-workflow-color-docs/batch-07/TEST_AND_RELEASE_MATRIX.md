# 测试与Release矩阵

日期2026-10-11（Europe/Berlin），原始机器日志使用Asia/Shanghai，跨其本地午夜不改写用户日期。集合重叠不相加，skip单列，不等于通过。不会与运行testhost共用output并发构建。

## 目前完成的验证

| 验证 | 通过 | 失败 | 跳过 | 记录 |
|---|---:|---:|---:|---|
| Core全量 | 1636 | 0 | 6 | core-all / core-source-final.trx |
| WPF相关最终 | 21 | 0 | 0 | related-source-final.trx |
| 两实际相机完整product+旧保存重开 | 8 | 0 | 0 | product-current.trx |
| CoreTiff/preview/黄金/恢复专项 | 50 | 0 | 1 | core-related-final.trx |
| WIC PNG16/JPEG444小图/方向 | PASS | — | — | wic-png16.trx / jpeg444.trx |
| 同Sony旧新stage/文件hash | PASS measured | — | — | MEMORY_*.json |
| PV4真实OS进程5退出阶段恢复 | PASS | — | — | PROCESS_RECOVERY_RESULTS.json |
| 更早mosaic读取2相机 | PASS observation | — | — | EARLY_SIGNAL_*.json |

WPF全量 wpf-all.trx 已完成1527通过/0失败/15跳过（7m49s）；最终源码补方向/DPI/文件SOF校验后的结果与Release身份在文末追加，不能用旧专项替代未跑全量。

## 失败与修复记录

1. WpfPublishingRenderer新增stream局部变量output与外层重名，编译失败；改streamedOutput重建。
2. 旧组件baseline probe混入新方法引用导致MissingMethod，随后native DLL依赖缺少和重复DllImportResolver注册失败；独立仅旧APIprobe并复制同版本jpeg8/lcms2/zlib1/raw_r四依赖成功。失败日志保留不算baseline通过；有效数据MEMORY_BEFORE来自stages-before-final。
3. 默认JPEG抽样CanonMAE6.488越界导致产品export失败，通道bias接近0，重试使用OnLoad确认同误差；没有修改MAE5/RMSE15阈值。WindowsWIC显式444使Canon2.947/Sony1.274，通过。旧失败TRX和最终修复记录并存。
4. Publishing byte depth能力不能按用户配方假写：PV4 PNG16与非sRGB现在明确reject；original PNG8/JPEGquality与TIFF16都有真实读回。
5. 取消校验发现需保原存在文件，ProfessionalRawExport使用私有随机staged文件→通过后move，失败只清本次staged；三格式cancel、ownedbuffer/gate、旧文件不删除已测。
6. 本轮原raw GitHub/curl/网页工具个别没有内容/连接reset，官方Git浅克隆+sparse确切SDK header后核对。没有调用Browser或Computer Use。
7. MSTest建议式warning仍存在；没有把warning或人工缺失当PASS。

## 跳过与运行状态

Core6沿旧光色空间/真实opt-in/性能/GPU；WPF完整跳过逐项在TEST_SUMMARY.json。新增真实batch07测试默认opt-in在全量将skip，已单独显式授权本机copy运行；不是把合成测试当真实相机。

RELEASE_STARTUP=NOT_RUN、DESKTOP/DPI=NOT_RUN、USER_VISUAL=NOT_RUN、USER_ACCEPTANCE=NOT_APPROVED。无人操作桌面/截图，不用另一自动化方式绕过禁Computer Use。BUILD/发布组件验证以RELEASE_IDENTITY和后续实际结果为准。

整体限定范围：生产代码和数值/真实file验证完成项单列PASS；超budget多块详情、Publishing缩放/水印pixel容差、全应用内存与桌面响应PARTIAL/NOT_RUN；HDR另批。

## 最终源码专项

final-source-product.trx：23通过/0失败/0跳过（3m23s），实际两相机Quick/Publishing三格式和旧会话保存重开；后续source-gate.trx 21/0/0补TIFF方向6、DPI240、JPEG16拒绝、Stop命令取消及Original模式guard。源码最终完整回归wpf-current.trx为1527/0/15（7m42s）。

## 最终源码与Release

源码396e9ab4628d6095d97cc501b8fe3a76db588bf3：Core core-current.trx 1636/0/6；WPF wpf-current.trx 1527/0/15（7m42s）。所有构建已等待同目录testhost退出；Release x64 self-contained publish PASS，ProductVersion绑定该源码，EXE及290文件hash见RELEASE_IDENTITY.json。未启动EXE；发布DLL隔离专项身份见PACKAGED_COMPONENT_IDENTITIES.json，实际发布DLL隔离专项packaged-components.trx为23通过/0失败/0跳过（1m48s），包含授权2相机Quick/Publishing三格式/native细节/旧会话重开。不是启动ReleaseEXE。

实际发布Core隔离性能与输出等同性PACKAGED_MEMORY_SONY.json也PASS；RenderRowAggregate与Encoded并列，前者是后者内部CPU时间，不累加总耗时。
