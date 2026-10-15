# 实机与输入观测边界

2026-10-15检查本机desktop窗口可用性：存在Chrome/微信/Downloads等其他应用；只做窗口清单，不读取无关正文/截图，不发送输入。独立只读GetForegroundWindow/GetLastInputInfo记录前台Chrome，37.11秒内有人输入；该共享桌面没有无人使用的保留时段。按用户“前台其他应用占用/争抢就停止”的规则，本批没有把合并版拉到前台抢输入。

- 合并版产品EXE启动：NOT_RUN（不是后台测试启动成功）。
- 工作台→策划/Color Studio实际导航：NOT_RUN。
- 原目录搬走后的实机插图/重开/undo/save/export：NOT_RUN；A搬迁自动测试在本合并程序集Passed，另记。
- 照片切换/取消：NOT_RUN。
- 100%–200%物理DPI：NOT_RUN。
- WPS真实打开编辑导回：NOT_RUN。
- 视觉批准：NOT_APPROVED。

实际授权RAW已按原CompanyRAW清单哈希全部匹配；本批只读探针对Canon/Fuji最终RGB16执行成功，Sony旧0.21.1仍RAW_NATIVE_FILE_UNSUPPORTED，未偷偷切换候选。记录容器元数据/EXIF、decoder版本、域/位深/裁切及source前后hash。黑位前、黑位后矩阵前、已WB矩阵前内部阶段均UNAVAILABLE，不能用最终RGB16反推。候选0.22.2固定exe/DLL存在且hash一致，但下一批RAW双实现数值对照未做，未启用/捆绑。

大图Structure在授权Canon5496×3670（20.17MP）上独立进程测full/tile/cancel；full/tile输出float字节hash完全一致，max绝对差0。tile7.22s/full4.23s，tile峰值working set约2.01GB/full1.85GB；本次tile更慢且峰值更高，不改生产空间算法、不宣称优化。取消110ms；ledger回零不等OS回落，3秒无强制GC序列仍未回到初始工作集。原生allocator归属UNAVAILABLE。
Details full同尺寸本次可运行约18.43s，不伪造预算拒绝。历史BUDGET_REJECTED仍保留，不据一次运行提升所有大图oracle覆盖。此批只选Structure做full/tile完整对照；其他节点大图完整对照未做。

CalibrationEligible=false。没有实测色卡、光源/扫描闭环，不证明A7M4绝对色准或宝丽来精确复刻。本批不要求购买色卡。
