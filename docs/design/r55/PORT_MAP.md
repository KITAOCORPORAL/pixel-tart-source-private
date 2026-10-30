# LightTable → 光镜 观片台 移植对照（PORT_MAP，给 Codex）

> 源码不随包附带（src/ 为当时的只读克隆），请从下面两个仓库链接获取。

源：https://github.com/hqiy0831/LightTable-Windows （`src/win`，hqiy0831/LightTable-Windows，release v0.1.75.1-windows-preview）为主；https://github.com/fuyuan9972-svg/LightTable （`src/orig`，fuyuan9972-svg/LightTable，macOS Swift）作视觉/行为权威对照。只读克隆，**不要向任何仓库提交**。
许可：MIT（Copyright (c) 2026 fuyuan9972-svg）；用户已取得原作者许可（非商业使用）。保留版权与署名，见文末。

## 0. 技术前提（最重要）
- Windows 移植是 **WinUI 3（WindowsAppSDK 2.5.1）+ Win2D 1.4.0**，不是 WPF。Win2D 不能直接嵌入 WPF。
- **可直接复用（纯 C#，无 UI 依赖）**：`Core/**`（Geometry、Optics、Models、Slideshow 运动曲线、State）、`Audio/MechanicalAudio.cs`（数学合成音效，MIT）。
- **需重写渲染层**：`Rendering/**`、`Interaction/**`、`App/Controls/**` 依赖 Win2D/WinUI（CanvasDevice、VirtualKey、Pointer）。光镜 WPF 方案二选一：(a) WPF `DrawingVisual`/`WriteableBitmap` + 现有代理图管线（推荐先做，满足 1600 代理）；(b) D3D11 共享纹理（Vortice/SharpDX）+ `D3DImage` 承载着色器（灯光高光掩模、镜片）。着色器逻辑可参考 `Rendering/Lighting/OpticalPhoto.cs`、`Core/Optics/SrgbOptics.cs` 改写为 HLSL。
- 输入：把 `VirtualKey`/Pointer 换成 WPF `KeyDown`/`Mouse*`/`Manipulation*`。

## 1. 功能对照

| 功能 | Windows 移植源文件/类 | 作用 | 光镜 WPF 集成要点 |
|---|---|---|---|
| 灯板 关/开 + 辉光 | `Core/Optics/LampState.cs`（Toggle/TryIgnite/Sample）、`Core/Optics/LightResponse.cs`（时间曲线 .16/.28/2s、panelWhite）、`Rendering/Canvas/TableRenderer.cs`；原版 `ContentView.swift` LampPanel（16px 白描边 + 12px 模糊、底边刻度） | 未开灯暖米底材，开灯约 2s 冲光回落，边缘柔光 | 状态存项目（开/关）；**灯光仅为显示层**，绘制在台面 Visual，不得写入变体/导出/比较结果/代理缓存；尊重系统“减少动态效果” |
| 亮度 0–160% | `LightResponse.Brightness`（Clamp 0..1.6）、`MainWindow.cs` 第 149 行 Slider（Win 版 0–160%，原版 10–160%） | 灯板与透光亮度，双击复位 100% | 放胶囊组；光镜取 10–160%（同原版，避免全黑）；只影响显示 |
| 玻璃放大镜 + Space 凑近 + 8× | `Core/Optics/LightResponse.cs` LoupeGeometry（直径 260、1.5×、凑近 = 短边 94%、动画 .50/.34/.26s）、`Core/Optics/LoupePose.cs`、`LoupePower.cs`、`App/Controls/MagnificationSwitch.cs`（144×34 胶囊）；原版 `LoupeExperience.swift`（镜边径向/角向渐变、高光弧） | 跟随指针的圆形镜片，Tab 切原倍率/8× | 镜内取样来自**代理图（最长边 1600）**；镜边常显「代理」徽标；8× 超出代理分辨率时封顶并提示；全分辨率流式 = 后续 |
| 台面平移/缩放 | `Interaction/TableInput.cs`（panning：中键或空白处拖动）、`Core/Models/Document.cs` Viewport/FitScale | 无限台面视口 | 视口存项目；滚轮缩放需与光镜滚动约定统一 |
| 整理全局 / 一键平铺 | `App/State/TableSession.cs` `Fit(bool replace)`、`Core/Geometry/Geometry.cs` Fit | ⇧Z 回到全局视图；一键平铺重排（清空散片布局） | 一键平铺必须先确认（会丢布局）+ 可撤销 |
| 自由旋转/缩放 | `Core/Geometry/TableManipulation.cs`（Apply anchor/translation/scale/rotation）、`TableInput.cs`（Alt+拖动旋转、触控 Manipulation） | 任意角度、缩放 | 旋转/缩放属于台面布局，存项目，**不改变体像素**；保留 90° 快捷旋转 R |
| 框选 / 多选 | `TableInput.cs`（Shift+拖动 marquee，Ctrl 点选）、`TableRenderer.SelectMarquee` | 多选移动 | 多选后上下文条只显示可批量操作（移除、比较限 2 张） |
| 底栏托盘 + 已上台 | `Interaction/TrayInput.cs`、`Rendering/Film/FilmContactSurface.cs`、`TrayFlight.cs`、`Core/Geometry/SleeveMotion.cs`、`Core/Models/PhotoCollections.cs` | 胶片袋缩略图、拖上台飞行动画、「已上台」标记 | 用光镜**胶片条**承载，不新增「栏目」概念；条目身份 = 变体 ID（不是文件路径）；已上台标记来自灯箱固定列表（上限 100） |
| 幻灯动画 + 声音 | `Core/Slideshow/ProjectionMotion.cs`/`ProjectionPlayback.cs`、`Rendering/Slideshow/ProjectionRenderer.cs`、`App/Windows/SlideshowWindow.cs`、`Audio/MechanicalAudio.cs` | 黑底 84%，出片 .08/黑场 .12/入片 .18s，机械音 15% | 播放代理图；声音默认开且可关；顺序按灯箱（随机为选项）；**不复制原版非 MIT 录音**，只用合成音 |

