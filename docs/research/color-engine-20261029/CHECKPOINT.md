# 当前接续检查点：第三批已落地（2026-10-29）

当前研究状态仍 OPEN，完整结果见 `ROUND3_REPORT.md`，最小后端契约 `BACKEND_CONTRACT_V1.md`，复跑 `REPRODUCE_ROUND3.md`。以下第0号保留为历史，不再代表“尚未运行”的当前状态。

- 矩阵float：共享校验18测试，实际产品Display四路全图门禁通过；独立默认关闭解码工具可在progress中直接生成float，衔接现有面积预览、RGB16导出、研究变体新进程重开，保留负/>1。仅固定D800开发范围，不注册产品默认；无C1/色准/主观偏好结论。
- WB余量：LibRaw highlight=1实跑，归一/参数读回、35480 scale样本预测0差；满值4405→0，但AHD与暗部改变，本批拒绝产品应用，不与矩阵收益合并。
- Spektra：完整固定源码/资源/工具已构建，9组合实际渲染；ABI取消−7、stride/alpha/所有权/释放/重复ledger、空间零值/条带均实际测试。CTest10PASS/1FAIL（BT传递边界）保持失败；商业分发许可未批准，暂缓接入。
- 产品src、UI/Grok布局、旧PV4/5/6和旧recipe未修改；不合main、不强推、不上传RAW/大缓冲/第三方engine资源。详细hash与原始日志见round3。
- 大像素本机根 `N:/pixart/color-engine-local-20261029`，上批stage在Temp保留；均非独立验证/保留组。需要独立场景、人工偏好、资源授权和失败修复时明确阻点。

---
# 色彩引擎第 0 号检查点（2026-10-29）

状态：OPEN / 本批无候选通过。仅只读探针，不改变产品像素或默认流程；不移交算法候选。C1 同源配对 NOT_RUN，色准、审美、跨机一致性 NOT_RUN。

## 基线和隔离
- 2026-10-29 fetch 后，公司 `origin/company/r54-closure-20261029` = `f8beaa37977e2b76b2096fa51f3a716a45f33274`；家用 `origin/home/r54-darkroom-20261028` = `ec71ed406c96f1b547091ca2fd66b92f95f062f5`，两侧独有提交分别 8/9，非线性关系。当前从公司 HEAD 建树，未合并家用；家用涉及渲染图和 RAW 输出等源码，不能当成相同版本。
- 调查来源 `origin/codex/raw-c1-feasibility-20261027` = `dbd19d66cebd043437c23433d2b1deb30dd78306`，仅历史证据；本轮已重新读当前源码和测试。未修改其他工作树。
- 已读 AGENTS、HANDOFF、open-source-first POLICY；原有 D800 为明确列名的本地兼容测试素材，文件哈希核验。未扫描其他私人目录。

## 本批限定的三个假设与结果
1. **现有整数前端可能压掉传感器未饱和的信息**：源码确定有截断操作；当前样本输出饱和位置不能定位原因，真实同条件前后对比 NOT_RUN，假设未验证。一次实际基线见 `baseline-d800.json`。
2. **不改变解包，后处理 float 矩阵可以保留矩阵越界信息**：预期只对矩阵之前尚存的值有效，无法挽救 WB 阶段已截断数据；尚无正确提取同一去马赛克输出的已验证接口与数值对照，NOT_RUN，未实现。
3. **保持传感器解包后在 float 进行黑位/WB/去马赛克可保留更多数据**：需确认各格式 CFA、边缘、相机白电平、去马赛克和 WB 参数同条件及许可证；NOT_RUN，未实现。不得将这些假设表述为高光可恢复。

## 当前产品链数值域（源码核验，不是每阶段实测）
`ProfessionalRawDecoder` 在 Unpack 前设置 `CONVERTFLOAT_TO_INT`，限制三色 Bayer，1 GiB 估算及 master 512 MiB预算；以 camera multipliers 设 user_mul、关闭自动/相机WB覆盖，AHD、ProPhoto、gamma 1/1、16bit、关闭自动亮度、highlight 0。LibRaw 0.21.1 `dcraw_process.cpp` 约 82/116/235 行依次黑位、缩放、后续插值及矩阵；`postprocessing_utils.cpp` 的 `scale_colors_loop` 对含黑位/增益值写回 uint16 时 CLIP，`convert_to_rgb_loop` 对 float 矩阵结果转整数再 CLIP。去马赛克工作缓存为 uint16，具体每一步及不同格式仍需单独证明。`MakeDcrawMemoryImage` 得 RGB16，随后 `/65535f`，float 容器不能撤销量化和截断。最终 RGB16 满值只是输出满值，不证明传感器饱和或可恢复；负/大于1可来自工作空间转换，不能推断传感器值。

