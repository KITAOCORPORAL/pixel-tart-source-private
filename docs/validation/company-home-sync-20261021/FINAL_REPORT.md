# 第一阶段：公司同步与完整构建验证

2026-10-21，Asia/Shanghai。独立fetch/ls-remote家用d39d11d68eb44f5aa0ec8a2f1afc95c98630820d、公司R3 a1e52238616b8d22f09b92595a85e76c7d9c1460均未前进。新树E:/Kitao-Worktrees/company-home-sync-performance-20261021，分支company/guangjing-home-sync-performance-20261021。

产品冻结99d7818e035310c9273565e73752ee0359ebb8f1，HEAD:src树与冻结src树完全相同，家用之后仅证据提交。两个分类/过滤条件接受当前灯位图/服化道与道具并保留旧别名；新增e9b08863测试已读。

公司从源码构建完整x64自包含诊断版：E:/Kitao-Deliveries/company-home-sync-performance-20261021/Company-Home99-Diagnostic-win-x64/app；WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false。290文件、264依赖均存在，独立UserData启动入口保留。SDK10.0.302，运行时10.0.10。主EXE/WPF/Core字节与家用不同，未宣称相同；具体哈希与构建环境见BUILD_IDENTITY/HOME_COMPANY_BUILD_COMPARISON。家里ZIP未入Git，本轮自行构建。

本轮公司：策划Core25/25、WPF64/64；完整Core1714：1709通过、0失败、5跳过，0漏项；完整WPF1715：1695通过、0失败、20跳过，missing=0、extra=0；发布组件14/14。原始TRX和发现逐项核对封存RAW_COMPANY_TESTS.zip。跳过/runner非全绿不改为PASS，家用成绩仅对照。

公司完整Core本轮Scenario11通过，但没有用这一次抵消家用32139.0909ms失败，也不能推出并发就是原因；第二阶段单独测。100k元数据去重在stopwatch.Stop之后，不计30秒阈值。400ms P3查询是另一测试。

保留全部旧工作树/脏文件/旧Release；初始记录与内容hash另列，未pull/reset/stash/clean/rebase/合并main/强推。发行门禁仍BLOCKED：策划外链/普通附件不全便携，15区汇总导出未实现，Windows/WPS/相机等物理操作本轮NOT_RUN。
