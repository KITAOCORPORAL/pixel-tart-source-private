# 像素蛋挞最终产物一致性审计报告

审计时间：2026-07-31（Asia/Shanghai）  
审计方式：只读检查源码；重新执行 clean/restore/build/test；从当前源码重新生成 UI 验收图；隔离安装、启动和卸载当前安装包。未修改产品源码、版本或安装脚本。

## 一、最终结论

- 当前唯一有效版本：**2.0.1**。
- 当前唯一有效安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.1_x64.exe`。
- 当前有效报告：本报告。`回传给GPT_像素蛋挞_2.0.1录屏参考视觉重构报告.md`的数据与当前安装包一致，但已由本报告取代为最终审计依据。
- 已过期报告：`回传给GPT_像素蛋挞_2.0.1工作台视觉纠偏报告.md`。
- 当前源码 clean 构建出的 `KitaoPhotoSelector.dll` 与当前发布目录中的 `KitaoPhotoSelector.dll` SHA-256 完全相同，均为 `6EA1A438AE1C30FA9E6AB894AD5F89C94A1EF3F796740A4799DD85AE1FD19B33`。
- 因此，当前安装包对应当前最终源码；本轮重新生成的截图也来自同一当前源码。
- 本次不提升为 2.0.2。原因是审计期间没有产品代码变更，当前源码、发布主程序集和现有安装包一致；截图较晚仅因本次重新生成审计证据，不代表代码比安装包更新。下一次发生任何产品代码或打包内容变更时，应提升到 2.0.2，避免再次使用同一版本号覆盖文件。

## 二、当前真实版本与时间

1. 源码产品版本：`2.0.1`，来自 `Branding.ProductVersion`。
2. 项目版本：`2.0.1`，来自 WPF 项目文件。
3. 当前程序集产品版本：`2.0.1`。
4. 当前程序集文件版本：`2.0.1.0`。
5. Git 提交：**无法提供**。当前工程目录及其父目录没有 `.git` 元数据，不是 Git 工作区。
6. 产品源码最后修改文件：`D:\AI AGENT\RAWSelectionAssistant\src\RAWSelectionAssistant\MainWindow.xaml`。
7. 产品源码最后修改时间：`2026-07-31 10:56:16.425 +08:00`。
8. 本轮 Release clean 构建主程序集时间：`2026-07-31 11:56:51.799 +08:00`。
9. 当前发布目录生成时间：`2026-07-31 10:57:22.580 +08:00`；发布 EXE 时间为 `2026-07-31 10:57:22.402 +08:00`。
10. 当前安装包最后生成时间：`2026-07-31 10:57:58.987 +08:00`。

说明：本轮 clean 构建晚于发布目录和安装包，但 clean 构建主程序集与发布目录主程序集哈希完全相同，证明两者代码内容一致。

## 三、当前安装包

- 文件存在：是。
- 完整路径：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.1_x64.exe`
- 文件大小：**48,765,008 字节**。
- SHA-256：`5255D10DCAAE6E5EA4E7916E62AD07B0C38487BA86EBF73BC8DC5C0FCE00241C`
- 安装包版本：2.0.1。
- 安装包内容主程序集与当前源码：一致。
- 本轮没有覆盖或重新生成该安装包。

## 四、两份旧报告冲突说明

### 1. 两个安装包是否为不同构建

是。旧记录中的安装包为 48,740,169 字节，SHA-256 为 `DDCABC1883766C8533792F1C2997B3A331270FC5A4D5BEA283AEEFB74C70E21F`；当前安装包为 48,765,008 字节，SHA-256 为 `5255D10DCAAE6E5EA4E7916E62AD07B0C38487BA86EBF73BC8DC5C0FCE00241C`。文件大小和哈希均不同，必然是不同二进制构建。

### 2. 后一个安装包是否覆盖前一个

是。安装脚本始终输出同一路径和文件名 `像素蛋挞_Setup_2.0.1_x64.exe`。当前文件创建时间保留为 `2026-07-30 18:14:43.042 +08:00`，但最后写入时间为 `2026-07-31 10:57:58.987 +08:00`，与同名文件被后一次构建覆盖的行为一致。目录中不存在旧哈希对应的安装包文件。

### 3. 哪个安装包与当前源码一致

