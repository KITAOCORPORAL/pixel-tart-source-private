# 色彩候选输入/显示边界

源码 LibRawDecoder：DcrawProcess 使用相机或自动 WB、UseCameraMatrix=true、OutputColor=SRGB，专业解码输出 16 位；不是未经处理的相机线性数据。Gamma/高光默认行为本轮未独立测量，契约 HighlightsRendered=null，来源文本明确未知。

CandidateColorBoundary：
- CameraLinear 必须有显式相机到 ACEScg 矩阵，否则拒绝，不把相机 RGB 当 sRGB。
- DisplayEncoded 当前仅接受明确 sRGB，先逆传递函数再进工作空间；其他编码空间要求 ICC 转换。
- 已应用 WB 或 JPG 编码信号拒绝重复 RAW 增益。
- SceneLinear 中间负值/超范围保留；输出通过统一 ToSrgbOutput 进行显式 0–1 裁切与 sRGB 编码。
- 当前硬裁切仅为边界基准，不是专业高光/色域映射。
- 曝光候选在场景线性域；HDR 和选色仍未迁移新域。JPG 冷暖保留旧处理路径，不声称等价 RAW 白平衡。

正式 ProcessingVersion 1/2 未替换。v3 仍是实验候选，预览/导出可共用边界定义但尚未接入生产渲染。自动测试涵盖重复 WB 拒绝、负值/超范围保留及输出范围。真实 RAW/JPG、色卡、灰阶、肤色语料本轮 NOT_RUN，未借第二批或 C1 截图冒充。未对 C1 私有算法或顺序作已证实结论。
