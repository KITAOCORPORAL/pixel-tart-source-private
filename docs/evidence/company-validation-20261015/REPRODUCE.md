# 公司A/B集成：复现方法

日期2026-10-15。工作树D:/AI AGENT/kitao-company-validation-20261015；SDK D:/AI AGENT/.dotnet/dotnet.exe 10.0.302。仅进程环境变量隔离，未改系统配置。

## 阶段1完整合并验证
使用8c3986e原合并版本（双亲dc67b44）顺序build Core/WPF/tools/VerifiedTestRunner，运行verified_runner.py完整Core和WPF。原严格NotExecuted为FAILED；用review_gate.py逐名分类单独判断审阅。AB_EXECUTION_AUDIT逐项核对A7+B8，不以分类存在当执行。
原合并版发布在artifacts/company-validation/merged-release/win-x64，另建只读源码worktree构建，和随后性能修复版隔离。发布身份明确8c3986e，不能当后续3d526ae版本。

## 阶段2性能
1. 原必需Core测试参数及门槛不变，KITAO_P3_TRACE指向新目录时记录SQL/参数/EXPLAIN及每轮预热+三次warm输出。
2. P3Probe生成新10128条独立DB，拒绝复用目录；3个warm最大值不能只取最好。查询数据不读用户库。
3. P3UiProbe将该DB复制到新目录，以真实P3QueryScope+后台Dispatcher观察切换（不Show产品窗口）；输出refresh/idle/gaps及SQL/timing。before与after各自目录，不能覆盖。
4. CompanyQueryAggregateTests验证SUM与分页oracle跨scope/archive/filter；CompanyScopeSwitchTests检查首屏500排序/数量、global count不变及普通刷新仍重算。
5. 最终src/tests冻结3d526ae后重新完整Core/WPF；其中此前画布popup失败保留，修复为精确owner menu身份，原paste/对象数/zoom断言不删。

## 阶段3ICC研究
参考仅本地artifacts/company-validation/icc：LCMS2.16 conda-forge包（下载sha与API metadata核验）、Argyll3.5.0官方bin/src归档、skcms固定commit。参考源码/二进制不入Git/发布。
先用Zig0.16本地c++编译：
```
zig c++ -O2 -std=c++17 -DSKCMS_PORTABLE -DSKCMS_DISABLE_HSW -DSKCMS_DISABLE_SKX -I <skcms-root> tools/company-validation/skcms_probe.cpp <skcms-root>/skcms.cc <skcms-root>/src/skcms_TransformBaseline.cc -o <research>/skcms_probe.exe
```
`python tools/company-validation/icc_compare.py`生成固定日期原创ICC并运行三套真实实现；`dotnet build tools/company-validation/SyntheticIccProbe -c Release`后执行`icc_subset_compare.py`比较受控reader。`icc_reference_memory.py`记录独立参考进程样本。脚本输入/输出路径按本工作树约定，参数、profile/EXE/DLL hash见manifest。没有把参考算法源码复制进wrapper。
Argyll拒绝v4、skcms绝对意图未提供控制、LUT不一致与损坏profile宽容处理均保留，不通过乘2或增容差掩盖。

## 大图和真实RAW
RawInputProbe在授权原件hash核对后仅执行旧LibRaw只读解码，记录final RGB16；黑位前/矩阵前写UNAVAILABLE。LargeSpatialProbe使用同hash Canon5496×3670、独立进程mode tile/full/cancel，source前后hash、无强制GC的3秒释放序列；原生allocator归属不可见。不要把“private-managed”差额冒充原生分配所有权。没有改生产空间节点。

## 发布/人工
最终组件testhost只替换发布目录7个产品DLL，并逐个SHA一致。Core身份测试设置PIXEL_TART_BATCH12_RELEASE_CORE为测试host内实际发布Core，单独精确类Passed才消除发布依赖，不改原strict skip。
实机需无人争抢的前台会话，本轮foreground为Chrome且近期有输入，因此没有发送输入或启动产品抢占。Windows导航/真实拖动/DPI/WPS均NOT_RUN。