## 2. 不可原样移植（与简报冲突）
- 放大镜/凑近/8×：必须显示「代理」徽标，永不显示「原图 100%」，直到全分辨率流式实现。
- 灯光、亮度、开灯冲光：永不进入导出、变体、剧照、比较结果或代理缓存。
- 「栏目」「相册/胶片袋页」「回到过去」（高斯场景，SHARP 模型仅限非商业研究）——不移植。
- 应用内更新（UpdateService/Sparkle）、诊断采样上传——不移植。
- Delete 在 LightTable 会删除底栏照片；光镜 Delete 只「移出灯箱」，永不删除变体或源。
- 一键平铺清空布局——光镜须确认 + 撤销。
- 色温调节：LightTable 没有（固定中性白），光镜「色温」仍为后续。EDR 背光：仅 macOS 原版，Windows 移植未提供，光镜为后续。

## 3. 快捷键冲突与建议（灯箱焦点内生效）
| LightTable | 光镜已有 | 建议 |
|---|---|---|
| Ctrl+L 开/关灯 | Ctrl+L 打开灯箱/返回暗室 | 保留光镜 Ctrl+L；灯光改 **L** |
| A 指针 / V 放大镜 | 暗室 V = 工具 | 灯箱焦点内 A/V 生效，与暗室按焦点分离 |
| Tab 切倍率 | Tab = 焦点导航（无障碍） | 仅在放大镜激活且指针在台面时拦截 Tab，否则保持焦点导航 |
| 按住 Space 凑近 | Space = 局部查看（点按） | 放大镜模式下按住 = 凑近；指针模式下点按 = 局部查看（代理） |
| ⇧Z 整理全局 | 无 | 采用 Shift+Z |
| Shift+拖动 框选 | 无 | 采用 |
| Alt+拖动 旋转 | 无（R=旋转 90°） | 采用，R 保留 |
| Delete 收回/删除 | Del = 移出灯箱 | 光镜语义：只移出 |
| ↑↓←→ 幻灯翻页 | 无冲突 | 采用；F5 进入幻灯，Esc 退出 |
| E EDR | 无 | 不采用（后续） |
其余光镜键 B、\、W、Shift+W、F2、R 不受影响。

## 4. 关于 / 开源许可 页面文本（原样放入）
```
观片台功能部分基于 LightTable（观片台）
原作者：抖音 @怕蛇LightTable · https://github.com/fuyuan9972-svg/LightTable
Windows 移植：hqiy0831 · https://github.com/hqiy0831/LightTable-Windows
经原作者许可在光镜中使用（非商业）。

MIT License

Copyright (c) 2026 fuyuan9972-svg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
另：若复用 Win2D / WindowsAppSDK 组件，需附各 NuGet 包自身许可（见 src/win/THIRD_PARTY_NOTICES.md；其中 Win2D EULA 链接核对为未闭合项）。不得打包原版非 MIT 录音。

## 5. 堆叠与层序（追加）
- **已有（可移植）**：层序。`Core/Models/Document.cs` `TableOrder`（List<Guid>，按绘制顺序）、`BringToFront(id)`（第 163 行）；`App/State/TableSession.cs` 第 72 行点击选中即置顶；`PhotoCollections.cs` 把 TableOrder 与 Slides/Viewport 一起存档。原版 `ContentView.swift` 第 806 行 `.zIndex(layerIndices…)`。
- **没有**：照片之间的透光叠底。原版仅对单张照片 `colorMultiply`（随灯光透射率变暗/亮，ContentView.swift 1057/1240）与 `CIScreenBlendMode` 光晕（PhotoOptics.swift 115/135），照片之间是不透明覆盖。→ 光镜 **后续/新开发**。
- 光镜实现建议：开灯时每张照片视为透射率 T（线性光），台面结果 = 灯板 × ΠT（重叠处相乘）。WPF：自定义 `ShaderEffect`（HLSL ps_3_0，读取已渲染的下层 + 当前层相乘）或 D3D11 离屏合成后经 `D3DImage` 显示；关灯为普通 Over + 接触阴影。所选照片不改层，只加选框。
- 交互：点击 = 置于顶层；上下文条图标「置于顶层」；右键 / … 「置于底层」；层序随灯箱布局按项目保存，可撤销。
- **仅显示**：叠底合成不得进入导出、变体、比较、代理缓存；局部查看 / 放大镜只取所选单张代理。

## 6. 侧栏（光镜决定）
- 灯箱内左右栏默认都收起为 22 窄条，灯板铺满（接近 LightTable）。展开状态**按用户记住**（跨项目/会话），理由：属于个人工作习惯，不是项目内容。
- 选中照片不自动展开右栏；上下文条「信息」「生成胶片」图标展开右栏对应页。
