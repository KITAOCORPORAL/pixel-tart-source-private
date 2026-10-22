# 镜头独有外链重连 · 家用开发验证

2026-10-22 Europe/Berlin。产品代码已交付；原生桌面闭环 BLOCKED；不能发行，本轮未制作安装包。

## 起点与保全
先 fetch，再独立 ls-remote 确认 home/planning-external-reconnect-20261022 = 23b0d8916a7e831583a84e790b5d10755ed329d3；旧树干净。从此新建 N:/pixart/guangjing-home-shot-reconnect-20261022，分支 home/planning-shot-external-reconnect-20261022。18 个旧工作树 HEAD/branch/status 读回不变。未改旧树、ZIP 或数据根，未 pull/reset/clean/stash/rebase 旧树，未合 main 或强推。按用户要求没有重复全盘/ignored/ZIP 内容扫描。

产品源码：c80b5e719dce8fab73f27c0c3e5b48c80c597c3a。

## 产品与持久化边界
ProjectShotReference 属于独立 .shots.json；PlanningDocument.References 属于 .planning.json，两集合可共享 ReferenceId。新增 PlanningDocument.ShotExternalLinks 收据记录原 ReferenceId、OriginalPath、具体 ShotIds、本机核对/自有副本；不把镜头独有项塞进文档 References，也不重写镜头文件。

缺失入口按 ID 合并文档/镜头引用，显示镜头序号、名称、完整 ShotId、原路径和原因。相同 ID 且同来源可共享一次确认；同 ID 不同路径/库身份或附件冲突拒绝重连，且不读取冲突预览。素材库项禁用定位，档期/账本继续明确未支持。

沿用原格式/大小/文件头/SHA-256、设备/ADS/重解析路径保护。确认后重查文件和镜头集合；定位期间项目切换取消提交。已批准文件变化时不回退原路径、解析器或旧预览。已有副本不能被同 ID 降级或替换。

写入采用 shots gate → planning gate，并保持 shots 文件只读 lease 防止 Windows 跨进程替换。副本完成复验后只原子替换 planning 文件；无须假装两个 JSON 同时原子提交。保存失败仅删除 created 列表中的本次新副本，旧副本及策划/镜头字节保留。包导出、校验和恢复包含镜头副本；重新导入丢弃本机外链批准，保留已打包并核对的自有副本。

## 最终发布组件绑定回归
Windows x64 自包含诊断 EXE；WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，SDK 10.0.401。测试目录的产品 DLL 逐 hash 绑定到发布目录，见 BINDINGS.json。

|范围|通过|失败|条件未执行|
|---|---:|---:|---:|
|Core Planning/ProjectShot|58|3|0|
|WPF Planning/ProjectShot|88|0|2|

新 Core 7 项（6 通过、1 权限失败）、新 WPF 2 项通过均包含在表内，不重复相加。覆盖镜头独有外链/副本、两个镜头共享文档 ID、来源冲突、重复操作、取消、确认后变化、保存失败只回滚新副本、镜头文件字节不变、重建预览、当前根及独立根项目包重导入清本机批准。

3 个 Core 失败均在创建合成符号链接时抛 IOException“客户端没有所需的特权”：
- SourceAndDestinationReparsePointsAreRejectedWithoutFollowingThem
- ReparseSelectedFileIsNeverFollowed
- RecoveryKeepsLiveAndRecentStagesMalformedOwnersAndReparseTargets

未改变系统策略、未记 PASS。WPF 2 个条件未执行保留原值。不是全软件回归。

早期保存失败回归只预期 IOException，本机返回 UnauthorizedAccessException；改为接受这两种真实 OS 失败后继续严格核对 rollback 和旧字节。构建错误日志保留。wpf-r2 在构建失败时误跑旧 DLL、只跑 1 项，为无效旧程序集结果，不计通过；r3/r4 和最终发布绑定测试已包含新 2 项。

## 真实 Windows EXE：受阻，不冒充完成
本轮 PID 33292 启动 EXE 和全新隔离根，实际观察到诊断标题/教程。点击退出教程后，目标截图两次显示其他前台应用；一次 activate_window 恢复后仍不可靠。停止进一步输入，未持久化无关应用截图。仅对本轮自有程序调用 CloseMainWindow，返回 true，确认退出。

未完成原生项目包导入、镜头定位确认、保存、正常 UI 退出、新进程重开和预览。WPF 的 ViewModel 重建/预览测试不替代这些未执行项。DESKTOP_STATUS.json 和 MANUAL_RETEST.md 明确记录。

## 剩余限制
- 原生闭环待桌面可靠捕获时人工复验；不能发行、无安装包。
- 同 ID 不同路径保守拒绝，不猜路径等价、不扫描私人目录。跨格式替代仍拒绝；签名白名单不是杀毒/完整图像解析。
- 外部删除镜头或改变来源后不会自动认领别的 ID；不一致收据会让项目包校验拒绝，需要后续明确管理，不会自动修复私人目录。
- 自有副本的安全预览/汇总沿用现有 PNG 限制；其他接受格式可存收据但不保证图像呈现。
- 仅查询工作树状态保全，未重做旧所有文件/ZIP 内容 hash。

RAW_TESTS.zip、RESULTS.json 为原始/逐项结果，samples 为合成包和替代 PNG。发布 EXE 仅保留在新实验目录 final/app，没有提交二进制。产品与本验证记录分别提交。
