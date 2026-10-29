# 第 1 批：矩阵阶段隔离实验

状态：OPEN；**实验 float 候选值得进入下一轮验证，但没有候选通过产品门禁。** 它是本地冻结 `MatrixFloatOutputResearchV1`，不接产品、不改 PV4/5/6；不能称为完整 float RAW 前端、高光恢复、色准或审美改进。

## 冻结与交付
本批基线 `f8beaa37977e2b76b2096fa51f3a716a45f33274`。远端已 fetch；家用 `origin/home/r54-design-fixes-20261029` 已前进至 `9f439febbeda90c91c0adf3a3dc9ce8d7ee2a1df`，本批有意不追入，避免改变实验基线。公司/家用分叉关系不可混同。第 0 号 `eced4d5c7fd82c9f86bf4ddc5f8eef2a70126cb4` 已推送并读回。

## 实验与接口
固定 Sdcb.LibRaw 0.21.1.7 + 现有 LibRaw 0.21.1 Windows x64 原生 runtime；在单独研究进程中用现有 `libraw_set_progress_handler` 在 `scale_colors` 与 `convert_to_rgb` 的 iteration 0/1 时同步复制 `libraw_data_t.image`（固定 ABI 偏移），不改 LibRaw、不替换产品 DLL、无原生补丁和额外依赖。该接口能在本样本取得两个操作前后完整 uint16 缓冲；它**不暴露函数中间的浮点数**，故矩阵 float 与 scale preclip 根据同次输入、参数和源码独立计算并核对实际写回，而不是声称实测内部浮点寄存器。研究版调用产品原始 Configure 并另外执行真实产品 decoder：输出 108,987,816 个通道样本 **0 不同**。两次解码不代表对其他机型的验证。程序将原片及中间大缓冲只写本地临时目录，Git 不包含它们。

固定代码参考 LibRaw 0.21.1 commit `cccb97647fcee56801fa68231fa8a38aa8b52ef7`：`src/postprocessing/dcraw_process.cpp`、`postprocessing_utils_dcrdefs.cpp` 的 progress 0/1 与矩阵构建、`postprocessing_utils.cpp` 的缩放/矩阵循环、`src/postprocessing/mem_image.cpp`。现有进度回调是 C API；进程内内存布局属于 ABI 特定研究实现。研究时经版本、x64、矩阵、Bayer、零边距与布局检查；现仅支持此 D800，其他边距/方向拒绝。LibRaw 文件 LGPL-2.1 或 CDDL-1.0，已有包随附许可证，新增再发行需独立法律与依赖审核；Sdcb MIT。无 GPL 代码被搬入产品。

独立数值复核：Python 3.12、NumPy 2.5.0 的 float64 矩阵乘法，使用同一个矩阵前缓冲及冻结 float32 系数，单独计算并与 float32 比较。它验证算术，不是另一个 RAW 解码引擎；RawTherapee 独立引擎实跑 **NOT_RUN**。合成负值、溢出、分数及缩放检查 PASS，不代表自然图覆盖。

## 域、对应关系与本样本
本样本原始 raw 7424×4924，raw pitch 14848 bytes；有效处理区域 7378×4924，top/left 均0，LibRaw flip=0，坐标 x 向右、y 向下，未做 EXIF 人工旋转。原始 CFA 数值为 uint16，阶段缓冲是 R/G/B/G2 四 uint16 通道，后插值矩阵前 RGB 是相机域整数；矩阵后输出是 LibRaw 线性 ProPhoto D65，0..65535 编码，候选 float 将该矩阵结果除以 65535，可小于0或大于1。前三通道在本实验是实际参与矩阵的值，第四是 CFA 第二绿通道。对这一文件 36,329,272 CFA 同坐标原始值与 scale_before 对应值全部相同；不能将此映射泛化到其他 RAW。

unpack 后 metadata 全局 white=4095，black=0，cblack 全0；此非独立传感器阈值校准，按该操作阈值 CFA >=4095 位置有1085，**物理饱和状态 UNKNOWN**，不以单点判定邻域。WB=[505,256,352,256]；highlight=0，按 LibRaw 源码最小 WB 归一化，actual pre_mul=[1.97265625,1,1.375,1]，scale = pre_mul×65535/4095，计算值见 JSON。对应 CFA 的 scale-before 和 after 预测与观测 0 不一致；4405 位置缩放前 float >65535 被写回裁切。样本四通道白位未有独立校准，不能把 white=4095 说成每色可靠饱和阈值。

