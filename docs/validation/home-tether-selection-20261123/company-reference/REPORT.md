# 公司家庭成果接续与内存集成 · 2026-11-23

**PARTIAL：显式内存链已在独立候选集成并构建；公司原TIFF与合法legacy缓存均触发新资源STOP，候选不准入。当前唯一稳定产品保持f84。** 原始失败、旧包和用户项目不变，不把首轮正确输出或ac1家庭受控通过升级为本公司三轮通过。

## 基线与集成

fetch后home/d07-ordinary-acceptance-20261122为baa1de690afb6215c6e25ca3d16880afab4caa1a，功能f84f8b2d9f68d668c6387089832e6f52cdd24dab；precision-reopen研究头ce9c38e7404d74d146ec918f862c46a38adfb85b。没有把旧公司d07当完整产品。原公司主树f665与原dirty树均保留，详细现场PREFLIGHT在包外根。

独立integration/company-precision-20261123从baa1建立，仅cherry-pick显式产品提交：cf5a63d3→bb14b81a，11a424c9→8a2d6951，ac1c57e9→f3190f4470996232aa63477838b8979d66dd8e45。没有把研究HEAD与f84整段diff当补丁，没有整文件/目录覆盖产品。研究自有LegacyExportMemory工具按ce9单独导入，属于受控验收，不变产品源码。

f84拒绝可见提示、预约受保护链接直达JS及对应WPF/JS文件blob与候选一致；MainWindow/App关闭协议、像素节点公式、旧配方与发行RAW解码器未由显式三笔改动。F84_PRESERVATION给出精确blob。

## 重新审查RGB48 backing

- 仅既有HighBitDepthImageBuffer新增私有ownedRgb48表示，规范方向时从解码数组复制为独占ushort[]；外部数组后续修改不影响源。现有公开ushort构造仍按旧合同展开；新拥有路径通过专用内部工厂，不擅自声称所有输入都改成RGB48。
- CopyRgb32/ToVisualRgb24/ToRgb48与proxy/16行HighPrecisionExportRows按原ushort/65535f逐样本消费；RGB48源不被行借用者释放。像素和舍入不新增数学。
- 兼容Rgb32在backingGate锁内一次展开，清内部ushort引用；并发调用返回同一缓存。ReadSamples捕获ReadOnlyMemory快照，即便另一调用展开/Dispose，已取得读视图的托管数组不会被池复用破坏。Dispose拒绝后续读取；不替其他借用者销毁共享master。
- 取消在行/解码/方向循环内传播；新未发布图像失败才由本scope释放。8方向、全部65536值、多个借用者/取消/失败与复杂fallback测试已执行。
- 这些bufffer不直接进入工作文件JSON；配方/方向语义不改。旧序列化和session转移回归保留。图/空间/复杂合成保持原整帧fallback，仍可能显式触发Rgb32兼容展开，不能推广40MP容量。
- 公司legacy实测每个master快照均floatMaterialized=false，source/Editor共享对象按id去重。首次新STOP发生在第二次解码结果ushort分配阶段，不是错误地又展开一个完整float。

旧PATCH_REVIEW针对11a的2378MiB是历史。ac1的处理3轮/中性3轮/Sony2轮、85/20原受控通过重新阅读并保留其范围；不继续复用旧拒绝理由。本轮f319在公司实际缓存/原TIFF的新失败是独立准入依据。

## 公司材料当前身份

准确授权TIFF存在：241121559bytes，mtime2026-11-03T10:39:00.008Z，SHA0da1bf38e42b0b76aee28f2b9a0af013ceff4cb9c46102128732d0626d667c6f匹配。

准确恢复JSON存在：6851bytes，mtime2026-11-19T06:59:52.924Z，SHA a321c8a74c86b9656b508deb640f247da70dadc18372d78088815a9acd0a21de。实际1目标、BasicTone exposure0.1、stackVersion2/ProcessingVersion2、rawDefinition=null、活动版本82858053-bb81-94a8-cd29-61e03f1a1adc；不能从两个历史摘要替它猜当前版。完整定义见MATERIAL_CURRENT。

只复制两份文件到独立authorized-copies；新工作副本只改目标路径指向TIFF副本，原件不写、不遍历私人图库。后续普通保存与另存快照全在本轮根。

## 自动回归与夹具边界

Core85/85、WPF26/26均通过、无跳过，含原ac1的85/20类及家庭拒绝/关闭等。本轮受影响JPEG选片准备、客户所选/批量导出、持久重开、关闭额外组初跑47PASS/1FAIL。失败旧夹具同时空名称与数量0，却等旧数量优先提示；当前f84已批准名称必填优先，产品相关文件未被三补丁改。只让数量用例先有合法名称/客户来隔离数量校验，单独复跑1PASS。原失败TRX不删、不算产品退化，也不把重叠filter累计成唯一测试总数。

