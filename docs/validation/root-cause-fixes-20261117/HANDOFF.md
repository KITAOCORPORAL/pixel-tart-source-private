# 根因修复短交接

先读REPORT.md和SOURCE_BINDING.json。用户已批准B分阶段实施及名称必填；不要恢复“未命名项目”或误把旧待审文档当本轮未授权。

独立分支fix/root-cause-b0-b1-b2-20261117，基线f6655aff。B0 f55825bb：互斥量实际获取/abandoned，激活超时不杀进程。B1 057d58ff：根关闭共享尝试，捕获/复核版本，等待画布任务，暂停与终止分离，保存失败重试/终止故障分阶段。B2=eef3b35b；最终功能源码433a1d8e88d839839e4f21341e3e2776696d33a5（反例补充终止期编辑禁用、保存IO后名称复核）：名称前置必填、目标版本校验、明确创建结果。

受控：B0多root/慢启动/未保存内容/互斥量与abandoned；B1 16项+1故障反证；B2三类空名/其他输入保留/零暂存与记录/补名成功；自动回归52/52。2GiB/50ms、无强制GC、新RAW0。原始失败夹具/编译日志留在E:/Kitao-Deliveries/root-cause-fixes-20261117。

普通EXE验收未完成：前候选eef的App2004和选片服务40608曾启动，新source目录选择器尚未确认，工具返回物理Escape停止控制。最终433未在工具停止后再启动；勿把受控、eef候选或旧e818回执当最终普通闭环；恢复时先核对PID/创建身份，见ordinary-qa/CONTEXT.json及native-first/LAUNCH.json，不盲信历史PID。原用户工作区未动，不强杀QA窗口。TailscaleNeedsLogin，真机NOT_RUN。

完整产品包及SHA见DELIVERY.json，EXE为包/App/KitaoPhotoSelector.exe；Start-FullProduct-LocalTest.cmd采用独立DataRoot。源图/DB/key/token不在包内；无安装器。旧40MP像素/ICC回执保持原source，当前普通关闭/重开需补验。主线与调查树保留，未混入其他研究线。
