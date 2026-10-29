# Matrix float 第二轮 — 2026-10-29

结论：**OPEN；矩阵 float 候选继续研究，不准入产品；半尺度候选拒绝作为可用方案。** 从 808d4b719d734ab666ba11da8ddb5abf7cf09380 冻结，fetch、远端 SHA 一致，未改变产品默认处理，SpektraLab 另立检查点。

## 加固与实际文件
探针检查输入 SHA 白名单、实际加载 `raw_r.dll` 文件路径/SHA `F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9`，Sdcb DLL SHA `6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3`、版本及 x64、型号、最终 CFA 排列；仅允许清单中的两个 D800。要求四阶段名称恰好各一次，尺寸一致；生成清单含各缓冲长度和 SHA。分析器先验证清单、长度、哈希、源未改变与产品逐样本一致；拒绝其他条件。进度回调采样的是 **uint16 缓冲**；float 是之后对同一矩阵输入的计算。两次早期失败已记录：第一次在 unpack 前读 filters 0，第二次 unpack 后仍不是最终规范化值，均拒绝，没有输出被当成测量；因此最终 CFA 校验在处理后，并以原始/scale同坐标全部相等与实际回读交叉检查。

`analyze.py` 的可选第三参数写 `xb` float 文件，附 JSON 标识 RGB little-endian float32、线性 LibRaw ProPhoto D65、除以65535、负数和>1允许、无嵌入 ICC；不改原文件，不能把它冒充 D50 标准 ProPhoto。实际文件在本机临时研究目录，435,951,264 字节，SHA `dd923c658d376d62a9e3812bb6d069d29095bd0dfc848aa64eaf7294592d228d`；分块写出只保存本地。研究域数据与显示PNG均未进入Git。

## 结果与展示
D800 12bit 自然图与上批统计一致：float保留22个负通道及 6627 个溢出通道；4405 CFA 点在上游WB缩放时已裁切。第二个 D800 14bit lossless 清单样本（SHA `593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A`）实际执行：同模型和同布局，输出与产品 0 不同，矩阵预测与整数写回0不同，8负值、2628个上溢通道、1704个缩放裁切点。两文件是否同场未知，只是开发兼容测试，不是独立场景/保留组；不泛化到别的机型、边距、黑电平或第二绿映射。

A和B都采用同样的研究实现 `Display(ApplyExposure(...))` 数学（LibRaw ProPhoto D65矩阵逆、原固定肩部/中性轴/ sRGB transfer），EV 0及-2，两组原始 full PNG 和128×128相同坐标局部PNG保存在临时目录；研究脚本复现公式但未与产品 display 逐像素 cross-check，不称完整产品预览导出一致。EV0 全图平均绝对显示差 0.00002559，最大0.1650；EV -2 平均0.00001296，最大0.03297；8位输出分别704054和361435像素至少一通道不同，包括量化阈值差异，不等同可见收益或主观偏好。

(6637,83) 上游WB 69865.99→65535，矩阵蓝 66025.34→65535；-2EV时 float蓝0.2518705 vs整数0.25，显示蓝0.5411390 vs 0.5390099。值可区分，却不能恢复已经丢失的上游CFA信息。暗部负值(1600,2328) 矩阵蓝 -38.015→0；不存在将负值称为可恢复高光的依据。人工A/B偏好及可见收益/退化 NOT_RUN。

固定 RawTherapee 5.11 源 commit `db575c6690d30ce6428c0e1383bec8b8f1fb8d21`，Windows 官方 zip SHA `A13DEB582675E1F2AC5FB5BB2E34B6CDCDCA74F8A6351C878064E92270157A50`，CLI SHA `4071002692F4F2214BE93DE259D2C937FCE6C1AC77872E0A4E80B5178BEEE679`；实际 `rawtherapee-cli.exe -q -t -b16 -o <temp>/rt-d800.tif -c <12bit RAW>` exit 0，输出7370×4916 TIFF16 SHA `EA76F8027A2573BCA6BA7BFC4250115EB4842F520752C280236C40DC0A24D0A2`，含ICC。其默认前端、WB、去马赛克、裁边、tone未对齐，不用最终差值归因矩阵或色准。不是对 C1 的比较。

## WB 缩放余量的隔离预实验
已有固定 LibRaw 的 `scale_colors` 在 highlight=0 时 user_mul 按最小增益归一，因此单纯把全部 WB 乘0.5并不能证明留出余量。对同一真实 scale-before CFA 只做理论 `scale×0.5 → uint16 → ×2`：4405→0个理论溢出，15745314点量化绝对误差比基线大；尚未实际运行可生效的 LibRaw 余量设置及重新去马赛克，噪声/图像后果未知。此理论观察**不构成候选通过**；本批拒绝半尺度方案的产品应用，不把它与矩阵float收益混合。下一轮若要继续须真实参数读回并单独验证。

一次硬化探针进程 44,439 ms、峰值 2,231,795,712 bytes，包括阶段落盘、哈希及第二次产品解码，不是候选算法单次性能，不能与产品基线比较。候选耗时/增量内存、旧项目golden、完整预览/重开/导出、C1、色准、主观偏好及验证/保留组均 NOT_RUN。

## 复现
见 `MATRIX_EXPERIMENT.md`，现在运行 `analyze.py <stage_dir> <new_float_output>` 会先验证清单并额外写域说明sidecar；不允许覆盖已有输出。`render_compare.py <stage_dir> <float_file> <new_output_dir>` 同域渲染，`roi_compare.py` 输出局部；`scale_headroom.py <stage_dir>` 仅理论测试。原素材及大输出不要提交。第0号及第一批记录仍保留。
