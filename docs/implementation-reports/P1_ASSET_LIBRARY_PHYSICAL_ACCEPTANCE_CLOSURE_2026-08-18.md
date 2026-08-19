# 素材库 P1 Gate A 真实验收记录（BLOCKED）

日期：2026-08-19  
分支：`feature/modular-harness-v1-p1`  
Gate A 起点：`20c1df775673cec790b1daa9db25072c2e34926c`  
P0：`140e34348000174986c6e503dcedff8f90a78c34`  
P1 实现：`b4bd38f53d6a44756289eeda8bfc4feb343443c7`

## 结论

P1 Gate A 状态为 **BLOCKED**，不得写成 PASS/CLOSED，也不得进入 P2。

已完成并推送两个可独立回滚的 Gate A 检查点：

- `94f2207a821e8025ac73de0baca6f8586b1128c2`：保存选择恢复、物理输入诊断与窗口证据工具。
- `7fc0c8ad66c439ac78a1a23674142b355d72bdac`：加入只在专属 DevPreview 构建和精确环境变量下启用的确定性状态链、严格证据契约与只读校验器。

`capture_status` 继续保持 `not_captured`。本轮没有伪造状态、命令直达、图片后处理、坐标盲点或循环重试。

## 确定性状态链与校验器

状态实现已通过自动化验证：

- 首次空库使用独立全新隔离根，真实 SQLite v6 初始化并从真实 `AssetItems` 读取 0。
- loading → recoverable error → Retry → empty 在同一 ViewModel 内完成；首轮只注入带固定 ID 的已知 `IOException`，第二轮必须进入并完成真实 `SqliteAssetLibraryRepository` 查询。
- fresh DB/WAL/SHM、真实 repository implementation、schema version、asset count、异常类型、injection ID、attempt 和时间线均写入原始事件/快照。
- 初始化与 Dispose 共享 lifetime cancellation 和 gate；取消不会产生伪 ready，也不会在 repository 使用中提前 dispose。
- first-empty 与 retry 两个会话分别使用精确 8 条和 18 条时间线。
- PowerShell 5.1 严格校验器的完整成功夹具、控制器事件篡改和 PNG CRC 篡改等动态测试为 6/6。

这些结果只证明代码与证据机制可用，不替代真实前台截图和物理 Retry 点击。

## 本轮真实前台尝试与阻断

本轮最终证据根：

`.validation/P1-GateA-Final-20260819-114313-b22c8654/`

构建绑定到 `7fc0c8ad66c439ac78a1a23674142b355d72bdac`，专属 DevPreview 自包含发布成功，EXE SHA-256 为：

`827767075FD022DD5D89990F3C5A595A2E91173BC93B0FD4D7C922F0B4BA0FB9`

使用了两个全新 first-empty 隔离根。两次均精确绑定唯一进程、唯一标题窗口和一级导航 `AssetLibraryNavigationButton`，但真实前台输入被外部桌面状态反复打断：

- 第一次元素点击返回未知结果；按有界规则重新观察后，唯一一次重试被 `user input was detected in this window; call get_window_state before continuing` 拦截。
- 第二个新根/新进程仍无法完成元素点击；窗口随后被外部状态最小化，唯一的重新绑定、激活和状态刷新恢复也被同一输入守卫拦截。
- 截图接口还复现 `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`。
- 因未真实进入素材库，未创建任何状态截图、Retry 点击证据、键盘 splitter 证据或 DPI 交互证据。

严格校验器对该根按预期失败，共报告 48 个缺失/未捕获条件；因此证据门没有假绿。

## 显示状态与 DPI

本轮开始前实测基线为 `3840x2160 @ 60 Hz / 150%`。Computer Use 在首个状态场景即达到恢复上限，因此没有执行任何 Windows 显示切换。

清理后重新读取的显示状态仍为 `3840x2160 @ 60 Hz / 150%`。四个目标组合均为 **未执行/BLOCKED**：

| 目标组合 | 结果 |
|---|---|
| `1366x768 @ 100%` | 未执行；前台输入在进入素材库前被阻断 |
| `1920x1080 @ 125%` | 未执行；前台输入在进入素材库前被阻断 |
| `1920x1080 @ 150%` | 未执行；前台输入在进入素材库前被阻断 |
| `2560x1440 @ 175%` | 未执行；前台输入在进入素材库前被阻断 |

因为显示设置从未改变，最终基线不是“推测恢复”，而是保持原值并再次读回确认。

## 最终自动回归

- Debug solution build（warnings-as-errors）：PASS，0 warnings / 0 errors。
- Release solution build（warnings-as-errors）：PASS，0 warnings / 0 errors。
- Core：1192/1192。
- WPF：837/837。
- Modular Harness：14/14。
- DPI：75/101；26 个失败全部仍为既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失。
- 完整 solution：2144 total / 2118 passed / 26 failed / 0 skipped。
- 状态链 + Gate A 校验器 + Embedded 聚焦回归：32/32。
- 专属 DevPreview + state seam + input diagnostics 构建：PASS，0 warnings / 0 errors。
- Modular Harness acceptance runner：focused suites 为 Harness 14/14、Asset 28/28、Visual 26/26、WPF embedded 49/50、100K 2/2；唯一失败是本轮 runner 根缺少 `foreground-result.json`，总状态 `complete=false`。该结果与本轮前台 BLOCKED 一致。

## 安全边界

- 正式产品 Schema 5 未改。
- 素材库 schema v6、表、索引和用户素材行未改。
- 没有读取、移动、重命名、覆盖或删除用户源文件。
- 没有写入 Eagle `.library`，没有进入 P2～P6。
- `.validation` 中只保留本地运行证据、发布产物和失败诊断，保持 Git ignore；没有提交 DB、日志、PNG、EXE/DLL 或机器绝对路径。
- 所有代码、测试和工具检查点均已推送到私有远程分支，未强推、未合并其他功能分支。

## 剩余闭环项

只有在新的可控桌面会话中，才可继续以下 Gate A 项目：

1. 全新 first-empty 会话的真实空库截图。
2. 全新 retry 会话的 loading、recoverable error、物理 Retry 和恢复空库截图/时间线。
3. 左右 splitter 四个方向键的同一按键 Layer 1～4、边界、折叠/展开及新进程恢复证据。
4. 四组真实 Windows 分辨率/DPI 的 default/interaction 截图与窗口清单。
5. 完成后恢复并复核 `3840x2160 @ 60 Hz / 150%`，运行严格校验器，将 `capture_status` 改为 `captured`，再追加 P1 closure 提交。

上述任一项缺失时，P1 仍为 BLOCKED，禁止进入 P2。
