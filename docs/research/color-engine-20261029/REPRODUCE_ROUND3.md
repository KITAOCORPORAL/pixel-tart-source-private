# 实验复跑入口（第三批）

固定研究起点：3d39dc9e。所有命令在研究工作树根运行；不启动浏览器、GUI、UI重构或产品集成。不要将本地浮点图、原片、外部源码/资源/DLL放进Git。本机绝对素材文件仅使用已授权清单白名单。

## 共享门禁与实际产品显示
```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
dotnet build tools/color-matrix-probe/DisplayOracle.csproj
python -O tools/color-matrix-probe/test_stage_validation.py -v
python -O tools/color-matrix-probe/analyze.py <StageManifestV2目录>
python tools/color-matrix-probe/run_display_gate.py <stage目录> <新输出目录> tools/color-matrix-probe/bin/Debug/net10.0/DisplayOracle.dll <已验证float文件>
```
已有本机stage10与candidate位于 `C:/Users/Admin/AppData/Local/Temp/gj-matrix-research-20261029/stages10`；一般机器先依第一批probe生成固定阶段和manifest，不能把缺文件改成跳过hash。`run_display_gate.py`输出约3.5GB完整float64 oracle，仅本地。上批无manifest的stage不得自动补伪记录。

## 研究解码/余量、预览和重开
```powershell
dotnet build tools/color-research-decoder/ResearchDecoder.csproj
python -O tools/color-research-decoder/test_recipe_rejection.py -v
$raw='N:/pixart/RAW/Real/nikon/RAW_NIKON_D800_12bit_FX_LOSSLESS.NEF'
$hash='3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5'
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll $raw $hash <新matrix目录> matrix
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll $raw $hash <新headroom目录> headroom
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll reopen <已生成变体目录> <新重开目录>
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll $raw $hash <新性能目录> matrix --decode-only
```
`baseline`调用现产品原decoder，同三个模式均只在工具内；performance_summary.py读取记录名perf-{baseline,matrix,headroom}-{1,2,3}。演示路径不新增产品recipe。保存master负/>1保持，导出TIFF16是现有Display后的sRGB。域/flags及兼容边界见BACKEND_CONTRACT_V1.md。

## RawTherapee5.11受控参考
```powershell
& <已核对hash的rawtherapee-cli.exe> -q -t -b16 -p tools/color-matrix-probe/rt-aligned.pp3 -O <新输出.tif> -c $raw
```
`-O`会输出resolved pp3；比对尺寸/profile和配置，不从最终像素差归因矩阵。固定包和源commit见报告；没有“参数同名=实现等价”的断言。

## SpektraLab隔离构建
指定提交c1447c6，完整source2和vendor已取得，无submodule。只像素引擎；NativeRaw/Desktop OFF。必要工具另装到研究根，不放产品PATH或SDK目录。以下是本次工具组合（不是宣称官方唯一配置）：
- CMake3.31.10、Ninja1.11.1.4：`python -m pip install --target <研究根>/pytools cmake==3.31.10 ninja==1.11.1.4`。
- LLVM-MinGW release20250402 `llvm-mingw-20250402-ucrt-x86_64.zip`，SHA4EDC13...41D38；glslang16.6.0 Windows release zip SHA82BF43...A8D9B。完整hash在spektra-tools.json；先核对再解包到toolchain、glslang。
- Vulkan-Headers commit b5c8f996196ba4aa6d8f97e52b5d3b6e70f7e4e2/tree5ae5f51...。Vulkan driver为本机已有，不安装/替换产品驱动。
```powershell
& tools/spektralab-research/build-isolated.ps1 -ResearchRoot <隔离根>
```
**本机CTest失败1项是预期保留证据，不要删测试或改门槛**。脚本在CTest失败明确停止；需要独立研究运行时使用已成功build出的DLL，并在报告继续标明失败，不把跳过测试叫PASS。

```powershell
python tools/spektralab-research/prepare_resources.py <source2> <build-vulkan> <新overlay目录>
python tools/spektralab-research/run_matrix.py <source2> <build-vulkan> <overlay> <stage目录> <float文件> <新输出目录>
python tools/spektralab-research/spatial_tests.py <source2> <build-vulkan> <overlay> <新输出目录>
```
`grain_repeat.py`、`performance_probe.py`本轮固定默认临时研究根；换机器前显式编辑根路径并冻结脚本hash，不能拿旧输出覆盖。upstream windows_stripe_regression.py用同DLL/resources运行，参数rows1/7/17。

本机研究结果根 `N:/pixart/color-engine-local-20261029`；原stage缓冲在Temp保留，不扫描其他图片目录。本报告/脚本/数值hash可Git移交，第三方源码与完整图只保留本地。不将外部engine或GPL绑定代码直接复制进产品src。
