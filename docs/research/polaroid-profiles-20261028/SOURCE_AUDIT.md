# 现有能力 / 缺口：不另建产品引擎

## 已有
- `InstantFilm.cs`：8 个 legacy film ID；默认 `CreativeApproximationV1`，随机 `SplitMix64SubstreamsV1`、分流 seed 与快照、纸框几何和冻结。旧温度 math 是创意公式，不是测量响应。
- `InstantV3/CandidateRecipe.cs`：AlgorithmVersion3、chemical family/era、Response、Provenance、Optics、Emulsion、Surface、Paper/Defects/Creative 子版本、Random、Amount、冻结。i-Type/600 已共用 Modern response，SX70 参数明确写 creative。不能改 Evidence 字符串冒充测量。
- `CandidateTone.cs`：输入 encoded sRGB D65 [0,1]；Decode→linear luma/toe/shoulder/density/saturation/creative bias→linear；CandidateSpatial 再 Encode。旧彩色近黑存在 chroma scale t/l 与黑位相互作用；此报告不是批准修改它。
- `CandidateSpatial.cs`：颜色后 linear spatial/texture→encode→creative/defects→amount 与 encoded 原片混合。`CandidateRenderSession` 再做窗口、纸面、扫描表现，已有有限 halo、全源坐标、共享 strip/preview session，避免另起引擎。
- `InstantV3ProductPipeline.cs`：`Source.Read` 调 `ProfessionalRawPipeline.RenderPixel`，并移除 InstantFilm，避免叠旧胶片节点；同 recipe session 用于 preview/region。`ProfessionalRawPipeline.RenderPixel` RAW scene→Display，rendered input 提供已编码路径；不能再套一次 Display。
- `ReferenceColorWorkspaceViewModel.InstantV3.cs` 的 FreezeV3/源文档快照/seed；`InstantFilm` VM 的候选与原图/当前编辑选择。CurrentEdits 宽域→sRGB 胶片域已有桥接，不是宽域光谱模拟。
- `InstantV3DirectExport.cs`：strip 输出 TIFF/PNG/JPEG，目标嵌 sRGB ICC，编码后验证尺寸/方向/profile/像素；不应烘焙 monitor ICC。
- `InstantFilmEvidence.cs`：来源、ICC/hash、权限、观察、关系；现有 uncontrolled library 验证明确拒绝 CalibrationEligible=true，不可拿来装测量库。
- R5.3 同链 240px 缩略、seed 冻结和显影草稿已有；截至读取的 UI 纠偏仍 PARTIAL。

## 缺口 / 不能推断已完成
- 搜索 CameraProfile 仅见 RAW decoder 设置 `output.CameraProfile=null`（LibRaw 字段），不是 i‑2/SX‑70 的标定档。UI 相机选择/动画也不构成光学测量。
- 相纸可选名称≠每个化学世代都有独立经验证响应；无配对、无相机对照，不做 camera hue bias。
- 生产新档还缺 stable external profile ID/model hash/data hash/许可及 input-domain 的完整冻结、schema/version 兼容、缺档明确失败或随项目保留模型，不得静默回退最新档。
- 实测证据库应新增受控样本定义或显式版本，不把原本禁止 calibration 的 uncontrolled evidence reader 改名放行。
- 温度现有创意数值不能变成 measured curve。新模型先关闭 temperature-induced colour/随机纹理/缺陷；只显示官方趋势提示。
- 近黑修复候选只存在离线 Python，不注册生产，不替换既有 V3。算法批准仍缺第二成熟完整实现对照/真实 ICC/照片和旧 PV4/5/6 golden、全链 runtime 回归。Lab 插值数值对照不是此批准。

所有文件 hash：`evidence/source-files.json`。本批源冻结是所读文件 hash，不是整个并发 UI worktree 的不可变快照；起始和结束 HEAD 分开列。




