# 产品内存修复整合与无需新增RAW的回归

日期：2026-11-12。状态：**INTEGRATED_NO_RAW_AUTOMATED_PASS / PHYSICAL_ENTRY_PARTIAL**。

## 整合基线与范围

独立分支 integration/product-memory-20261112，以当前公司产品1e6bcf3019e3972667d3cda0b8be5c0c3c56bf6f为基线。merge-base核实该线包含Chrome批量侧栏057fe820和此前采集0eda7786；未按分支名称推断整合。原公司产品树、人像研究树和内存研究树均保留原HEAD与工作内容。

审查后分别移植a42cb7cd→18d27c08、b86d0245→6ccc5230。**整合产品源码SHA：6ccc5230065aa2c07f6068a7fb8b3271b8a9733a**。生产差异只有：

- RawDisplayBitmapAdapter：完整零偏移RGB24 backing直接供WIC创建，切片仍复制。
- ReferenceColorWorkspaceView：不可见、未加载或零尺寸视口不请求整图。
- PhotoGradePageView：可见照片页自己拥有100%/视口细节请求。
- TetherReferenceModeViewModel：关闭清除完整显示引用。
- ReferenceColorWorkspaceViewModel：关闭清除分析图像与派生引用。

另带入5项内存行为测试。没有合并整个研究分支，没有导入22a后继RAW授权逻辑、X-Trans候选、MaxChannelShoulderV1或亮部observer变更；Core颜色/解码、手机/浏览器模块源与基线一致。

## 自动回归与缓存生命周期

受影响测试10/10通过、0失败：RawPostImportMemoryTests5、StudioRawDetailPreviewTests2、RawMatchTiff16ProductWpfTests3。分别在隔离运行根/进程执行，未读日常公司库；不是整个solution或真实RAW验收。

公司已存缓存副本7752×5184 RGB16，SHA=0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E。重放只注入缓存读取器；伪RAF仅是本任务生成的说明文本，没有调用相机RAW解码器，所有快照nativeRawLoaded=false。

| 阶段 | 观测PrivateBytes | 结果 |
|---|---:|---|
| 首次适合窗口自然空闲 | 1073.12 MiB | 1600宽代理，无隐藏整图请求 |
| 显式100%后自然空闲 | 1682.95 MiB | 7752宽完整显示 |
| 200% / 10% / 100% / 15% / 100%切换 | 约1656.92–1683.71 MiB | 重用完整显示；缩小时保留其缓存，不误称已释放全部整图 |
| 保存项目 | 1658.12 MiB | 实际SaveSessionCommand完成，schema1 |
| 关闭工作区自然空闲 | 1615.87 MiB | 不再发布源图；未把未收集垃圾计作已释放 |
| 关闭后的诊断GC快照 | 1300.51 MiB | 单列诊断，不是生产强制GC |
| 从保存项目重开、自然空闲 | 1258.86 MiB | 缓存重放读取累计2次；RAW0，源图1600宽 |

生命周期整次峰值PrivateBytes=1733.27 MiB，OS峰值WorkingSet=1522.11 MiB，未触发2 GiB。完整阶段计数器/对象身份见RESULTS.json。

另外执行210秒最小化自然空闲并恢复：期间PrivateBytes约1069.64–1076.88 MiB，恢复约1098.38 MiB；整次峰值1100.65 MiB，未触发守卫。此场景没有强制GC直到测量结束后的释放阶段。WPF/原生残余仍可保留，不宣称关闭后PrivateBytes归零或所有图形缓存已唯一归因。

## 一致性与输入

保存/关闭重开前后的导出输入均按完整帧逐行量化读取，SHA均为0806D680…，与冻结缓存一致。完整原尺寸RGB24读回覆盖120,559,104字节，hash与前次C81DB4E20BAF5E8A6A55CBDD9C844740B717EE5655DC87C7634D39A3CC02D14E一致。preview/result/code都没有改变曝光/颜色公式或缓存身份。

