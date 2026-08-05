# 像素蛋挞 2.3.0 阶段 E：性能、恢复、安装版验收与条件封版报告

## 结论

阶段 E 的代码、压力测试、重复回归、真实 WPF UI 证据、Release Publish、RC1 安装包、隔离安装和 2.2.0→2.3.0 升级验收均已完成。当前机器只检测到一台真实物理显示器，因此严格按条件封版规则保留在 `release/2.3.0`：生成 RC1，不合并 `main`，不创建 `v2.3.0` Tag，不进入 2.4.0。

## 1. Git、版本与数据库

- 阶段 E 开始 HEAD：`4c3f4a5dd0ebaf98620c03ce2aa30cf9b2117a41`
- 阶段 D 功能提交：`52a40a02529adf02fe8f87bca0817d7ef8fce2ad`，已确认是阶段 E 开始 HEAD 的祖先。
- 阶段 E 最终验收代码 HEAD（本报告与证据提交前）：`ba64b784fd2002a378d63b8f9f942f2cb744161e`
- 当前分支：`release/2.3.0`
- 工作树：阶段 E 开始时干净；报告与证据提交完成后再次确认干净。
- 产品版本：`2.3.0`
- 文件版本：`2.3.0.0`
- SchemaVersion：`3`
- 数据库修改：未新增表、未新增迁移、未修改 SchemaVersion；仅使用隔离数据库验证锁定、恢复和 Schema 2→3 升级。

### 阶段 E 代码提交

- `5b2e02e2ab6de95477e571e543290c2bc3d9a2d9` — `perf(tether): harden monitoring performance and recovery`
- `bd2affacdfd21d1bbd4a1e8ae97cc4f7025ef9af` — `fix(tether): refine monitoring layout and accessibility`
- `62fa0726a8c4526371e8f22a543aaf00c123f15e` — `test(tether): add 2.3.0 release acceptance gates`
- `282d741221b77ce3efe27dcc5d465e582a496f95` — `fix(release): support Windows PowerShell acceptance paths`
- `2c45a01d777821548fac987ac5e81fe50e9234dc` — `fix(tether): retain explicitly observed burst files`
- `bf25853ad2682f8ac20c533c1281fe985676c14f` — `fix(test): keep regression evidence structured`
- `bf5e47d558a34ac46b05e45dcd414fd2f0f33b28` — `test(tether): record physical display acceptance`
- `85c12b72031ef59547e88b37fa5c4100b980ab8f` — `fix(test): constrain long stress evidence output`
- `bdfe268c8b57643df1779026c6fce573c4089a2d` — `fix(test): preserve final UI evidence filename`
- `ba64b784fd2002a378d63b8f9f942f2cb744161e` — `fix(test): preserve isolated acceptance labels`

其中产品缺陷的精确根因是：Watch Folder 已明确收到 `Created` 事件并排队的文件，可能因 NTFS 创建时间精度与会话启动截止时间比较而在协调扫描中被跳过，导致高并发断言先看到 97/100。最小修复保留显式观察到的队列路径，不再用创建时间截止值排除；新增旧创建时间的确定性回归测试。目录恢复成功后会话状态同时恢复为 `Running` 并清除 `LastError`。

## 2. Watch Folder、文件系统与恢复

