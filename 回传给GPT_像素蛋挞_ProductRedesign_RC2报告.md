# Pixel Tart ProductRedesign RC2 验收报告

## 基线与提交

- 分支：`feature/pixel-tart-product-redesign`
- RC2 产品提交：`ca6924e5e180608629eb0a23233d6cbefeee3962`
- RC2 测试保留提交：`fb3716c7bfa67a76f465526d39a2cff6ff6f4070`
- 当前工作树：干净
- ProductVersion：2.3.0
- FileVersion：2.3.0.0
- SchemaVersion：5

## 本轮完成

- 建立 `docs/audit/PixelTart_VisibleFeatureAudit_RC2.md`，对可见入口分类为 ProductionReady、PreviewDisabled 或 Hidden，并覆盖水印预览禁用态和零死控件规则。
- Task Center、RAW 转 JPG、批量压缩统一读取 TaskEngine 终态，显示真实 TaskId；失败任务提供“查看原因”和脱敏诊断复制入口。
- 工作日历统一使用 `CalendarDayVisualStateResolver`：Free 灰、Scheduled 红、PostProduction 黄、Delivered 蓝；绿色 Shot 不再作为主日历状态徽章或图例。
- `MarkShootCompleted` 持久化 `ShotCompletedAtUtc` 并进入 PostProduction；支持撤销拍摄完成、后期阶段、交付与重新打开交付。
- Schema5 迁移及回滚/备份说明见 `docs/architecture/CalendarWorkflowStateMigration_RC2.md`。
- 关闭档期保持锁定覆盖，不改变已有排期；完整日历保持 60/40 布局。

## 测试与构建

- Debug：`1943/1943`，0 失败、0 跳过、0 错误；Core 1114、WPF 728、DPI 101。
- Release：`1943/1943`，0 失败、0 跳过、0 错误；Core 1114、WPF 728、DPI 101。
- Debug/Release 构建均 0 错误；DPI 矩阵 100%、125%、150%、175%、200% 保留。

## 候选安装包

- 路径：`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_ProductRedesign_RC2_x64.exe`
- 大小：50,716,988 bytes
- SHA-256：`E33C0A5B13312FA9CEB874A8BB52E907E6E4D8F444FE1D0B66ED101439BF8FFF`
- RC1 未覆盖，原包 SHA-256：`D8A997A463D64BB1D44D3ACFFBDD4A7213DC4A1EDD91FF404CBA4711C2804660`。
- Publish 载荷：280 文件；WinExe/self-contained win-x64；Provider=None；无测试程序集、PDB、数据库、日志、样本图或用户数据。

## UI 与隐私边界

- 脱敏证据位于 `artifacts/ui-review/product-redesign/`，已筛选副本位于 handoff 仓库 `ui-review/rc2/`。
- 截图仅使用生成或测试素材，不含客户照片、头像、RAW、联系人或生产数据。
- `InstalledUiVerified` 仍为 partial；右键日历、关闭档期重启保持和在线选片原生文件对话框链路仍需用户前台实机确认。
- `UserVerified` 未声明；本轮未操作用户真实 LocalAppData。

## 发布边界

- 未合并 `main`。
- 未创建正式 Tag。
- 未进入 2.4.0。
- RC2 完成后停止，等待 GPT 审查与用户实机验收。