这些是通过真实产品ViewModel/视图类的注入缓存重放及测试，不是鼠标入口验收，也没有假称完成实际相机RAW或真实整应用TIFF导出。本轮只验证导出输入合同及既有合成产品导出回归。

缓存生命周期第一轮在项目已重开后，诊断脚本用错JSON字段大小写导致KeyNotFound；原日志保留。更正为实际schema的version并加完整导出输入指纹后，cache-lifecycle-02正常完成。该失败不记为产品失败或通过。

## 全模块诊断构建

构建参数WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，自包含win-x64。EXE ProductVersion明确绑定2.3.0+6ccc5230065aa2c07f6068a7fb8b3271b8a9733a。

App保留全部产品模块入口及代码；ScheduleService359文件、extension16文件、native-host192文件从当前公司公开诊断包逐字节保留，未重新注册、启动或操作现有手机/浏览器会话。当前这些子件没有被重复验收，不拿内存改动冒称手机/浏览器新验收完成。新启动器使用包外独立product-runtime，不含日常数据库或设备owner key；未生成Installer。

为原生UI工具区分同名公司进程，添加字节完全相同的GuangjingMemoryAcceptance.exe apphost别名，仍加载同一KitaoPhotoSelector.dll与相同源码版本。常规启动仍用Start-Guangjing-Diagnostic.cmd。

## 实际产品入口结果与现场阻塞

已从本轮发布EXE正常启动，独立运行根，原生可访问树验证了完整模块侧栏；使用侧栏“暗室”进入实际产品，再点击可见“导入/导出”打开菜单。菜单实读包含“导入照片… / 打开项目… / 保存项目 / 导出主变体… / 退出暗室”。

桌面连接随后未能可靠投递导入菜单项：Sky截图FrameArrived超时/无几何；focus-window明确报告未成为foreground；agent-desktop新ref重读后两次菜单项严格身份解析超时，disposition=not_delivered。没有重复打开TIFF，没有启动RAW，也未操作另一份正在做手机日历验收的公司实例。

| 真实入口 | 状态 |
|---|---|
| 发布EXE正常启动、隔离根、全模块侧栏 | VERIFIED_ACCESSIBILITY |
| 可见侧栏暗室 | VERIFIED_NATIVE_SEMANTIC_ACTION |
| 可见导入/导出菜单 | VERIFIED_MENU_SURFACE |
| 选择已显影TIFF并完成导入 | BLOCKED_BEFORE_DELIVERY |
| 物理界面适合/100%/缩放/关闭重开 | NOT_RUN_PHYSICAL_UI；自动缓存/视图测试结果分列 |
| 实际产品导出 | NOT_RUN |

当前只保留本轮隔离诊断实例（GuangjingMemoryAcceptance，PID432）和2 GiB记录器，停在导入菜单。需人工让该窗口取得可交互前台，再选择已授权高位深TIFF，才能继续真实入口后半段。目标是已经显影的公司DSCF0347 TIFF，不是RAF；因此后续本轮操作也不新增RAW。

## 保全与研究边界

原desktop-02的采样、输出、缓存、STOP及22项现场hash保持。旧RAW批次消费1、可执行0；真实后继DRAFT02保持原报告状态，源b86d0245、拟议6、授权0、无新ledger；未擅自将本轮整合SHA写入或激活该草案。

高光候选继续研究，未出现在本次产品差异中。1bfdfbe9 EV0代价记录：中性白Y1→0.875（约−0.193EV）；肩部高亮meanY约−10.9%，暖纹理约−9.0%；合成中性亮纹理的相对局部梯度RMS约−39.5%。后续按显式高光压缩工具评估，真实白衣/金属等缺项及原高亮已裁切不可恢复边界保持。

本轮结论为：产品代码整合与无需新增RAW的自动回归完成；全模块诊断构建已准备；真实产品入口后半段因桌面投递阻塞仍待完成，不能标READY_FOR_RELEASE或完整桌面验收通过。
