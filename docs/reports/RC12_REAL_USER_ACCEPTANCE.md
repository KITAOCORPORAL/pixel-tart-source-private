# Pixel Tart RC12 Real User Acceptance

## 验收状态

**PHYSICAL ACCEPTANCE = PENDING**

RC12 已进入真实用户电脑验收阶段，但尚未完成整套真人操作矩阵。当前结果可以分发给受控验收人员，不能标记为正式 Release Candidate。

- Product: Pixel Tart `2.3.0-RC12`
- Acceptance target: Asset Library UX Closure candidate
- Closure source: `b8552458818d8540ad6399318ae32605046e0ad6`
- 禁止范围保持不变：不新增功能、页面、Moodboard、Planning Center、AI、Browser Clipper 或新的 Asset Library UX 改造。

## 安装包

`artifacts/releases/2.3.0/installer/Pixel Tart_Setup_2.3.0_RC12_x64.exe`

- SHA-256: `FF3FDC70C81C8B433348013AB4944A6F9527AA7CF487B5676799F9BD80AACFE0`
- 安装、启动、卸载、重装：PASS。
- 详细记录：`docs/reports/RC12_INSTALLER_REPORT.md`。

## 当前真实电脑记录

| 项目 | 结果 |
|---|---|
| 操作系统 | Windows 10 专业版，10.0.19045，x64 |
| 显示 | 3840×2160 @ 60 Hz |
| DPI | 96 / 100% |
| 安装 | PASS，exit 0 |
| 启动 | PASS，窗口标题 `像素蛋挞`，进程响应正常 |
| 关闭 / 卸载 | PASS，卸载 exit 0，安装目录已移除 |
| Asset Library 人工交互 | 未执行：本次桌面控制接口没有暴露已启动的原生 WPF 窗口 |

注意：应用窗口真实存在且正常响应；“未执行”是验收控制通道不可见，不是产品启动失败。没有用自动截图替代真人点击结论。

## 真实机器矩阵

下面每一行都需要验收人员在对应的真实显示配置下填写。自动生成的逻辑 DPI 截图只作为对照，不把状态改成 PASS。

| 分辨率 | 100% | 125% | 150% | 200% |
|---|---|---|---|---|
| 1920×1080 | PENDING | PENDING | PENDING | PENDING |
| 2560×1440 | PENDING | PENDING | PENDING | PENDING |
| 3840×2160 / 4K | 安装生命周期 PASS；交互 PENDING | PENDING | PENDING | PENDING |

## 操作 Checklist

每项记录 `PASS / FAIL / BLOCKED`，并附电脑、分辨率、DPI、照片集和截图编号。若为 FAIL，记录可复现步骤；只有 Crash、错位、乱码或功能不可理解才进入 RC12 修复。

### 1. 导入与素材网格

| 照片数量 | 导入完成 | 缩略图 | 滚动 | 选择 | 拖拽 | 结果 / 备注 |
|---:|---|---|---|---|---|---|
| 100 | ☐ | ☐ | ☐ | ☐ | ☐ | PENDING |
| 1,000 | ☐ | ☐ | ☐ | ☐ | ☐ | PENDING |
| 10,000 | ☐ | ☐ | ☐ | ☐ | ☐ | PENDING |

### 2. 图片比例

| 类型 | 无黑边 | 无裁切 | 无变形 | 结果 / 备注 |
|---|---|---|---|---|
| 横图 | ☐ | ☐ | ☐ | PENDING |
| 竖图 | ☐ | ☐ | ☐ | PENDING |
| 超宽 | ☐ | ☐ | ☐ | PENDING |
| 超长 | ☐ | ☐ | ☐ | PENDING |

### 3. 查看大图

| 检查项 | 结果 / 备注 |
|---|---|
| 点击后载入原图，而不是放大缩略图 | PENDING |
| 100% | PENDING |
| 缩放 | PENDING |
| 拖动 | PENDING |
| 下一张 | PENDING |

### 4. Quick Loupe

| 检查项 | 结果 / 备注 |
|---|---|
| 卡片右下角出现放大镜 | PENDING |
| 点击打开 | PENDING |
| 显示高清细节 | PENDING |

### 5. 右键菜单

| 菜单层级 | 图标 | Hover | 二级菜单 | 颜色对比 | 结果 / 备注 |
|---|---|---|---|---|---|
| ☐ | ☐ | ☐ | ☐ | ☐ | PENDING |

### 6. 筛选

| 评分 | 标签 | 颜色 | Hex 输入 | 色板拖动 | 结果 / 备注 |
|---|---|---|---|---|---|
| ☐ | ☐ | ☐ | ☐ | ☐ | PENDING |

### 7. Inspector

| 按钮不过曝 | 星级清晰 | 结果 / 备注 |
|---|---|---|
| ☐ | ☐ | PENDING |

### 8. 灵感板

| 拖入图片 | 新建灵感板 | 切换 | 结果 / 备注 |
|---|---|---|---|
| ☐ | ☐ | ☐ | PENDING |

## 摄影师第一次使用记录

本轮没有可用的真人摄影师参与者，以下记录保持空白，不能由自动化代填。

| 场景 | 观察 | 是否需要“猜” | 当时可见文案 | 期望 |
|---|---|---|---|---|
| 第一次打开：是否知道素材库在哪里 | PENDING | — | — | — |
| 第一次导入：是否需要思考 | PENDING | — | — | — |
| 第一次查看大图：是否符合预期 | PENDING | — | — | — |
| 第一次整理照片：是否理解 | PENDING | — | — | — |

所有“需要猜”的地方都应记录：前一步操作、屏幕上看到的文字、用户实际猜测、期望结果和截图编号。不要把建议扩展成新功能或视觉重设计。

## 产品语言最终扫描

- `ProductLanguageLeakTests`: 3/3 PASS。
- 额外扫描用户可见 XAML 字面量：0 命中。
- 扫描词：`TaskId`、`P3Query`、`SHA`、`LibRaw`、`False`、`True`、`AssetId`、`Viewer`、`Query`。
- 绑定名、自动化标识、数据库字段和折叠诊断信息不属于用户可见 UI；扫描没有把这些内部标识误报为界面泄漏。
- 原始结果：`artifacts/rc12-real-user-acceptance/product-language-final-scan.json`。

## 自动证据（仅作操作对照）

- WPF process isolation：96 fixtures，1,193/1,193 PASS。
- Product Visual Harness：71/71 PASS。
- DPI：100%、125%、150%、200% 共 32 张当前版本截图。
- 分辨率：1920×1080、2560×1440、3840×2160 及组合截图。
- Scale：10K / 50K / 100K，每档三次采样，PASS。

截图目录：`artifacts/rc12-product-visual/asset-library-ux-closure/`。

## 阻塞问题与修复规则

本轮没有发现新的产品 Crash、错位、乱码或自动门禁阻塞，因此没有产品代码修复。

真人验收若发现以下问题才修复：Crash、错位、乱码、功能不可理解。禁止借机做视觉重设计、新页面或新功能。

## 放行条件

只有当上面的真实机器矩阵、八组操作 Checklist 和摄影师第一次使用记录全部有真实观察，所有阻塞问题关闭，并由验收负责人明确给出 `RC12 实机通过`，才能制作/标记正式 Release Candidate。