同一矩阵前缓冲的 float32 计算、截断后预测与实际 LibRaw 矩阵后 RGB **0 不同**；矩阵后到 memory image RGB16 也 **0 不同**；仅这个文件和这次配置。分别产生负值 [0,0,22]、>65535 [641,940,5046] 通道样本，涉及 6018 不同像素；其中 2547 像素的同坐标 CFA 已在缩放时超过65535。剩余位置也不能证明邻域未截断。对范围内分数，RGB 数量 [36309218,36303225,36296716]，最大截断量均小于1编码值；float64 与 float32 运算最大差 [0.00794,0.01058,0.00655] 编码值。负值和越界是工作空间矩阵的数值，不是可恢复高光证明。候选结果按块计算哈希、不保存像素文件；原始缓冲哈希见 JSON。

JSON `examples` 为相同坐标原始 CFA、黑位后/缩放前、缩放 preclip/后、去马赛克 RGB、矩阵 float 与实际 RGB16。例 (6637,83)：CFA 3175<metadata 4095，但 WB scale 69865.99→65535；矩阵 B 66025.34→65535。去马赛克邻域信息完整性 UNKNOWN。例 (1600,2328)：矩阵 B -38.015→0，不能称恢复高光。

## 评价集关系与待补
D800 是历史明确授权清单 `docs/color-studio/raw-compatibility/01_RAW_CORPUS_MANIFEST.md` 的第三方本地兼容夹具、历史 RAW/C1 pilot 的开发探索样本，同一 SHA `3A0C...447B5`，其场景及关联拍摄未知，保留开发组。公司素材清单 `docs/color-studio/real-camera-company/COMPANY_RAW_FIXTURE_MANIFEST.json` 中 company-02/03 均 Fuji X-T5 同日相隔约5分钟，可能同场但未核验，应先作为同组候选而非强行拆分；company-01 Sony、company-04 Fuji、company-05 Canon 的场景内容与跨样本关联未知，无法仅凭机型/时间宣称独立。以上记录是清单整理，不新增扫描。验证/保留组 NOT_ESTABLISHED；肤色等覆盖尚未审查。C1 同源、绝对色准、跨相机、人工偏好、完整 PV4/5/6 golden、保存重开/预览/导出、候选性能和内存均 NOT_RUN。保留组不得用于调参。

## 复现（本机 PowerShell，不打开浏览器）
前提：Windows x64、.NET SDK 10.0.401、Python 3.12 和 NumPy 2.5.0，满足 Git 项目现有 NuGet 依赖；输入为清单明确授权且哈希一致的 D800 文件。指定一个不存在/空白的临时目录，输出约 1.4 GB 研究缓冲，远离原片。探针要求输出目录为空，不得覆盖旧实验。以下命令在研究工作树根执行，`$raw` 和 `$tmp` 按实际设置；后者不可置于原片目录。

```powershell
$raw = 'N:/pixart/RAW/Real/nikon/RAW_NIKON_D800_12bit_FX_LOSSLESS.NEF'
$tmp = Join-Path $env:TEMP 'gj-matrix-one-new-run'
dotnet build tools/color-matrix-probe/ColorMatrixProbe.csproj --configuration Debug
python tools/color-matrix-probe/analyze.py  # synthetic test first
dotnet run --project tools/color-matrix-probe/ColorMatrixProbe.csproj --no-build -- $raw 3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5 $tmp
python tools/color-matrix-probe/analyze.py $tmp > result.json
```

本次 DLL SHA256：probe `386259D88F7C035EFA02DBAF135EF15CC6779FE77A2B41B30C5508F6625E3C40`；Sdcb `6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3`；raw_r `F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9`。构建产物受路径/工具链影响，不要求其他环境 hash 相同；raw_r 哈希用于固定实际二进制。测试 TRX 13 PASS/0 FAIL；没有完整产品集成验证。基线原 `stages` 仅执行标签，已增加明确说明与 `stageObservations`；这次的真实阶段观测在 JSON 中。

## 结论
已验证此样本矩阵阶段的负值、上溢和小数会被整数路径丢弃，float 候选保留了**矩阵计算之后**的数值。没有验证此前 WB 截断可恢复，也没有证明这种保留提升画质。下一轮若继续，应以固定场景验证集、兼容性及参考实跑检验，再考虑显式版本输入输出契约；此批不予产品接纳。