- 100 文件短连拍：`100/100` Ready，失败 `0`，重复 `0`，最终队列 `0`，源文件不变；处理时间 `00:00:00.4285613`。
- 1000 文件批次：`1000/1000` Ready，失败 `0`，重复 `0`，最终队列 `0`，源文件不变；处理时间 `00:00:04.8036957`。
- 长会话：实际 `01:02:34.4240653`，发现 `3200`，Ready `3200`，失败 `0`，重复 `0`，NeedsAttention `0`，目录断开/恢复 `12` 次，未释放会话 `0`，未完成任务 `0`。
- 队列峰值：`256`。
- 慢写与异常写入：专项与全量回归覆盖稳定探测、Changed 重新唤醒、临时改名、损坏/不支持文件隔离、超时和重试语义；未稳定前不生成正式资产，坏文件不终止会话，源文件不变。
- 文件系统故障：隔离目录断开/重命名与恢复通过，恢复后状态回到 `Running`、不重复发现；权限、只读、长路径、中文/空格路径、冲突、缓存不可写等既有安全回归全部保留并通过。未执行真实磁盘耗尽或真实外接盘物理拔出。
- 数据库锁定：确认 SQLite 临时写锁发生；写入失败明确暴露，锁释放后重试成功，`integrity_check=ok`，SchemaVersion `3`，新连接立即读到持久化状态。
- 崩溃恢复：恢复门禁 20 轮、每轮 4 项全部通过；应用重启、数据库重开、未完成任务恢复和已完成任务不重复恢复通过。未执行破坏性的真实 OS 进程硬杀注入，作为已知限制记录。
- 文件安全：所有压力、隔离安装和升级源文件哈希保持不变；未覆盖、移动、删除源文件。

## 3. 内存、代理图与文件句柄

- Watch 长测工作集：起始 `23,912,448 B`（22.80 MiB），峰值 `83,288,064 B`（79.43 MiB），空闲 GC 后 `75,743,232 B`（72.24 MiB）。峰值后有回落，未观察到随循环持续单调增长。
- Watch 句柄：起始 `222`，峰值 `334`，最终 `305`；会话已释放且无未完成任务。
- 代理/100% 测试：连续浏览 `1000` 个缩略图、快速切换 `100` 次实际尺寸；起始 `89,124,864 B`，峰值 `92,024,832 B`，GC 后稳定 `82,472,960 B`。
- 缓存：缩略图阶段 `3`，释放后 `0`；源文件句柄已释放，源文件不变，旧请求回写被阻止。

## 4. LUT、ICC、显示器和生命周期

- LUT 解析压力：1D `2/256/1024/65536` 与 3D `2/17/33/65` 全部完成；最大 1D 解析 `86.5307 ms`，最大 3D 解析 `238.9811 ms`；6 类损坏输入被拒绝。
- LUT 渲染：1024=`160.5353 ms`、1600=`213.2796 ms`、2048=`100.2685 ms`。这些是当前机器的真实相对测量，不作为跨机器绝对性能承诺。
- LUT 取消与竞态：最新请求胜出、旧请求取消、旧结果不回写；损坏缓存自动恢复；源 LUT 不变；失败回退 sRGB/原始安全输出。
- ICC：存在、缺失、损坏、不可访问、API 失败与不同显示器配置的自动化回退通过；未安装 ICC、未修改 Windows 色彩设置、未写注册表。
- 显示器拓扑：自动化覆盖 StableKey、顺序/边界/分辨率/DPI/方向、客户屏断开恢复和独立窗口生命周期。
- 当前物理显示器：`1` 台，2560×1440，96×96 DPI，横屏，StableKey=`display-355d268f959ea7adcee68eb8`，ICC 检测为系统默认 sRGB。
- 真实物理双屏：未执行，`PhysicalSecondMonitorTested=false`，不得宣称通过。
- 睡眠/唤醒：生命周期抽象、拓扑刷新、应用重启和恢复自动化通过；未控制真实 Windows 睡眠/唤醒，不伪造结果。

## 5. UI 与无障碍

- UI 优化：保持阶段 C/D 工作区结构，仅调整密度、分组、紧凑布局、状态文案、焦点和提示；未重做整个工作台。
- 无障碍：所有交互控件具备可读 AutomationProperties.Name，增加并验证键盘入口与快捷键（含 LUT、锁定、客户监看）；错误和状态不只依赖颜色，高对比和混合 DPI 通过自动化复验。
- UI 截图：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-stage-e`
- UI 总览：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-stage-e\像素蛋挞_2.3.0阶段E最终UI验收总览.png`
- 证据：32/32 张真实 WPF RenderTarget 截图，每张 SHA-256 唯一，布局通过，阻断问题 `0`；源资产前后 SHA-256 一致。