当前 48,765,008 字节、SHA-256 为 `5255D10D...00241C` 的安装包与当前源码一致。重新 clean 构建的主程序集和安装包所用发布目录主程序集哈希完全相同。

### 4. 哪份报告已过期

`回传给GPT_像素蛋挞_2.0.1工作台视觉纠偏报告.md` 已过期。它记录的是 350/350 测试、旧安装包大小/哈希和 7 张截图，对应较早的 2.0.1 构建。

`回传给GPT_像素蛋挞_2.0.1录屏参考视觉重构报告.md` 对应当前二进制的 356/356、48,765,008 字节、当前安装包哈希和 12 张截图，数据有效；但从本次审计完成起，以本报告作为唯一最终审计报告。

### 5. 为什么版本号没有随第二次构建变化

第二次构建被作为 2.0.1 同版本内的 UI 视觉纠偏续作，源码、项目和安装脚本的版本字段没有提升，安装器也继续使用同一输出文件名。因此后一次 2.0.1 覆盖了前一次 2.0.1。这是产物管理问题，不是安装包损坏。

### 6. 是否需要提升到 2.0.2

本次审计不需要提升：没有修改产品源码，且当前源码、发布目录和现有安装包已验证一致。建议规则是：从现在起，只要再次修改产品代码、资源、配置或安装内容，必须提升为 2.0.2 并生成 `像素蛋挞_Setup_2.0.2_x64.exe`，不得再次覆盖当前 2.0.1。

## 五、最终测试

执行顺序和结果：

| 步骤 | 结果 | 实际耗时 |
|---|---:|---:|
| `dotnet clean RAWSelectionAssistant.sln -c Release` | 通过，0 警告，0 错误 | 1.114 秒 |
| `dotnet restore RAWSelectionAssistant.sln` | 通过 | 4.469 秒 |
| `dotnet build RAWSelectionAssistant.sln -c Release --no-restore` | 通过，0 警告，0 错误 | 3.833 秒 |
| `dotnet test RAWSelectionAssistant.sln -c Release --no-restore` | 356/356 通过 | 2.777 秒；测试执行 578 ms |
| UI 专项 `WorkbenchVisualCorrection201Tests` | 40/40 通过 | 1.298 秒；测试执行 98 ms |

最终统计：

- 自动化测试总数：**356**。
- 通过：356。
- 失败：0。
- 跳过：0。
- 当前 UI 专项测试总数：**40**。
- UI 专项通过：40。
- Release 构建警告：0。
- Release 构建错误：0。

完整日志位于：`D:\AI AGENT\RAWSelectionAssistant\artifacts\final-review\2.0.1`。

## 六、当前 UI 验收截图