LibRaw 类型 `libraw_types.h` 955-965 存在 float 原始图指针，但取决于输入/解码器，不代表常规整数 RAW 的完整 float 去马赛克输出；当前配置还要求 float-to-int。`libraw_processed_image_t` 暴露 bits/data 而非通用 float 管线。尚未验证通过现有 Sdcb 接口可得到所需端到端 float 数据，不能以字段存在宣称已解决。

当前 `RawWideOutputV1` 是默认关闭的既有 RGB16 后 LCMS 转换，无法恢复前端裁切；并行 `StudioGraphRenderer` 要求显示 sRGB，不可把新线性宽域直接冒充节点输入；宝丽来依赖独立定义，不用于判定基础 RAW 效果。LCMS 接口与 RAW wide 测试已查，现有默认 PV4/5/6 未改。

## 两个固定源码参考及许可证
- LibRaw 0.21.1 `cccb97647fcee56801fa68231fa8a38aa8b52ef7`，`src/postprocessing/dcraw_process.cpp`, `src/postprocessing/postprocessing_utils.cpp`, `libraw/libraw_types.h`；源码 SHA256 分别 `81EFB3BAB8631285123414F5E6DC2DE62069E2340BF54E7DAE41A55D7540C540`、`554D6A358915866B5FBC5BDDA8682F81B01F9619F522AEA7473EBDBBB5B32701`、`D83C7B9C7A8EA035B12F1D7B70F8ED686BAE360DA5B56C3FDF48DAC397B6402D`。项目现有运行时标注 LGPL-2.1-only OR CDDL-1.0；具体修改/再发布义务须独立审阅。默认参数不是所有文件/所有格式的保证。
- RawTherapee `db575c6690d30ce6428c0e1383bec8b8f1fb8d21`，`rtengine/rawimagesource.cc` SHA256 `65E06C3F795E67C16B0354B3F132EC1CF68BA25334F6BEDEF653F5EA01B1DE89`，`scaleColors` 约 2742 行用 float CFA、减黑位后下限0并按通道缩放；约 3550 行路径包含允许负数和超范围的工作空间变换，依赖此前处理和配置，不能简单套用公式。根 LICENSE 为 GPLv3，文件头及发布义务需完整审核；仅研究未复制代码。参考数值程序未安装/执行，不能算通过对照。

## 实测与限制
- 已列名 D800 素材 SHA256 `3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5`，解码前后 hash 相同。输出 7378×4924，RGB 各通道零 [0,0,22]、满值 [641,940,5046]，每通道 36,329,272 样本；这些是最终输出统计，不是 sensor 饱和。白位 4095，黑位读回 0，WB 505/256/352/256。单次 1739.2465 ms（包括解码器再次 hash），进程峰值工作集 1,062,768,640 字节（包含探针复制，不是算法增量），均非性能基准。
- 执行 `dotnet test ... --filter FullyQualifiedName~ProfessionalRawPipelineTests|FullyQualifiedName~RawWideOutputTests|FullyQualifiedName~Pv7PrototypeTests`：13 PASS，0 FAIL，0 SKIP；TRX 附后。它不等于完整 PV4/5/6 golden、真实 RAW 或预览/重开/导出门禁。
- 复现：`dotnet run --project tools/color-engine-baseline/ColorBaseline.csproj -- <明确授权RAW路径> <SHA256>`；只输出 stdout，校验 hash 前后，不保存或修改原片。测试使用 .NET SDK 10.0.401；实际时间与内存随机器及其他负载变化。

## 数据集与门禁
D800 单样本此前用于探索，**仅开发组**，场景身份未知；不可用为保留组。现有授权清单有型号、hash，但未完成逐场景审查、同源关联、授权范围和内容分区，不能把型号/哈希硬分成独立场景。验证/保留组均 NOT_ESTABLISHED；肤色、皮肤高光、中性、红橙紫、蓝绿、暗部、逆光、不同光照覆盖均 UNVERIFIED。参考色卡、C1 同源导出、独立参考运行、逐阶段观测、自然图差异、候选耗时/内存、PV4/5/6 golden、保存重开/预览/导出均 NOT_RUN。不可用合成数据冒充照片覆盖；无配对参考不报真实色差。

## 下一步
基于明确素材清单确定可追溯独立场景并冻结分组；选开发组查阅传感器真实白位和阶段前后值，搭建隔离且版本固定的参考执行链，确认格式/参数/白点；补第二条独立实现的可运行同域对照，再按政策填写节点完整模板。缺少真实阶段观测或许可结论时保持 OPEN，不改默认产品，不提交未经门禁批准的像素算法。
