# 保留的开发失败
- 初期Core/WPF夹具编译符号/QuickExportFormat/ICC内存类型问题，修夹具，原日志留。
- V2缓存局部变量命名冲突构建失败，最小作用域修正。
- 首轮公司家庭测试硬编码N:/pixart/RAW路径失败；只加显式环境路径，仍验证同一B7D358...hash，不造假源。
- 第一进程独立重开用例因未传项目NOT_RUN；之后精确adapter发现方法单独新进程执行PASS，不改runner状态冒充。
- 取消预览/保存后draft seed重置、Frame缩略零宽为真实产品问题，已修并加最终recipe seed42及布局断言。
- 保留旧数学/未知工具拒绝/原图hash/独立ICC读回，不删除测试、不放宽JPEG或快速误差阈值。
