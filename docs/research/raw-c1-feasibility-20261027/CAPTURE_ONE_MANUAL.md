# Capture One 人工实验清单（未执行）

适用安装：Windows，CaptureOne.exe 文件版本 **16.6.1.2962**。官方 AppleScript/JXA 支持 macOS；没有确认 Windows 可用且能读回全部设置的官方自动化。**不要运行虚构 CLI、直接编辑软件数据库、生产 .cos 文件或修改程序。**

## 0. 独立目录，先一张

1. 使用下方脚本把冻结清单中的 RAW **复制**到新的实验根目录。复制件会核验 SHA-256；原目录不创建 CaptureOne 子目录。不要直接浏览/导入原始素材目录。
2. 用户手动启动 Capture One。在 File → New Session 创建**全新测试 Session**；位置设为实验根的 `Session`，名称 `Guangjing-RAW-C1-20261027`。不打开/迁移现有 Catalog/Session，不更改全局默认、工作区、相机默认。
3. 仅导入 `InputCopies`，关闭导入自动调整/样式/自动同步。先只用 `D800-01` 对应清单的 12bit FX lossless 样本（实际清单中需按原路径确认；运行样本 ID `D800-12L`）。所有设置都只在选中测试 Variant 和测试导出 Recipe 内更改。
4. 记录 Help/About 的版本、Base Characteristics 的**实际处理引擎名**。安装版本不等于引擎名；不得用 16.6.1 推定引擎。

独立目录约定（相对实验根，不在原始 RAW 根）：

```
InputCopies/<sampleId>.NEF
Session/Guangjing-RAW-C1-20261027.cosessiondb
Exports/<camera-full-model>/<sampleId>/<caseId>.tif
Settings/session-settings.json
Settings/variant-settings.csv
Logs/export-receipts.json
```

复制准备示例（不会打开程序；实际根必须是尚不存在的目录）：

```powershell
& 'C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/tools/raw-c1-feasibility/Stage-C1Inputs.ps1' -Manifest 'C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/docs/research/raw-c1-feasibility-20261027/evidence/samples-selected.json' -ExperimentRoot 'C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/artifacts/raw-c1-feasibility-20261027/c1-manual-01'
```

## 1. 固定相机配置和白平衡

- ICC：在 Base Characteristics 为 D800 **手动选择并逐字记录** `Nikon D800 ProStandard`（名称以本机 UI 为准；若无此项记录 unsupported，不替换别机型）。本机目录存在 `NikonD800-ProStandard.icm`，**文件名存在不等于会话已选中**。
- 同时记录 profile 文件名、大小、SHA-256（可本地只读计算），只保留指纹，不复制商业 profile。相机 ICC 不是导出 ICC。
- WB：每源从 Shot/As Shot 开始，逐字记录 Kelvin、Tint、模式；对同源所有 Variant 固定。不要吸白墙/衣物、牙齿、眼白。不要把 C1 Kelvin/Tint 换成光镜同数值并声称运算相同。
- Curve：显式选择 `Linear Response` 和 `Film Standard`，不使用 `Auto`。逐机记录菜单支持性。**Linear Response 仍有高光滚降，不是传感器线性输出**；不拿 Linear Scientific 替代，不推定所有机型都有。
- ProStandard/Generic 如需另比，应独立新增一次 profile-only 实验，当前曲线实验保持同一 profile，不能同时更换 profile 与 curve。

## 2. 干扰项逐项关停并读回（unknown 不当 off）

每一行记录 UI 值、是否可关闭、是否确认。不能只写“已关闭全部”。