## 6. 测试矩阵

- 新增测试：`8`。
- 最终测试总数：`1187`（Core `948`、WPF `165`、DPI `74`）。
- 原 `1179` 项：全部保留，未删除、禁用、跳过或改分类规避。
- LUT 核心：20 轮，每轮 `48/48`。
- 客户窗口：20 轮，每轮 `28/28`。
- Watch Folder：10 轮，每轮 `67/67`。
- 恢复门禁：20 轮，每轮 `4/4`。
- Core 并行：3 轮，每轮 `948/948`。
- Core 非并行：3 轮，每轮 `948/948`。
- Debug 全量：3 轮，每轮 `1187/1187`，0 失败、0 跳过、0 警告、0 错误。
- Release 全量：3 轮，每轮 `1187/1187`，0 失败、0 跳过、0 警告、0 错误。

## 7. Publish、Release 扫描与安装包

- Release 扫描：通过。OutputType=`WinExe`、Provider=`None`、Release 无 Fake Camera、无厂商 SDK/未知相机 DLL、无测试程序集/测试资产/数据库/日志、无 localhost/127.0.0.1 启动逻辑、无后台服务、无托盘常驻、无系统 ICC 修改和照片上传。
- Publish：self-contained `win-x64`，路径 `D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\publish\win-x64`。
- Publish 文件清单：267 项，已生成 `file-manifest.csv`、`file-manifest.json` 和 `SHA256SUMS.txt`。
- 安装包类型：`RC1`，原因是只有一台物理显示器。
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\installer\像素蛋挞_Setup_2.3.0_RC1_x64.exe`
- 大小：`49,932,887 B`。
- SHA-256：`7C9AD2689BBCC5960D7B20396D8951D63F012A615447FEAE453BC6CABD588A2C`。
- 签名：未签名；当前证书存储未发现可用正式代码签名证书，未使用测试证书，不声称 SmartScreen 信誉通过。

## 8. 安装与升级验收

- 隔离安装：通过。使用 Win32 `CreateDesktopW` 和独立数据根，未操作当前桌面，未读取用户真实 LocalAppData。安装/启动/重启、Schema 3、联机 JPG/RAW 配对、标注、目录断开恢复、LUT、文档/恢复安全、源文件完整性、卸载和安装目录清理全部通过。
- 2.2.0→2.3.0 升级：通过。2.2.0 安装包 SHA-256 保持 `B4638CBCB8467B30EA21F474ED2E15603A1A6621081F5E05DB75C3177B313788`；因旧版不安全支持隔离数据根，未启动旧版，按授权规则构造受控 Schema 2 数据。2.3.0 两次启动后 Schema 3、`integrity_check=ok`、项目/排期/文档/快捷工具/设置/旧 JSON 与迁移备份保留，三张联机表可用且活动会话为 0，源文件不变，卸载后隔离用户数据保留。

## 9. 条件封版判定

- 源文件完整性：通过，压力、UI、隔离安装和升级所用合成源文件均未修改。
- 阻断缺陷：未发现仍未修复的代码级阻断缺陷。
- 未通过或未验证：真实物理双屏、真实 Windows 睡眠/唤醒、真实 OS 进程硬杀注入、正式代码签名和 SmartScreen 信誉。
- 建议物理双屏人工验收：是，使用根目录 `像素蛋挞_2.3.0物理双屏人工验收清单.md`。
- 合并 `main`：否。
- 创建 `v2.3.0` Tag：否。
- 进入 2.4.0：否。

本报告随阶段 E 文档和证据提交生成。Git 提交无法在自身文件内容中稳定自引用，因此本节记录的是完整产品、工具和 RC1 构建来源 HEAD `ba64b784fd2002a378d63b8f9f942f2cb744161e`；最终证据提交哈希与当前 HEAD 以提交后的 Git 输出和最终回传为准。