家庭f84完整JPEG普通闭环和真实iPhone/PC双向通过按baa1原回执保留；未重签凭据或改预约。本轮不声称重新实机验证。

## 普通公司TIFF：首份正确，同进程重开失败

最终候选普通KitaoPhotoSelector.exe PID26780，独立DataRoot，live版本2.3.0+f3190f44。主输入通道几何不可用，备用已有UIA控件模式/系统文件选择器实际打开工作副本；不是注入ViewModel命令。

已做连续曝光0.2/0.3、调用撤销/重做、恢复0.1、照片页100%按钮、保存、系统目录选择器实际导出。数字0.1与已保存读回；普通Canvas内部实际zoom/处理图全尺寸没有额外诊断读回，不夸称完整屏幕色准。所保存定义与导出侧车可核对。首份实际TIFF用未改Core点节点逐行oracle比较全部5184行/120559104 RGB样本：0差，最大差0，RGBA64即每通道16位，alpha0差、方向1、ICC3144/2B3AA164…一致。

系统对话框打开首轮已保存快照，确认替换当前批次后，Private2203717632bytes=2101.63MiB越限，OS峰1950105600bytes。外部双2GiB/50ms守卫STOP，exit1；完整lifetime含调查/工具等待，不是导入耗时。故至少三轮、同定义跨轮比较、正常关闭/独立进程最终导出均NOT_COMPLETED，不继续把首份PASS外推。原件和已保存副本仍在。

## 公司legacy缓存：首轮100%/保存/导出完成，重开分配失败

合法已存0806D680…payload可用，未重新解码RAW。f319同源码托管宿主加载生产VM/可见照片页与隐藏旧页，编辑开启、匹配图全尺寸、Zoom1；源master241118208bytes/floatMaterialized=false。首轮保存导出完成，观察到已登记analysis/detail/lightPrint/frame任务完成后再重开。

第二次约230MiB ushort数组读取阶段Private2176217088bytes=2075.40MiB，OS峰1642942464bytes，2GiB守卫STOP。未关闭处理预览、没有forceGC/裁工作集/加等GC延时/重置旧账本。是受控legacy路径失败，不是普通legacyUI通过；不会拿TIFF结果替代它。公司现有cache尚不能安全补普通legacy入口，避免任何cachemiss触发原RAW。

## 当前堵点与最小后续范围

新公司受控legacy在首轮100%时Private约1844MiB，sourceRGB48与原/处理两个完整CachedBitmap仍在；保存时managed估计回落而private提交未同幅下降。释放后旧弱引用仍活但并非堆根证据，scope任务已完成；第二个外部解码数组连续分配使private越限。ac1节约完整float已经实际生效，但无法保证OS/WIC/LOH提交及时回收及另一个230MiB结果数组能容纳。未采集新堆根/分配栈，不能称已证明引用泄漏或归为特定未观察WIC对象。

普通TIFF另走native位深栅格/prepared工作区暂存，newac1主要是legacy源表示，未覆盖所有同进程替换时旧/新源和处理图重叠。两个场景各自新STOP保留，不把它们混成同一根因，也不能用home合成3轮成功反证公司失败不存在。

后续最小定位/修复应限定：1)原root工作区替换/已完成渲染与WIC持有者的真实根/寿命；2)RAW解码结果和独占source的分配/转移契约及整个在途工作集准入；3)原TIFFprepared新旧图重叠。不能靠弱缓存、复用未拥有数组、取消但不drain、forceGC或关闭处理预览过门；颜色数学和旧格式保持。不在门失败时直接准入候选。本轮没有声称修复这些更广容量问题。

## 完整包与交付

候选f319和稳定f84均构建App/SelectionService/ScheduleService/native-host及原扩展，16个EXE/DLL逐项版本/哈希核对。候选不提供当前默认启动器，显著CANDIDATE_HELD标记。当前唯一产品入口为同目录CURRENT_PRODUCT_AND_ADMISSION内f84 Start-FullProduct-LocalTest.cmd；已实际启动并再次调用确认复用同一个桌面/服务，无第二写者。

f84公司路径/独立DataRoot重新生成，不导入家庭/原项目运行库。旧包不改；新完整zip/manifest/最终SHA由包外PACKAGE_DELIVERIES与REMOTE_READBACK记录。source f319是产品功能源码；之后夹具修正/工具/报告提交另记，不篡改已运行EXE版本。

状态止于PARTIAL/不发行。候选Private余量：普通TIFF−53.63MiB、legacy−27.40MiB；不是正余量。有限条件之外不作大图无限稳定承诺。原材料、恢复定义、失败STOP、全部采样和截图/控件回执保留本轮原始根，Git不含图像/数据库/凭据/dump。
