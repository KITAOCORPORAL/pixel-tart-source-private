# 连续曝光后的大预览持有问题与当前复验

cb8c1adc 的普通 PID56004 在已载入40MP恢复文件、曝光+0.2可见后，撤销阶段再次触发2GiB守卫。50ms采样 PrivateBytes2227568640，exit1，原LAUNCH/STOP/EXIT与摘要均保留。这推翻了“单次分块缓冲修复足以完成普通连续操作”的假设；未提高阈值或强制GC。

分析缓存原本在最多四个 PreviewAnalysisEntry 中各强持有 BitmapSource Image；其计算只需768px代理、统计、色云和图像身份。40MP RGBA64每帧321484800字节，四份约1.2GiB，可与source、当前帧和下次临时输出并存。d07e4f43只把该身份改为 WeakReference，在lookup期间 TryGetTarget；实际分析任务和当前界面继续正常强持有需要的输入。另把编辑器预览缓存的费用从固定4字节改为按实际 BitsPerPixel 和行宽计算，缓存键/像素/导出/历史配方不变。允许既有至少一张大结果；不清理其他持有者的共享缓存。

像素处理、cb分块函数、Core曝光数学、session事务保持不变。已冻结源码两产品文件，四组件exe/dll共8项版本/SHA一致。41项相关回归在分离WPF进程运行通过；首次自定义输出路径host未发现框架，测试未运行，单独保留错误，未混入41通过。

受控生产ViewModel宿主载入同一个授权恢复副本，连续+0.2、+0.3、回到+0.1，撤销、重做、保存、释放后用另一ViewModel重开均通过；相同+0.1完整RGBA摘要一致。50ms采样PrivateBytes峰值1385533440（1321.35MiB），OS峰值工作集1440702464；自然GC，不调用Collect，不新增RAW解码。此为受控宿主；重开是独立ViewModel，不能写成普通EXE关闭、独立进程重开或TIFF导出通过。

普通d07完整包PID56424已启动，独立runtime/副本，当前为空暗室，尚未打开恢复文件。Sky截帧/几何失败；原生Open Computer Use操作与快照多次30s超时；CUA最后AX unavailable。线程快照未发现产品渲染在运行。进程及数据保留，2GiB/50ms守卫仍在。当前仅普通启动可证，TIFF编辑/关闭/独立重开/两次实际导出及位深/方向/ICC仍待验。

手机：原公司合成预约受PHONE_HANDOFF_ACTIVE保护，本轮只更换已证明路径/FILETIME/runtime的owned服务为d07源码，签发新48小时设备凭据并本地读回。预约全行字节一致，revision仍4；匿名401、设备读取200。Tailscale仍NeedsLogin，HTTPS/iPhone实际双向修改尚未完成。新登录链接与设备token仅在本地私有文件，不入Git或包。
