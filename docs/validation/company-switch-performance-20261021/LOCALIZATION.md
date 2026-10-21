# 性能定位（产品修复前）

2026-10-21，Asia/Shanghai。家用原始32139.0909ms >30000ms真实失败保留。公司原Scenario11独立单跑3628.9637ms；同发布Core冷进程3418.1982ms；预热后三次3270.0605/3298.6967/3324.9674ms。固定四WPF并发同条件新三次见原始performance/concurrent-baseline-controlled，非同一次完整WPF负载、不证明家里因果。

诊断镜像10k中9998缓存命中、两次SQLite验证。阶段累计：路径normalize26.84ms/root文件检查215.29ms/指纹元数据437.14ms/缓存物理路径2772.75ms/SQLite验证3.61ms；缓存物理路径约80%。14万GetAttributes、7万File.Exists、6万Directory.Exists为诊断计数；进程逻辑I/O640000级，不等于物理磁盘等待。仅前2次有SQLite，热打开不获取writer lease，锁等待不是热路径已证瓶颈。

最小修复方向：每次Open仍检查根及7个固定布局组件，消除一次调用内共享根/父组件和Exists+Attributes重复查询；禁止跨调用缓存重解析点安全状态，不改变manifest指纹/SQLite冷验证/容器身份/排序筛选/锁协议。实际修复与回归单独提交。

物理磁盘每调用等待、SQLite busy等待不可独立归因，保留null和明确限制；磁盘总体采样不冒充该归因。家里慢于公司尚未复现，不声称已修复唯一32秒根因。
