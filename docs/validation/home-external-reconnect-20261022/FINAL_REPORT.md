# 家用续接、性能复测与外链重连交付

2026-10-22 Asia/Shanghai。**产品功能限定验证完成；全软件/发布验收未完成，诊断版不能发行。** 原始执行主机 China Standard Time，日志原值保持，process epoch/UTC可独立转换。

## Git 与旧家用资料

先 fetch --all --prune，再独立 ls-remote。公司远端 `feature/planning-current-root-import-20261022` = `2d8db4d9931f58af43025004cb794dc35affb8b3`；旧家用 `home/guangjing-whole-app-sync-20261020` = `d39d11d68eb44f5aa0ec8a2f1afc95c98630820d`，公司超前30提交，家用为祖先，无分叉。从公司HEAD新建独立工作树 N:/pixart/guangjing-home-reconnect-20261022，分支 home/planning-external-reconnect-20261022。没有旧树pull/reset/stash/clean/rebase、合main或强推。

17旧工作树HEAD/branch/status不变；211现存未提交文件hash不变；104旧ZIP hash不变。忽略产物枚举有Windows长路径警告，不能声称全部ignored文件逐hash覆盖。对应原始清单/读回在WORKTREE_PRESERVATION、DIRTY_FILE_PRESERVATION、OLD_PACKAGE_PRESERVATION。

## 产品提交

- 9d50bb483abadd7b86e611735e02155ff9ccd453：新增Core重连事务/收据、WPF逐项定位及副本选择、类型/大小/hash校验、summary新位置/hash、取消/变化/冲突回归；修复公司路径字符串断言。
- ca0c497e + b91858ee：根据真实桌面失败，定位期间隐藏modeless子窗，主窗原生确认后恢复，避免遮挡；新哈希加入预览缓存key。
- **最终产品 4f57ca5646b1b6e5f831bdecc8c7847114dfa941**：拒绝跨格式参考图替代（JPEG/TIFF别名除外），避免自有副本扩展名误标。

引用ID、原路径、Category/Group/hero/moodboard不改；仅添加所选文件收据或自有副本。已有副本不可由同ID重连覆盖，重复ID/库引用/错误类型/选后变化/取消均不提交。复制先完成并核对，再在原project gate下原子保存策划；失败仅回滚本操作新建副本。导入别的包清除上机本地定位批准，不能信任包内路径直接读取。未扫描个人目录、不猜同名文件。

范围：文档参考集合的外部参考图、普通附件；仅镜头外链未进入文档参考集合时尚无单独操作。素材库原件、档期、账本、画布/色彩工作区继续列出未包含依赖，不生成这些业务记录。文件白名单/签名检查不是防病毒或完整格式解析。

## 公司成果在家接通（真实EXE，不是VM重建）

基线完整自包含EXE PID12644：退出教程→策划→当前根导入→普通列表→编辑标题“家用实机编辑保存 20261022”→保存→正常完全退出→新PID42548同根重启→普通列表重开→导出15区汇总。原始包二进制未随fetch Git/证据ZIP提供，使用公司提交的生成测试在本机2d8db4d9源码生成的v1包，**不是公司f9747e16样例逐字节副本**。生成测试无关的“路径须含current”断言先失败，保留TRX；改为核对当前确切数据库路径。

b91858ee实机：PID23740，新隔离根，导入合成外链包→缺失清单/原路径/原因→参考选继续外链(.png/3821 bytes/hash)、附件选项目副本(.txt/42 bytes/hash)→磁盘收据核对→参考预览→保存→正常退出→新PID8160→普通列表重开→参考预览仍可见→导出汇总。独立DOCX读回15个Heading1、两项hash均存在。首轮9d50bb48/14856遮挡导致未写入失败保留，不冒作通过。

最终4f57ca56只加格式拒绝保护：重新完整构建/绑定、受影响Core/WPF/模块回归、原性能全部测次；实机PID5364启动并重开上述持久化项目/参考图预览/正常退出。整套原生定位闭环未在这最后格式保护提交重复执行，限定边界已在DESKTOP_RESULTS明确。详细真实动作与截图在DESKTOP_STEPS.md及desktop目录。

## 完整诊断构建与身份

