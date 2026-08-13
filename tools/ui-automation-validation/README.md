# Pixel Tart UI Automation Validation

这是独立的 Windows UI Automation 验证器，不属于产品解决方案，也不引用产品项目或生产依赖。

## 安全边界

- `launch` 只允许启动文件名以 `.Acceptance.exe` 结尾的验收程序。
- 必须显式提供隔离的 `--acceptance-root`；工具同时重定向 `PIXEL_TART_ACCEPTANCE_ROOT`、`LOCALAPPDATA`、`APPDATA`、`TEMP` 和 `TMP`。
- 拒绝把真实用户目录、桌面、文档或真实 LocalAppData 作为隔离根。
- 拒绝浏览器进程和浏览器窗口类，绝不把鼠标或键盘输入发给浏览器。
- JSON 不输出完整可执行文件路径或隔离目录路径；路径、邮箱和长号码会脱敏。
- 验证必须使用无客户资料的隔离验收数据。`AutomationName` 可能来自 UI 内容，因此只允许用于隔离基线诊断。

## 构建

```powershell
D:\AI AGENT\.dotnet\dotnet.exe build tools\ui-automation-validation\PixelTart.UiAutomationValidation.csproj -c Release
```

该项目故意不加入 `RAWSelectionAssistant.sln`。

## 命令

先启动验收程序：

```powershell
PixelTart.UiAutomationValidation.exe launch `
  --exe <KitaoPhotoSelector.Acceptance.exe> `
  --acceptance-root <PixelTart_Validation\InputRouting> `
  --wait-automation-id <已知控件ID> `
  --output <launch.json>
```

`launch.json` 返回进程 ID。后续命令始终只操作该目标进程的顶层窗口：

```powershell
# 正式复验：AutomationId 精确定位
PixelTart.UiAutomationValidation.exe inspect-id --pid <PID> --automation-id <ID>
PixelTart.UiAutomationValidation.exe invoke-id --pid <PID> --automation-id <ID>

# 真实鼠标点击：按 AutomationId 定位，工具自行使用控件中心点，不接受坐标参数
PixelTart.UiAutomationValidation.exe click-id --pid <PID> --automation-id <ID> --output <click.json>

# 基线诊断：AutomationName 精确定位，JSON 中 selector_type=automation_name、diagnostic_only=true
PixelTart.UiAutomationValidation.exe inspect-name --pid <PID> --name <完整可访问名称>
PixelTart.UiAutomationValidation.exe invoke-name --pid <PID> --name <完整可访问名称>

# 枚举当前可见 Button
PixelTart.UiAutomationValidation.exe list-buttons --pid <PID> --output <buttons.json>

# 只向目标应用窗口发送 Escape；前台窗口无法确认时不会发送
PixelTart.UiAutomationValidation.exe press-escape --pid <PID> --output <escape.json>
```

`click-id` 在发送前缓存目标窗口和控件属性，并记录中心点处实际最上层的 UIA 元素摘要（不读取 `Name`）。只有该元素属于目标 `.Acceptance` 进程时，才通过 Win32 `SendInput` 发送鼠标移动、左键按下和抬起。发送后只执行最长两秒的目标 ID 消失探测，以及 350 毫秒 `WM_NULL` 窗口响应检查，避免界面关闭后继续访问已失效的 UIA provider。

`inspect-id` 和按钮清单包含：

- `automation_id`
- `name`（脱敏且截断）
- `bounding_rectangle`
- `is_enabled`
- `is_offscreen`
- `control_type`
- `invoke_available`

正式复验必须使用 `AutomationId`。`AutomationName` 命令和 `list-buttons` 只用于找出基线缺失的 ID，不构成正式通过证据。
