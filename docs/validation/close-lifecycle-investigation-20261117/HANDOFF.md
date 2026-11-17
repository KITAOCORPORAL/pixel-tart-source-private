# 关闭/图像生命周期交接

调查基线fdf01831；统一功能源码e8180221；产品src/tests零修改。产品主线聊天01a1e355-64ea-74e0-b7b5-15ec557fdc30维护AGENTS与主线整合，调查只维护本目录和tools/CloseLifecycleInvestigation。

先读REPORT.md及OPTIONS_AND_MINIMAL_PROPOSAL.md。最终15个独立进程探针加1个资源重复均得到预期观察，包含同栈关闭重入和终止后晚到图像等缺陷，绝不能写成产品15项通过。PrivateBytes峰值145MiB、无2GiB STOP、无强制GC、新RAW0。原未保存暗室工作文件只读哈希前后一致；旧画布revision未知。

已证实C1 nested pump使Yield仍在Closing内重发Close；C2独立重发覆盖别的处理器一次取消；C3隐藏目标使关闭意图丢失。I1卸载后provider晚到发布，I2卸载后Editor.Changed重启预览，I3Page释放没有drain画布任务。旧现场具体哪个nested pump仍未知。独立主线跨root误杀夹具另有外部归属证据。

推荐B：根窗口单次关闭协议+画布可等待生命周期，拟改5文件，先保存和取消判断，再终止发布/drain/释放自身引用；保留89b64fd2保存失败重试、所有像素/内存身份补丁。产品实施必须用户审核，当前未实施。RULES为本调查短规则，不改根AGENTS；SKILL_PROPOSAL只是窄范围提议，未创建。

原始根E:/Kitao-Deliveries/close-lifecycle-investigation-20261117；runs/v2为最终矩阵、repeat-resource为额外复现；smoke和v1失败夹具全保留。数据库/源图/采样不入Git，不动用户原工作区。时间以各原始UTC回执为准，未重复主线14天全库统计。
