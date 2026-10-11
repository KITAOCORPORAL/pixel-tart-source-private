# Kitao Studio 光镜 · 第七批交付

日期2026-10-11（Europe/Berlin）。实际工作树N:/pixart/pixel-tart-integrated-workflow-color-docs；分支feature/pixel-tart-integrated-workflow-color-docs。

## 起点与保全

N盘存在、约950GiB可用；工作树起点干净。LOCAL/首次fetch远端均 **1ca73a2954f750c4be8a9bd3c721388863a60441**，没有新增远端变更。已读batch06全部Markdown/JSON证据，复核生产decoder/master/workspace/quick/Publishing/managed调用链。未发现新增适用AGENTS，用户禁止Browser/WebView/Computer Use/CDP/Explorer继续生效。

原集成工作树 **a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc** 及两份未跟踪审计目录保持；运行期间原树外部新增instant-film-public研究目录也未触碰；无reset、clean、强推或自动合并。仅复用已有授权本机RAW/JPEG副本，输入原件/hash不变，不提交照片/成片/生产数据库/credential/二进制。测试生成的历史ui-guardian三报告本地备份后恢复起点tracked字节，不变更历史人工状态。

## 源码

源码SHA：**396e9ab4628d6095d97cc501b8fe3a76db588bf3**。

24文件：流式RGB16TIFF/有界回读与hash、Windows原生WIC JPEG444/PNG16行encoder、显示线性area代理、原像素detailpatch/DPI100%/cache/revision、quick与Publishing同PV4定义/实际输出校验及参数/不支持ICC或位深明确拒绝、阶段/进程probe与数学/产品测试。

显示变换/全尺寸PV4数学及decode参数不变，PV1/2黄金/PV3实验不变；旧PV4合同可读。独立同Sony文件新旧完整TIFF输出SHA相同，不是换处理版本隐藏历史像素变化。JPEG编码质量档位增加明确WindowsWIC-JPEG95-444-RowsV1，PNG16行encoder标识亦落receipt，预览是新的quality不是改full算法。

## 实测主要结果

- Sony旧全尺寸导出stage累计峰值 **2102181888字节（1.958GiB）** → 新 **976109568（0.909GiB）**，本场景约53.6%下降；尺寸7028×4688、RGB16不变，旧新TIFF SHA **2F9F37A699E7B37335AF0F1DC220BD9227BA35A321B2F2A93F05A4487EDB109A** 相同。另第六批复现约2.009GiB，不混用两个测量。
- 新Sony render+encode2457ms、stream validation122ms、hash288ms；decode/nativefloat峰值仍约0.91GiB。PNG/JPEG回读/Publishing/完整testhost累计可约1.74GiB，未宣称全应用都达到隔离TIFF峰值。
- HQ1600代理area反alias：Sony1724–2028ms，512×512 native region16ms、产品含140ms防抖约200ms。800nearest仅快速占位；原尺寸100%按DPI和原像素分块读取，超4Mi像素budget处仍代理明确提示。
- 两实际相机（Sony A7M4、Canon EOS5D III）快速TIFF16/PNG16 lossless max0；JPEG95显式444 MAE1.274/2.947、RMSE1.697/3.906，不放宽MAE5/RMSE15；默认Canon抽样失败保留记录。
- Publishingoriginal TIFF16像素hash相同、PNG8与8bitoracle exact、JPEG真实quality/DPI/sRGBprofile与有损门；非sRGB/PNG16/JPEG16明确拒绝。resize/watermarkpixel专门验证仍PARTIAL。
- PV4 Encoding/Encoded/Pending/Moved/Receipt五真实进程Exit73重启：Encoding无可信记录Missing保留临时，其余Completed并重复Completed。现有managed恢复政策保留。

## 已完成检查

Core全量1636通过/0失败/6跳过；WPF最终源码完整回归1527/0/15，7m42s；两真实相机/格式/原像素/旧会话/Publishing专项23/0/0。Core/WPF最终完整回归均PASS，skip仍单列；真实发布组件专项另有记录，不把命令测试升级桌面验收。

## Release与推送身份

Release x64 self-contained publish PASS。EXE：N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch07/release/win-x64/KitaoPhotoSelector.exe；SHA256 **3E10E8E1B80B100729133EB4A0CEB27D534D01BC16751EC7E5C6BA1D3EA3F152**；ProductVersion **2.3.0-batch07+396e9ab4628d6095d97cc501b8fe3a76db588bf3**。完整290文件hash见RELEASE_IDENTITY.json，Core与主程序集绑定相同源码。发布组件专项23/0/0（1m48s），实际发布Core性能峰值975904768字节、行渲染2192ms/render+encode2430ms/验证121ms/hash311ms，旧新TIFF SHA一致；不启动EXE。证据提交后LOCAL_HEAD/REMOTE_HEAD再核对，证据commit自SHA不写递归自引用。发布仍绑定上述源码而非纯文档commit。

## 限定评价与实际剩余

本批限定生产代码/自动/真实file主要能力完成，整体 **PARTIAL，待桌面/视觉与剩余质量验收**，不是全调色系统完成或失败：

1. HQ代理每次全像素display-linear面积积分约2s，真实连续调色/拖动体验需人工；超大屏超过单detail4Mi像素块预算的整视口多块补齐未实现。
2. Publishingresize/watermarkpixel容差与full应用内存尚PARTIAL，original专项完成；PNG/JPEG OnLoad读回仍占完整codec缓冲。
3. 512MiB/master/工作区活动保留和HeavyWork并发1是明确策略，但跨工作区master总预算、decode复制/即时OS回收仍待优化；不降低位深/尺寸伪优化。
4. 真实灰卡/标定曝光阶梯/未知机/已WB小RAW未找到可用授权标定，NOT_RUN；火焰人像不替代中性灰；内置camera matrix不等于色准批准。
5. RELEASE_STARTUP、DESKTOP/DPI、USER_VISUAL均NOT_RUN；USER_ACCEPTANCE=NOT_APPROVED，不启用Computer Use绕过。
6. 更早mosaic读取和实际参数/域调查完成，HDR/高光重建另批未实现；未把已clipRGB压暗说成恢复，未同时重写ICC/空间/GPU。

必需十份文档与追加机器可读证据已集中batch-07；详细逐项PASS/PARTIAL/NOT_RUN见REQUIREMENTS_TRACE.md，失败修复/跳过见测试矩阵。
