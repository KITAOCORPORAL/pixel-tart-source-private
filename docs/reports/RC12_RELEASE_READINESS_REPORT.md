# Pixel Tart RC12 Release Readiness Report

## 最终结论

**GO：进入受控真实机器验收。**

**NO-GO：尚不能标记为正式 Release Candidate。**

安装生命周期、自动回归、截图证据、规模门禁和产品语言扫描都已通过。唯一剩余 Release Gate 是真人在普通摄影师电脑上完成分辨率/DPI 矩阵、Asset Library 操作清单和第一次使用观察。

本轮继续完成了参考仿色 / 胶片工作区的收口：三栏在专注/窄屏状态下会真正释放栏宽；参考来源选择器支持项目方案、灵感板、自由画布、素材库、最近使用和本地导入；胶片方案、强度、颗粒尺寸、光晕、柔化、暗角和纹理参数现在随色彩方案非破坏性保存；渲染后端已抽象为可替换接口。该部分没有增加新页面或新产品模块。

## 候选版本身份

| 项目 | 值 |
|---|---|
| Version | `2.3.0-RC12` |
| Installer | `artifacts/releases/2.3.0/installer/Pixel Tart_Setup_2.3.0_RC12_x64.exe` |
| SHA-256 | `FF3FDC70C81C8B433348013AB4944A6F9527AA7CF487B5676799F9BD80AACFE0` |
| Size | 51,214,323 bytes |
| Closure source | `b8552458818d8540ad6399318ae32605046e0ad6` |
| Scope | Asset Library UX Closure；无新增功能或页面 |

最新已提交文档基线 `84d28df` 与 closure source 之间只有 `docs/reports/` 文档变化，产品源码、installer 定义和打包脚本没有变化。

## 安装测试

| 操作 | 结果 | 证据 |
|---|---|---|
| 第一次安装 | PASS | `installer-smoke/result.json` |
| 启动 | PASS，主窗口标题 `像素蛋挞` | 同上 |
| 正常关闭 | PASS | 同上 |
| 卸载 | PASS，exit 0，安装目录移除 | 同上 |
| 重新安装 | PASS，新隔离目录 | `installer-smoke-reinstall/result.json` |
| 当前 4K Windows 电脑安装/启动/卸载 | PASS | `docs/reports/RC12_INSTALLER_REPORT.md` |

## 自动质量门禁

| Gate | 结果 |
|---|---:|
| Release build | PASS，0 warnings / 0 errors |
| Core | 1,307 / 1,307 |
| DPI | 90 / 90 |
| Modular harness | 14 / 14 |
| WPF process isolation | 1,193 / 1,193，96 fixtures，0 skipped |
| Product Visual Harness | 71 / 71 |
| Scale | 10K / 50K / 100K，三次采样，PASS |
| Product language | 3 / 3；指定九词可见 XAML 扫描 0 命中 |

本轮追加验证：Release|x64 编译 0 警告 / 0 错误；核心测试 1,380 / 1,380，DPI 90 / 90，Modular harness 14 / 14。WPF 真人验收仍需在独立进程中运行，不能把同一 testhost 内重复创建 `System.Windows.Application` 的失败误报为产品崩溃。

## 截图证据

以下是当前 closure source 生成并经 manifest 哈希校验的真实应用截图。它们用于比对，不代替真人交互结果。

### 素材库主界面

![RC12 Asset Library clean](../../artifacts/rc12-product-visual/asset-library-ux-closure/01_clean.png)

### 右键菜单与二级菜单

![RC12 context menu](../../artifacts/rc12-product-visual/asset-library-ux-closure/02_context_menu.png)

![RC12 submenu](../../artifacts/rc12-product-visual/asset-library-ux-closure/03_submenu.png)

### 颜色筛选与 Inspector

![RC12 color filter](../../artifacts/rc12-product-visual/asset-library-ux-closure/05_filter_color.png)

![RC12 inspector rating](../../artifacts/rc12-product-visual/asset-library-ux-closure/06_inspector_rating.png)

### 灵感板、Quick Loupe 与查看大图

![RC12 inspiration board](../../artifacts/rc12-product-visual/asset-library-ux-closure/07_inspiration_board.png)

![RC12 active loupe](../../artifacts/rc12-product-visual/asset-library-ux-closure/09_loupe_active.png)

![RC12 full preview](../../artifacts/rc12-product-visual/asset-library-ux-closure/10_full_preview.png)

完整截图和元数据：`artifacts/rc12-product-visual/rc12-product-visual-evidence.json`。

## 发现问题

| 类型 | 结果 |
|---|---|
| Crash | 未发现 |
| 错位 / 自动布局阻塞 | 未发现 |
| 乱码 | 未发现 |
| 产品语言泄漏 | 未发现 |
| 当前验收环境限制 | 原生 WPF 窗口未暴露给本次桌面控制接口，无法执行真人点击/拖动/右键动作 |

最后一项是验收执行限制，不是产品缺陷。应用已在 3840×2160、100% DPI 的当前电脑上成功安装、启动并保持响应。

## 修复记录

Asset Library UX Closure 阶段已完成：

- 修复无效预览缓存测试图片。
- 修复空状态/灵感板 modal 的重叠误报。
- 修复 Quick Loupe 证据合成，使截图包含真实绑定的 1600px 高质量预览。

本轮未发现新的真实阻塞问题，未修改产品代码，未进行视觉重设计。

## 未关闭的真实机器 Gate

- 1920×1080、2560×1440、4K。
- 100%、125%、150%、200% DPI。
- 100 / 1,000 / 10,000 张真实导入及缩略图、滚动、选择、拖拽。
- 横图、竖图、超宽、超长比例人工确认。
- 查看大图、Quick Loupe、右键菜单、筛选、Inspector、灵感板全流程。
- 摄影师第一次打开、导入、查看大图和整理照片的“需要猜”记录。

可直接填写的完整清单：`docs/reports/RC12_REAL_USER_ACCEPTANCE.md`。

## Release 建议

保持当前二进制不变，交给普通用户电脑执行 `RC12_REAL_USER_ACCEPTANCE.md`。只有所有真人检查项完成、阻塞问题关闭，并收到明确的 `RC12 实机通过` 后，才把这一二进制提升为正式 Release Candidate。
