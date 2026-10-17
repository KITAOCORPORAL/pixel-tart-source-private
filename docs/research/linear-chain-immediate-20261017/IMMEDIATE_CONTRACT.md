# 即时结果／演示合同

新增运行偏好InstantDevelopmentDemo默认false；false计算完直接提交，不再经过4s时间线。原InstantCameraAnimation只在演示中选择相机/退片展示，不偷偷控制最终像素。演示开关和skip不写入配方/seed，skip按钮在参数锁定之外保持可用。

每次应用decode/cache、pixel、demo、commit单独计时，demoMs不把源hash复核算作等待。取消/切图/revision/version身份在提交前再检查，即使跳过演示也不能绕过。每次应用只提交一次历史；即时与skip同像素/配方/seed测试通过。

旧测试原来隐含“所有应用必须等待4s”，现显式启用演示后保留原4000ms/相机退片时序断言，不放宽；新增即时<3000ms小图门槛另测。真实尺寸由实际计算决定，不保证大图毫秒级。

06-focused执行21通过、2条件未运行，无执行失败；条件为既有授权/环境测试。原05-focused演示测试失败保留，不把新运行偏好当旧像素变化。
