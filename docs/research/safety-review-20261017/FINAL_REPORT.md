# 数据安全审查与既定产品流程：最终报告

诊断构建，不是正式发行。本轮外部审查是线索，完整当前源码与复现决定修复，不退回旧b40bccfd。

## 基准和提交

前轮fetch/ls-remote核对远端feature/kitao-v3-corrections-colorpage-20261017为b40bccfd459902d8761fd53a6f05c0894350a148；本轮再次fetch在2026-10-17因GitHub 443连接失败，当前远端状态无法独立复核。本地后续52efba2d已有独立色彩链、即时演示、原尺寸不同照片代码，保留后继续安全修复。

冻结产品源码：9ad468e851202ba35d7154803673d3c08aa9dde8；诊断交付提交8b8161e13c65c6628e04d7ed400c23066757dbd2只修正旧RAW版本目录测试的显式保存语义，未改src像素链。
隔离290文件目录：N:/pixart/kitao-v3-corrections-colorpage-20261017/artifacts/safety-review/11-delivery/release/win-x64。
EXE SHA256：E44A571C7C4A8ED83CA99A6D5EF24CE127009C52C2202B7385E13E34F04A389D。

## 核实和关闭的风险

1. 普通正式导出已存在temp→CommitOutputAsync无覆盖发布→receipt→success，审查“缺少原子发布”未成立；真实风险是固定output.tmp及失败TryDelete可能误触同名临时。改每任务同目录UUID；不改通用Encode覆盖语义，不破坏受控临时/测试调用。源hash/已有final/外部foreign.tmp/racing final/发布边界故障/取消/重试成功文件不变通过，RAW/V3原保护保留。
2. session两次保存夹repo保存确实有半完成窗口。现在一次项目快照携全部版本，独立共享版本目录仍由原显式命令保存；并非跨仓储全局事务。新增项目文件锁+预期SHA CAS，冲突明确失败、不写SessionSaved。未读已有文件API拒绝；UI明确替换后仍比较确认时SHA；源照片路径拒绝。
3. session加载原来过早清旧工作区。现在隔离workspace真实准备源/身份/参数/预览，冻结document再提交；commit失败恢复旧source/matched/尺寸、layout/filter/session、engine/look/stack/film、活动身份和完整undo/redo历史，不只是Targets。badJSON/缺源/坏bitmap、commit故障、准备后源替换均验证旧workspace还能undo/redo/另存。
4. 项目快照新增EmbeddedVersionsAuthoritative仅新项目写入；防重开被同ID旧repo覆盖。项目.9/repo.2真实测试复现后保持.9，repo后续变化显式版本保存仍冲突。旧项目默认false，不暗迁历史解释。
5. PixelTartPublishing新增GUID owner清单、hash、创建排他锁与消费者读锁；成功handoff保留给多配方与任务重试；失败/取消清自己登记文件。发布页显式“释放工作台临时文件”确认后逐文件身份/hash检查；活动/改写拒绝、未知文件保留。重启不扫名批删，保留清单供明确重新加入/释放；无owner旧遗留不删除。

完整处置表见REVIEW_DISPOSITION.md；实际验证不是把外部评论直接视为事实。

## 已下达功能继续结果

**生产新增节点**：RenderedSrgbExposure1，已渲染sRGB文件解释→D65相对线性sRGB→2^EV曝光→舍入sRGB编码→最后clamp。入口在新调色页，版本/工作域/参数写项目，可undo/redo/重开/三格式出口。显式RenderedInputVersion2避免复用旧CurrentEdits master，不与RAW/V3混用。不是DWG/RAW场景或显示ICC全接通。

**原失败根因**：项目decode cut=.04045；colour0.4.7默认实际cut=.0031308*12.92=.040449936，窄区间分支不同；OCIO moncurve cut=.055/(2.4-1)=.0392857142857、切线斜率.077380154467，不是1/12.92。float32与各阶段差异另列。原PhotoColorMathV1未改，旧失败保留；匹配阈值配置的colour及固定commit scikit原transfer函数逐阶段2e-10门槛最大0，未用clamp掩盖。原色/灰阶/负/>1/double/f32相邻、48授权样本保留。实际产品输出三授权中心crop对独立scikit文件像素：TIFF16/PNG16逐码0，JPEG沿MAE5/RMSE15通过。