|类别|测试 Variant 目标|不能确认时|
|---|---|---|
|自动调整/导入样式/Auto Adjust|不执行；导入设置 none|unknown|
|Exposure/Brightness/Contrast/Saturation|0/0/0/0 基线|unknown|
|HDR Highlights/Shadows/Whites/Blacks|全部 0|unknown|
|Levels/Curve 工具（非 Base curve）|复位至未调整；逐项确认|unknown|
|锐化 Amount/Threshold/Radius|Amount=0；其余记录|unknown|
|Noise reduction Luminance/Color/Single Pixel|能设 0 全设 0，记录其余|机型固有前端降噪可能不可关|
|Clarity/Structure|0/0|unknown|
|Vignetting、Grain、Dehaze|0/none/0|unknown|
|Lens correction、Diffraction、Chromatic aberration、Purple fringing|关闭可关闭项；畸变/暗角/锐度衰减量归零|内置强制校正需写 cannot_disable|
|Crop/Rotation/Keystone|全画幅/无旋转调整/无透视；EXIF 方向另记|unknown|
|Local layers/Heal/Clone/Styles/Color Editor/Color Balance|无调整；不复制外部样式|unknown|
|Output sharpening/Watermark/Annotations|disabled|unknown|
|Demosaic/固有黑位/WB/机型隐藏处理|通常不可完全关停；记录 unobservable|不得称“RAW前端全关闭”|

## 3. 导出 Recipe

- 测试 Recipe 唯一新名称 `GJ_C1_16bit_ProPhoto_Uncompressed`，不覆写用户 Recipe。
- TIFF、16 bit、RGB、**Uncompressed**，Full/Fixed 100% 原尺寸，不缩放、不设像素上限；嵌入 **ProPhoto RGB** 输出 ICC。
- 不用 Embed Camera Profile。不要把 LibRaw 的“Linear ProPhoto D65”数据直接标为常规 D50 ProPhoto RGB。
- 记录 ICC 实际描述、嵌入 profile 字节 SHA-256、渲染意图（UI 若不可配置/不可确认就 unknown）、BPC 可观测状态、软件版本、时间戳、文件路径、尺寸、方向、裁切与 Lens 状态。
- 首张 `Linear Response / EV0 / Contrast0 / Saturation0` 导出后先运行 `tiff_gate.py`。读取必须为 16/16/16、RGB、单平面、embedded ICC、orientation=1、未压缩。方向不是 1 时先单独规范化并记录，不静默旋转。
- 看见全尺寸 TIFF 和成功日志不代表设置已验证；先对原预期尺寸和嵌入 ICC 核验，再继续批量。

## 4. 单变量，不全组合

`evidence/capture-one-planned-cases.csv` 有 **20 个未执行计划**：两源各 10 项（两个 curve/EV0 基线，Linear Response 上 EV −2/−1/+1/+2，Film Standard 上 contrast −10/+10、saturation −10/+10）。

- 先在本机控件确认 ±10 合法可输入并精确读回；否则标 unsupported，调整计划并写明实际合法值；不要钳制后仍把请求值当实际值。
- 每例只从同源未调基线克隆，单项改变。0 EV 基线复用，不重复执行 0 档。
- 每例至少记录 `sampleId, sourceSha256, caseId, processingEngine, cameraICC exactName/hash, curve, WB mode/K/Tint, EV, Contrast, Saturation, all interferers, outputICC hash, TIFF size/orientation, export status`。
- 曝光组不同时改 curve；contrast/saturation 组不同时改曝光/WB。不同引擎滑块不能按同名同值数学配对。
- 无成功首张验收前不执行 20 项批量。现在 **Session 未创建、C1 TIFF=0、C1 curve/contrast/saturation 实验=0**。

## 5. 接收和分析

- 只比较同源 RAW 与同取景的输出；先人工叠图确认方向、裁切、畸变和像素位置。不配对跨相机照片不能测跨机一致性。
- 用 `tiff_gate.py` 对相同源、统一 ICC 的 TIFF 做 LCMS 浮点 Lab D50 比较。必须给 source SHA 和 geometry-confirmed；这个参数是操作者声明，不是自动配准证明。
- 目前工具只放行 uncompressed RGB16；压缩 TIFF 会明确拒绝，不偷偷转 8bit。若需压缩格式，先增加受测高位深 reader，不能借 Pillow RGB 转换测 8bit。
- 显示 PNG/JPEG 只是观察附件；测量保留 TIFF16。肤色/高光区域由人工标注且只做相对变化，不能把相片区域叫标准色块。

ID映射以 evidence/sample-id-map.json 为准。计划C1的D800-12L/14L是探针别名，复制文件名采用samples-selected中的D800-01等ID，导入后按sourcePath/hash映射，不以相同ID字符串猜文件。