- 截图目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\final-review\2.0.1`
- 实际截图数量：**12**。
- 截图生成方式：使用当前源码重新构建 UI Review WinExe，逐状态实际渲染；未复制旧截图，未使用 Photoshop、网页重绘、布局辅助线或调试边框。
- 总览图：`D:\AI AGENT\RAWSelectionAssistant\artifacts\final-review\2.0.1\像素蛋挞_2.0.1_UI最终验收总览.png`
- 总览图大小：428,828 字节。
- 总览图 SHA-256：`99B273D93FD043A0D9FCDACA796ACF45973E296F45004A0C364AF730708835A7`
- 机器可读清单：`UI截图_SHA256.csv`、`UI截图_SHA256.json`。

| 文件 | 生成时间（+08:00） | SHA-256 |
|---|---|---|
| `01_Workbench_Dark_1600x920.png` | 2026-07-31 11:54:45.627 | `3CEAF8E2CF804B4F4BFC3DB0F40C9AC806F89F6D3299F7A5764C2A2C192FFD0A` |
| `02_Workbench_Dark_1920x1080.png` | 2026-07-31 11:54:47.535 | `D2B925EB41718D050BD327DFE8893CAD9F97E4E58EE9442229ADB00D91A502EC` |
| `03_Toolbox_Popup.png` | 2026-07-31 11:54:49.772 | `22DA1ED689FA274E2B8AAE3C5D9E4600B06B15B78714EBE49F0282EFF369416E` |
| `04_Toolbox_FullPage.png` | 2026-07-31 11:54:51.632 | `A427A92382E8F46A08ED1E89A760EDD67083766D7261BC2297F7C260B149669B` |
| `05_RecentProjects.png` | 2026-07-31 11:54:53.485 | `3CEAF8E2CF804B4F4BFC3DB0F40C9AC806F89F6D3299F7A5764C2A2C192FFD0A` |
| `06_TaskCenter_WithTasks.png` | 2026-07-31 11:54:55.310 | `1A0A6878DAF623950ECD5D96161103EA05CBAB940356C98D9C76A41D509C43DB` |
| `07_TaskCenter_Empty.png` | 2026-07-31 11:54:57.196 | `3CEAF8E2CF804B4F4BFC3DB0F40C9AC806F89F6D3299F7A5764C2A2C192FFD0A` |
| `08_Settings_Dark.png` | 2026-07-31 11:54:59.128 | `E3CC91BECDE40DF368265B21CDD03F4495E046CD8900EE18F555AED03DA89632` |
| `09_Workbench_Light.png` | 2026-07-31 11:55:00.875 | `F8AFC782233470D3616285844E1697D90A07AFE80CD29B7AC8F305D3B0483E7A` |
| `10_Compact_1280.png` | 2026-07-31 11:55:02.671 | `4190D461FA73CA061F410420C9CA212CB44A33382B46569611937E4084EF7E31` |
| `11_Sidebar_Collapsed.png` | 2026-07-31 11:55:04.543 | `B75176A512E7943082B3940D5F776166D6845B7C3D4350B4A5A7F9FC62799C6D` |
| `12_Feedback_Dialog.png` | 2026-07-31 11:55:06.283 | `313753839AD7908FADD1EB704380AD76536158E6A0564F515E68F43DCC4C6ACB` |

说明：01、05、07 的哈希相同，因为这三个验收状态在当前演示数据下实际渲染内容相同；它们由三个独立启动过程分别生成，文件生成时间不同，不是复制操作。

## 七、隔离安装与卸载验证

为避免覆盖本机已有 2.0.0 安装记录，审计过程先导出并临时移开同 AppId 的旧卸载注册项，然后将 2.0.1 静默安装到：

`D:\AI AGENT\RAWSelectionAssistant\artifacts\final-review\2.0.1\isolated-install`

结果：

1. 安装包文件存在：通过。
2. 隔离安装：通过；Inno Setup 日志显示 `Installation process succeeded`。
3. 安装版本记录：2.0.1。
4. 安装后启动：通过，进程未提前退出。
5. 窗口标题：`像素蛋挞`。
6. 安装后程序产品版本：2.0.1。
7. 默认深色：通过。审计用户配置在启动前故意不提供外观字段；程序启动后自动写入 `Appearance.Theme=2 (Dark)`。
8. 无控制台：通过。PE Subsystem 为 2（Windows GUI），进程没有 `ConsoleWindowClass` 窗口。
9. 卸载：通过；日志显示 `Uninstallation process succeeded` 和 `Removed all? Yes`。
10. 隔离安装目录：已删除，无残留。
11. 原本机 2.0.0 卸载注册项：已恢复。
12. 原用户数据：已恢复，临时审计数据已删除。

## 八、无法确认或部分确认事项

- 当前工程没有 Git 元数据，因此无法给出 Git 提交哈希、分支或基于 Git 的变更清单；只能记录源码实际最后修改时间。
- Windows 图形捕获接口在隔离安装后的该 WPF 窗口上返回 `0x80004002`，因此没有继续对隔离安装实例执行自动点击，避免使用不可靠输入冒充通过。
- 工具箱 Popup 和侧栏折叠已通过当前源码重新生成的真实 UI Review 截图及 40 项 UI 专项测试确认，但隔离安装实例本身的点击交互未由自动化工具再次完成。
- 外部窗口截图工具选错了目标窗口；该错误图片已立即从交付目录删除，不作为审计证据。最终 12 张截图及总览不包含该图片。

## 九、唯一有效产物定义

从本报告生成起：

- 唯一有效版本：2.0.1。
- 唯一有效安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.1_x64.exe`
- 安装包唯一校验值：`5255D10DCAAE6E5EA4E7916E62AD07B0C38487BA86EBF73BC8DC5C0FCE00241C`
- 唯一最终审计报告：`D:\AI AGENT\RAWSelectionAssistant\回传给GPT_像素蛋挞_最终产物一致性审计报告.md`
- 最终 UI 证据目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\final-review\2.0.1`

旧报告保留为历史记录，但不得再作为当前安装包校验依据。
