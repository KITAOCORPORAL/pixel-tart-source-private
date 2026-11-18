# 根因修复短交接

**2026-11-18最新：** 用户允许访问后，最终433窗口置前与文件夹字段写入已成功；旧“拒绝访问”不再是本次复现结果。当前是系统模态对话框子控件缓存/捕获限制，尚未观察确认成功。路径已填，只需在“选择照片来源目录”窗口点底部“选择文件夹”（如看不到，先缩小该对话框）。见resume-20261118/RESULTS.md。项目和来源数量不变，未进入JPEG/TIFF闭环；当前手机GET200、匿名401，但仍NeedsLogin。继续前仍需重新核对窗口和进程创建身份。


## 最新接续状态

最终433已在独立acceptance-433a1d8e-20261117运行根真实启动（App42840，服务36564，创建身份另有回执），完成普通保存入口的空名/三空格拒绝与有效名称保存；项目08d18374-fb96-4efb-9451-cae9b8c48c40，来源0、相册0。当前在最终版“选择照片来源目录”系统窗口，工具置前GetCursorPos拒绝访问，已停界面输入。本人需在文件夹框填 E:\Kitao-Deliveries\root-cause-fixes-20261117\acceptance-433a1d8e-20261117\source-jpegs 并点击“选择文件夹”；继续前重查PID/FILETIME，按当前读回而非历史PID操作。不要操作旧eef窗口。

先读final-acceptance-20261117/RESULTS.md及SOURCE_PICKER_HANDOFF.json。原包1473项文件/ZIP哈希当前读回一致，仍只有既有433包。授权TIFF哈希/文件头当前确认，但未普通导入/编辑/导出。手机当前token有效、匿名401、预约r4，TailscaleNeedsLogin；当前17662服务仍e818，未标433真机通过。没有旧登录链接复用。以下保留原交付时的断点作历史。


先读REPORT.md和SOURCE_BINDING.json。用户已批准B分阶段实施及名称必填；不要恢复“未命名项目”或误把旧待审文档当本轮未授权。

独立分支fix/root-cause-b0-b1-b2-20261117，基线f6655aff。B0 f55825bb：互斥量实际获取/abandoned，激活超时不杀进程。B1 057d58ff：根关闭共享尝试，捕获/复核版本，等待画布任务，暂停与终止分离，保存失败重试/终止故障分阶段。B2=eef3b35b；最终功能源码433a1d8e88d839839e4f21341e3e2776696d33a5（反例补充终止期编辑禁用、保存IO后名称复核）：名称前置必填、目标版本校验、明确创建结果。

受控：B0多root/慢启动/未保存内容/互斥量与abandoned；B1 16项+1故障反证；B2三类空名/其他输入保留/零暂存与记录/补名成功；自动回归52/52。2GiB/50ms、无强制GC、新RAW0。原始失败夹具/编译日志留在E:/Kitao-Deliveries/root-cause-fixes-20261117。

普通EXE验收未完成：前候选eef的App2004和选片服务40608曾启动，新source目录选择器尚未确认，工具返回物理Escape停止控制。最终433未在工具停止后再启动；勿把受控、eef候选或旧e818回执当最终普通闭环；恢复时先核对PID/创建身份，见ordinary-qa/CONTEXT.json及native-first/LAUNCH.json，不盲信历史PID。原用户工作区未动，不强杀QA窗口。TailscaleNeedsLogin，真机NOT_RUN。

完整产品包及SHA见DELIVERY.json，EXE为包/App/KitaoPhotoSelector.exe；Start-FullProduct-LocalTest.cmd采用独立DataRoot。源图/DB/key/token不在包内；无安装器。旧40MP像素/ICC回执保持原source，当前普通关闭/重开需补验。主线与调查树保留，未混入其他研究线。
