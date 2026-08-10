# 像素蛋挞 2.3.0 RC5 CoreHotfix2 工作日历真实交互修复报告

## 结论

本轮 CoreHotfix2 已完成代码修复、自动化回归、Release Publish、独立安装包生成，并快进合并到 `release/2.3.0`。安装版已在独立目录真实启动，完成工作日历打开、临时排期创建、分步编辑器链路和编辑入口验证。真实安装版的状态切换、关闭/重开和 Pin 完整动态序列未全部完成，因此不将安装版完整动态验证标记为通过，仍需用户人工确认。

## 版本与 Git

- 起始基线：`e0a82c148ab61b03d781869db10ac7a2d825b177`
- 修复分支：`fix/2.3.0-rc5-calendar-edit-hotfix2`
- 修复提交：`9c008a00ffdcc4d4904d13fa880ac3109e1a81a9`
- 当前分支：`release/2.3.0`
- 当前 HEAD：`9c008a00ffdcc4d4904d13fa880ac3109e1a81a9`
- 工作树：干净
- ProductVersion：`2.3.0`
- FileVersion：`2.3.0.0`
- SchemaVersion：`4`；未新增迁移、未修改 Schema
- Provider：`None`
- 未合并 `main`，未创建 `v2.3.0` Tag，未进入 2.4.0

## 修复范围

1. 统一 `CalendarDayVisualState`，工作台迷你日历和完整工作日历共享日期状态解析；日期数字、今天、选中、状态色和关闭锁定相互独立。
2. 迷你日历上下文菜单改为真实命令绑定：新建排期、查看当天详情、打开完整日历；选中日期通过导航请求传递并在页面激活后消费。
3. 完整日历日期与排期上下文菜单补齐：查看详情、编辑排期、修改状态、归档，以及关闭/重新打开当天档期。
4. 详情页“编辑排期”复用统一编辑命令；编辑保存保持原排期稳定 ID，不创建重复记录。
5. 关闭档期写入持久化状态，关闭日期显示锁定和“已关闭”视觉；重新打开恢复可排期状态。
6. 工具箱 Pin 图钉提高尺寸、对比度和命中区域，固定/取消固定即时同步工作台快捷工具区。
7. 编辑器主要字段、步骤条和内容区调整为 DPI 150%/200% 下不重叠的稳定布局。
8. 修复 Review 构建中只读属性被错误 TwoWay 绑定导致的 XamlParseException；任务摘要显式使用 OneWay。

## 验证分层

| 功能 | CodeVerified | AutomatedVerified | InstalledUiVerified | UserVerified |
|---|---:|---:|---:|---:|
| 迷你日历查看当天详情 | true | true | true（已实机点击入口） | false |
| 正确日期导航与选中日期 | true | true | true（已从日历进入编辑器） | false |
| 已有排期编辑且保持原 ID | true | true | 部分（已进入编辑器并修改临时标题） | false |
| 完整日历上下文命令 | true | true | true（自动化证据） | false |
| 五色状态视觉统一 | true | true | false（本轮未完成安装版全状态轮换） | false |
| 关闭/重新打开档期 | true | true | false（本轮未完成安装版重启序列） | false |
| 工具箱 Pin 即时同步 | true | true | false（本轮未完成安装版完整 Pin 序列） | false |
| 编辑器 150%/200% 布局 | true | true | true（隔离安装启动正常；DPI 自动证据） | false |

## 自动化专项与重复回归

- 单元/交互专项新增：`96` 个 WPF 交互测试、`9` 个 DPI 测试、Booking SQLite 编辑测试；总测试数由基线继续增加。
- 查看当天详情：`50/50`。
- 编辑排期：`50/50`。
- 状态同步：`30/30`。
- 关闭/重新打开：`30/30`。
- 上下文菜单：`50/50`。
- 工具箱 Pin：`30/30`。
- DPI 布局：`20/20`。
- Debug 全量三轮：每轮 `1787/1787`，0 失败、0 跳过、0 警告、0 错误。
- Release 全量三轮：每轮 `1787/1787`，0 失败、0 跳过、0 警告、0 错误。
- 最终测试总数：`1787`（Core 1028、WPF 658、DPI 101）。

## 发布与安装

- Release Publish：成功；`win-x64`、self-contained、WinExe。
- Publish 目录：`D:/AI AGENT/RAWSelectionAssistant/artifacts/releases/2.3.0/publish/corehotfix2-win-x64`
- 新安装包：`D:/AI AGENT/RAWSelectionAssistant/artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC5_CoreHotfix2_x64.exe`
- 安装包大小：`50,019,833` 字节。
- SHA-256：`B34F45E58D25ED876D35E53E7FB1150465153589C564ADD8837D631942437F4F`
- 独立安装目录：`D:/AI AGENT/RAWSelectionAssistant/artifacts/ui-review/2.3.0-rc5-core-hotfix2/installed-app`
- 隔离安装启动：成功；未使用用户真实 LocalAppData 数据库。
- 旧 RC5、LayoutFix、RuntimeUI 和 CoreHotfix1 安装包均保留，未覆盖；CoreHotfix1 SHA-256 仍为 `BB651C506F3188E6077980094129BC8E8A3E51F077E5D8C9DD2313F39994CD84`。

## 自动化视觉证据

`D:/AI AGENT/RAWSelectionAssistant/artifacts/ui-review/2.3.0-rc5-core-hotfix2/` 保留 23 张固定命名截图和索引，索引中的 `LayoutPassed=true`、`BlockingIssues=0`、`EvidenceLevel=AutomatedReview`。状态色截图为同一合成状态下的场景映射，不能替代安装版逐状态人工确认。

## 范围边界

- 未新增联机拍摄业务、厂商 SDK、localhost 服务、Fake/Mock Camera、数据库表或 Schema 迁移。
- 未执行 Publish 以外的联网服务部署、未生成用户数据、未修改真实照片、未操作用户真实桌面文件。
- 不建议自动进入下一阶段；待用户完成安装版完整动态序列和人工确认后再决定后续工作。