Windows x64 self-contained，WholeAppDiagnosticBuild=true，PhotographerTrialBuild=false；SDK10.0.401，runtime10.0.12，版本2.3.0+最终源码SHA。主程序KitaoPhotoSelector.exe / 主WPF KitaoPhotoSelector.dll / RAWSelectionAssistant.Core.dll / Kernel、3模块、DX12分别记录实际身份。发布264运行时依赖独立读回无缺失。

Core/WPF/模块测试程序集及testhost、依赖逐hash保存；测试宿主拷贝发布DLL后全部同hash。实际WPF testhost进程PID18444的.exe、dll、runtime/Core/WPF/模块路径与hash由独立identity-only观测记录，**不冒充性能进程等待证据**。桌面加载组件另有实进程模块表。

## 受影响最终回归（发布组件绑定）

|范围|通过|失败|未执行/跳过|
|---|---:|---:|---:|
|Core策划|52|2|0|
|WPF策划/DOCX/RichFlow|87|0|2|
|模块宿主|14|0|0|

Core两失败均在创建合成符号链接时Win32错误1314“客户端没有所需的特权”，不是产品拒绝链接测试已经通过；未改系统权限/安全软件。新增4项Core与新增WPF闭环通过包含在表中，不重复相加。WPF两项条件未执行保留，完整原TRX/日志在RAW_EVIDENCE，未声称全软件套件通过。

## 家用Scenario11原合同

原测试blob与0e5c3ea7相同：10000交替OpenAsync，门槛30000ms，100000元数据/90000unique在stopwatch停止后处理。旧原始失败32139.0909ms/TRX/summary保留。原TEMP绝对路径为N:/pixart/Guangjing-Home-Planning3-20261020-225855/FINAL_CORE/run-20261020-233512-51dad32f/000/temp。当前该路径物理映射N: NTFS，本地磁盘3 Samsung SSD990EVO Plus2TB NVMe；原summary未存卷GUID/序列，历史是否重映射仍不能独立证明。

没有更改电源/安全软件/系统缓存。独立新进程、同卷全新TEMP。源基线2d8db4d9与0e5c3ea7有37个策划src/tests差异，明确使用公司最新源码，不谎称全树等价；性能测试未变。

|源码|原单跑ms|预热三次ms|固定四负载三次ms|
|---|---:|---|---|
|公司HEAD在家2d8db4d9|1233.1417|1178.6177 / 1162.9147 / 2130.2746|1339.5737 / 1320.3386 / 2710.5243|
|实机修复b91858ee|1256.8144|1259.1638 / 1225.1404 / 2433.8101|1287.6010 / 1259.7612 / 2446.2974|
|最终4f57ca56|1292.9921|1270.3130 / 1206.9170 / 2228.8839|1384.5548 / 1396.3964 / 2735.7902|

每轮calls CSV10000行，全保留；三组相同shared fixture路径/LibraryId/布局，普通非插桩发布Core哈希绑定；总时长、p50/p95/p99/p999/max、CPU、逻辑I/O、分配、GC、metadataMs在raw。固定四WPF类/每PID轮次/TRX退出码完整，measurement与全部四worker真实重叠核对。固定负载不是原“完整四片+Core”重演。原单跑stdout+TRX+实际值，包装PID和起止Epoch有保存；psutil不可用，因此性能testhost实际PID未捕获，独立identity-only不用于等待归因。

本轮所有10000次均<30秒，未触发新的WPR采集；不刷绿、不用公司耗时代家用结果，不宣称旧失败根因已唯一解释。物理卷历史指纹、性能testhost关联证据仍为限制。

## 交付及剩余限制

本地完整诊断ZIP、SHA-256和逐文件读回见DIAGNOSTIC_IDENTITY/DIAGNOSTIC_FILES；约81MB，自包含，不含用户数据库/素材。合成ptplan、替代PNG/TXT、真实桌面导出DOCX及简短手动步骤均附带。原始TRX/日志/每次CSV归档RAW_EVIDENCE，证据和产品独立提交。

不能发行。Word/WPS实机排版、Setup、全软件回归、真实相机/客户材料/在线服务均未执行。没有合main、强推或改写旧树。远端正常push和ls-remote读回另存PUSH_READBACK。
