# Pixel Tart P0：Global Surface Close / Shell Escape Hatch 审计

## 范围与结论

本轮只修复独立模块无法退出的 P0 交互问题。产品版本仍为 `2.3.0`，`SchemaVersion` 仍为 `5`，构建类型为 `GlobalSurfaceClose_DevValidation`，不是 RC3。

只读恢复审计确认，上一轮交互修复仍把“关闭模块”和“取消任务”混为同一条路径：`MainWindow.RequestEscapeCloseAsync` 对 RAW 转 JPG、批量压缩、拼图和整理图片先执行模块 `CancelCommand`，再尝试通过受 `IsBusy` 限制的导航命令返回工作台。模块失败、忙碌或命令不可执行时，Shell 没有独立逃生口；同时没有来源页面栈，因此即使成功关闭也只能固定返回工作台。

## 精确根因

1. 没有 Shell 级 Surface 导航合同；页面只通过 `MainViewModel.CurrentPage` 和各自命令切换。
2. `NavigateCommand` 的执行条件包含 `!IsBusy`，关闭界面会被业务忙碌状态间接禁用。
3. Esc 对工具页面调用模块 `CancelCommand`，导致关闭 UI 会取消后台任务，违反任务中心后台继续语义。
4. 页面、Modal、Drawer、Overlay 分别实现关闭按钮或取消按钮，没有始终可用的共享 X。
5. 没有 `OriginSurface` / `PreviousSurface` / `NavigationHistory`，无法从工具返回真实来源页面。
6. 教程退出依赖教程命令状态；Step18 失败时仍可能失去可执行出口。
7. “取消”“返回上一步”“关闭模块”文案与行为未严格分离。

## Surface 入口与 P0 覆盖矩阵

| Surface | 当前入口/宿主 | X 后预期 | 特殊约束 |
|---|---|---|---|
| RAW 转 JPG | 工作台快捷工具、工具箱；全页 Modal 宿主 | 返回真实来源 | Busy 时任务继续，底部只负责“取消任务” |
| 批量压缩 | 工作台快捷工具、工具箱；全页 Modal 宿主 | 返回真实来源 | Busy 时任务继续，底部只负责“取消任务” |
| 拼图 | 工具箱；全页 | 返回真实来源 | X 不执行 `CollagePage.CancelCommand` |
| 整理图片 | 工具箱；全页 | 返回真实来源 | X 不执行 `OrganizePhotosPage.CancelCommand` |
| 本地分片 | 工作台入口；全页 | 返回来源或工作台 | 不删除或回滚已完成文件 |
| 四步归片向导 | 侧栏/本地分片入口；全页 | 返回来源或工作台 | 第 4 步也必须可退出；复制结果保留 |
| 教程 Overlay | 帮助/首次启动 | 正常工作台 | Step18 失败也可退；恢复 HitTest、Sidebar 和焦点 |
| 快速创建拍摄 | 日历/工作台；Modal | 返回原日历上下文 | 未保存更改可确认；确认框本身可关闭 |
| 快速编辑拍摄 | 日历；Drawer | 返回原日历上下文 | 未保存更改可确认 |
| 完整拍摄策划 | 日历；Modal | 返回原日历上下文 | 未保存更改可确认 |
| 在线选片创建 | 在线选片；Modal | 返回在线选片工作区 | 不依赖 `IsBusy` / `CancelCreateCommand.CanExecute` |
| 在线选片项目工作区 | 侧栏/项目入口；全页 | 返回真实来源 | 关闭不删除项目或云端资产 |
| 摄影收支编辑 | 摄影收支；Drawer | 返回收支列表 | 未保存更改按编辑合同处理 |
| 设置独立页面/Modal | 菜单/侧栏 | 返回真实来源 | Esc 和 X 同一 Shell 路径 |
| 错误详情 | 任务卡/错误对话框 | 返回原宿主 | 错误状态不得禁用 X |
| Task 详情 | 当前实现为任务中心卡片内联选择，并非独立可视 Surface | 保持任务中心 | 若以后独立化，必须自动纳入共享 Chrome |

## Shell 合同要求

- 统一合同必须提供 `CloseCurrentSurface`、`CloseCurrentSurfaceAsync`、`ReturnToOrigin`、`ReturnToWorkbench`。
- 至少保存 `PreviousSurface`、`CurrentSurface`、`OriginSurface` 和简单 `NavigationHistory`。
- 来源不存在、被删除或上下文失效时，安全回退 `Workbench`。
- X 的 Shell 路径不得读取模块 `DataContext`，不得依赖 `CanExecute`、`IsBusy`、任务状态、教程步骤或验证结果。
- X 只移除当前 Surface，不得调用 `Application.Current.Shutdown()`、`MainWindow.Close()`、模块取消令牌、文件删除或撤销逻辑。
- Esc 与 X 使用相同的 Shell 关闭路径；ComboBox/Popup 第一次 Esc 仍优先关闭自身。
- 全页工具的 `Alt+Left` 只作为辅助入口，不能替代 X。

## Busy 与 Task Center 合同

运行任务时点击 X 必须只关闭当前模块。TaskEngine 任务、CancellationToken 和输出处理继续运行；Task Center 仍能立即读取 `Running`/进度状态，完成后更新终态并可发送轻量通知。只有模块内部明确标注为“取消任务”的按钮才可以触发任务取消。

系统级自动化必须证明：`Open -> StartTask -> X -> SurfaceClosed -> TaskStillRunning -> TaskCenterVisible -> TaskCompleted`。该测试不得使用固定延时或无限重试，而应等待产品/测试任务暴露的确定性完成信号。

## 失败态与教程合同

任何模块进入校验失败、任务失败、报告缺失或 NeedsAttention 后，共享 X 仍必须保持启用。教程“退出教程”按钮和教程 X 必须汇合到同一个 `ExitTutorialAsync`，并且 Step18 缺少 CSV/JSON/TXT 时也能释放 Overlay、InteractionLock、HitTest 与 Sidebar，而不关闭主程序。

## 自动化与安装版边界

`GlobalSurfaceCloseSmokeTests` 覆盖 Shell 合同、来源返回、来源失效回退、失败态、Busy 后台任务、Esc/Alt+Left 路由、共享 X、禁止关闭主程序及要求模块清单。源码/单元/WPF 自动化只能形成 `CodeVerified` 与 `AutomatedVerified`；以下字段只有真实 DevValidation 安装版前台点击后才可设为 `true`：

- `global_surface_close_verified`
- `tutorial_x_close_verified`
- `raw_x_close_verified`
- `compress_x_close_verified`
- `escape_surface_close_verified`
- `InstalledUiVerified`

`UserVerified` 必须继续保持 `false`，直到用户本人明确确认。

## 安全边界

关闭 Surface 不修改产品业务数据，不删除源照片、RAW、成功输出、临时之外的文件或任务历史，不清理真实 LocalAppData，不修改数据库表或迁移。安装版验证必须使用独立 `PixelTart_Validation` 数据目录和脱敏截图。