**来源**：Pillow/FFmpeg先前缺失源码与license本轮通过固定commit contents API取回git blob/SHA，原fetch失败字段保留并追加recovered信息。local executable版本相符不等于可重建binary provenance，未夸大来源完整；GPL外部参考不进Release。

**即时与演示**：InstantDevelopmentDemo默认关闭，即时计算完提交；演示打开仍保留原相机退片/4秒测试，skip只取消等待，不改recipe/seed/history。提交前完整source/target/revision/version/token检查保留。最终交付小图实际即时245.721ms，skip运行decode .031ms/pixel26.335ms/demo .0003ms/commit138.109ms，结果见IMMEDIATE_FINAL_PUBLISHED.json。不是把关闭实时反馈当优化：草稿与应用合同保持，计算后立即显示结果。

**原尺寸不同双照片**：Canon5496×3670 + Sony归一后4688×7028，原冻结授权hash，不扫描/不新解码Fuji。新页命令编辑、apply、undo/redo像素、切图、session重开、实际ExportSelected PNG16/TIFF16/JPEG，完整输出4220×4863，预览512对原尺寸TIFF线性面积oracle约2.165e-6 <=2/65535。权重/裁切/zoom在照片内部同坐标区域确实改像素，不拿重复导出代替oracle。

原WIC工作区76N+文件预算确实拒绝大二源，未扩预算。新增TIFF3只支持明确产品未压缩RGB16/精确sRGB ICC，以逐行读+最终float替代整图WIC缓存和中间数组，旧1/2保留。真实进程峰约4GB以上，台账不是进程上限；按最终原尺寸记录报告。staged加载会重复解码/渲染，内存和时间成本不掩盖。

最终8b8161e1诊断交付DLL宿主原尺寸重验通过：Canon5496×3670 + Sony4688×7028，输出4220×4863。初次应用41859ms，decode680ms/pixel13669ms/demo .0007ms/commit27040ms；commit含重新激活原尺寸预览，不把它称为人为等待。三格式出口+产品回读约32.6/34.5/32.3秒；TIFF16/PNG16误差0，JPEG MAE.51865/RMSE.89840；预览512按线性面积对完整TIFF最大2.165012e-6 <=2/65535。权重/crop/zoom同照片内部坐标各有变化hash。完整运行309.181秒；未对该次重新采样进程private峰值，11次WPF审计报告的lifetime working-set峰4,578,643,968bytes、台账关闭前242,043,840→0仅是各自测量，不能宣称整个进程无缓存或无泄漏。上一9ad468e8的250ms采样峰private4,664,442,880bytes仅属于旧构建，不冒充本次。当前独立源码/交付hash见FINAL_FULL_BINDINGS.json，旧10次证据继续保留。

## 测试和证据

安全故障专项与既有filmstrip/publishing通过，另新增portable项目repo一致性用例。完整Core/WPF、授权专项的计数及测试assembly hash见TEST_COUNTS.json：source Core1706通过/7未执行，发布Core1707通过/6未执行，发布WPF1632通过/24未执行；Runner整体将含Inconclusive套件标为FAILED，不能表述为全绿。旧ProfessionalRawWorkspaceTests缺少共享目录显式保存，10次有1项实际失败；11次修正旧测试语义并重跑无执行失败。原失败/旧构建单独保留，不以NotExecuted报Passed；最终src/DLL/test assembly/hash记录。

## 未运行与发行阻断

- 真实硬件满盘/断电、跨主网络文件系统锁语义、恶意同权限清单篡改不是已验收权限隔离。逻辑故障注入不冒充这些测试。
- staging加载是正确性准备而非零成本；上一构建有完整进程private/peak采样，本次只有lifetime working-set读数；不能因图像租约释放就宣布原生无缓存/泄漏。
- 发布中间源为重试保留，重启不自动清旧无owner目录；显式释放需要完整owner/hash。
- Fuji原生重复性、显示ICC分歧、物理DPI/前台操作、缺完整LUT/贴图继续开放。不改系统ICC/DPI、不发送用户桌面输入、不Browser/CDP/ComputerUse。

推送状态另见PUSH_STATUS.md；远端连接失败时不声称已推送。不强推、不main、不上传照片/RAW/数据库/凭据/构建二进制。
