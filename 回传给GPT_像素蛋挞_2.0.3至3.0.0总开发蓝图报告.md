# 像素蛋挞 2.0.3 至 3.0.0 总开发蓝图报告

## 一、本轮完成范围

本轮严格按“当前本轮只执行的内容”完成：

- 只读审计当前工程；
- 核验真实稳定基线；
- 评估架构对 2.0.3→3.0.0 路线的支持程度；
- 列出技术债和版本门禁；
- 创建总开发蓝图和五份专项规范；
- 创建 2.0.3 详细实施/验收任务拆分；
- 未修改产品业务代码；
- 未修改版本号；
- 未构建或覆盖安装包；
- 未提前实施 2.0.4 或后续版本。

## 二、当前真实稳定基线

总控原文开头写的是 2.0.2，但其优先级规则要求以实际源码和最新有效报告为准。

审计结果：

- `Directory.Build.props`：2.0.3；
- WPF csproj：2.0.3；
- `Branding.ProductVersion`：2.0.3；
- Inno Setup：2.0.3；
- 最新有效回传报告：2.0.3；
- 当前安装包：`像素蛋挞_Setup_2.0.3_x64.exe`；
- 文件大小：48,754,028 字节；
- SHA-256：`D6768A5DDC8A3CBCD5AA9C9FB21675006252A7FE480DA9BEF1A9AC60FD8F1AA9`；
- 最新有效测试记录：405/405 通过。

因此当前真实稳定基线是 **2.0.3**。

## 三、当前架构是否支持路线图

结论：**有条件支持**。

### 可复用基础

- WPF 应用层与 Core 核心层已基本分离；
- 文件名解析、媒体索引、匹配、冲突、客户 JPG 保护和报告服务可复用；
- `MediaCopyService` / `RawCopyService` 已具备 CreateNew、防覆盖、自动编号、路径约束、空间/权限检查、取消清理；
- 多数长操作接受 CancellationToken 和 IProgress；
- 设置、索引和项目历史使用临时文件后替换；
- Provider=None、Release 禁 Mock、WinExe/self-contained 边界清晰；
- 现有测试可作为回归基础。

### 无法直接支撑后续大版本的缺口

- 没有 Git、分支、Tag 和源码回滚点；
- 共享发布目录会覆盖产物；
- MainViewModel 约 2208 行，MainWindow 内联大量页面；
- 没有 SQLite、SchemaVersion、正式 Migration/Backup/Recovery；
- 没有统一任务队列、暂停、重试、检查点和崩溃恢复；
- 没有文件操作 manifest、完成校验和 Undo Journal；
- 没有正式缩略图缓存/LRU；
- 日志没有轮换、保留天数和统一路径脱敏；
- UI 测试大量依赖源码/XAML 字符串，真实 UI、DPI 和故障场景不足；
- 云端、AI、代码签名和生产授权缺正式配置。

## 四、最高风险技术债

### 最高风险 1：无 Git 和不可变发布证据

当前目录不是 Git 仓库。无法可靠区分用户修改、构建生成物和版本源码，也无法创建分支、Tag、回滚或证明安装包对应的提交。共享 `artifacts/publish/win-x64` 和 installer 目录进一步增加产物混淆风险。

处置：下一开发阶段第一步初始化 Git、建立 main、冻结当前 2.0.3、创建 `v2.0.3` Tag、CHANGELOG 和 release manifest；发布目录改为按版本隔离。

### 最高风险 2：文件操作没有持久化清单和恢复

现有复制实现单次执行安全，但没有持久化操作计划、逐文件检查点、完成哈希、撤销日志和崩溃恢复。若直接在 2.0.4 增加真实整理、移动和拼图导出，风险会快速扩大。

处置：2.0.4 的真实整理先使用版本化操作清单，默认复制；2.1.0 建立统一任务引擎、SQLite 状态、Undo Journal 和恢复流程。

### 其他高风险

- MainViewModel/MainWindow 单体继续膨胀；
- JSON 单文件缺 SchemaVersion 和备份策略；
- 缩略图和大图处理尚无缓存、并发和文件锁规范；
- 静态 UI 测试不能证明像素级主题和 DPI 结果。

## 五、2.0.3 合并规范差异

当前 2.0.3 是有效稳定产物，但对照新合并的更严格规范仍有差异：

- 顶部“工具”菜单缺少“拼图”；
- 页面仍出现“拼图模式”，未完全统一为“拼图”；
- 整理图片页面标题和新规范存在细微命名差异；
- 全局 ToggleButton、Popup、TabControl、ScrollBar、DatePicker 等需逐项证据化复核；
- 完整工具箱的显式固定按钮重点覆盖少数工具，尚未统一到全部候选；
- 工具名称和入口仍分散硬编码；
- 没有 Git Tag 和版本化 release manifest。

由于现有 2.0.3 安装包不得覆盖，建议用户选择：

1. 发布独立 2.0.3.1 维护版关闭差异；或
2. 接受当前 2.0.3 为冻结基线，将非阻断差异并入 2.0.4。

在用户明确选择前，不建议开始 2.0.4。

## 六、已生成文档

1. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_2.0.3至3.0.0总开发蓝图.md`
2. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_版本依赖关系图.md`
3. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_数据与任务架构.md`
4. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_文件安全规范.md`
5. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_UI与工具命名规范.md`
6. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_测试与发布规范.md`
7. `D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_2.0.3详细实施任务拆分.md`

## 七、2.0.3 任务拆分摘要

任务拆分分为九个工作包：

- A：基线冻结、Git、Tag、CHANGELOG、release manifest；
- B：首页问号提示的真实 UI/无障碍复核；
- C：全局深色控件覆盖矩阵；
- D：所有工具统一固定/取消固定；
- E：工具命名、图标和四类入口统一；
- F：整理图片框架验收；
- G：拼图框架验收；
- H：自动化、真实 UI、DPI 和安装测试；
- I：预计修改文件和完成定义。

详细路径：

`D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\像素蛋挞_2.0.3详细实施任务拆分.md`

## 八、架构演进建议

### 2.0.4

- 先拆分工具 View/ViewModel 和工具注册表；
- 快捷排序、整理图片和拼图使用版本化项目 JSON；
- 所有真实文件写入先生成操作清单；
- 默认复制、自动编号、防覆盖。

### 2.1.0

- 引入 SQLite、SchemaVersion、Migration 和 Backup；
- 建立 Task Engine、OperationItems、Checkpoints、Undo Journal；
- 任务中心只订阅任务状态。

### 2.2.0 以后

- 模板、健康检查、缩略图索引进入数据库；
- 2.3.0 通过 XMP/文件清单桥接外部软件；
- 2.4.0 先做独立隐私评审；
- 2.5.0 AI 默认关闭且本地执行；
- 2.6.0 没有证书/生产授权时只建立接口，不伪造状态。

## 九、是否建议进入下一阶段

当前建议：**暂不直接进入 2.0.4**。

先由用户确认：

1. 是否接受当前 2.0.3 为冻结基线；
2. 2.0.3 严格规范差异采用 2.0.3.1 维护版还是并入 2.0.4；
3. 是否授权下一阶段初始化 Git 并建立版本化发布目录。

获得明确确认后，才能开始相应版本代码开发。

## 十、本轮变更声明

- 产品业务代码：未修改；
- 产品版本号：未修改；
- 安装脚本：未修改；
- 安装包：未生成、未覆盖；
- 现有发布目录：未清理、未重建；
- 本轮新增内容仅为 `docs/roadmap` 文档和本回传报告。
