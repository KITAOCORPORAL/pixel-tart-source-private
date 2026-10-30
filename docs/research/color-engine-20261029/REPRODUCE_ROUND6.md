# 第六批复现：授权自然照片，默认关闭

固定base `8a9f14a8b0281133cb5b829b5debece8589da303`，先fetch及读取AGENTS/POLICY/第六批报告。产品路径与MatrixDecodeResearchV2不编辑。只用用户授权`D:/AI AGENT/PixelTart-TestAssets/CompanyRAW`，不自动搜照片、不找D80014、不索要色卡。

## 生产输入（现有decoder，不是unsafe研究解码器）

```powershell
$dotnet='D:/AI AGENT/.dotnet/dotnet.exe'
& $dotnet build tools/spektra-natural-input/NaturalInput.csproj
& $dotnet tools/spektra-natural-input/bin/Debug/net10.0/NaturalInput.dll inventory
# 输出目录必须新建且在已授权的本地研究根内；禁止覆盖
& $dotnet tools/spektra-natural-input/bin/Debug/net10.0/NaturalInput.dll TNAN8886.CR3 B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401 <新production目录>
```

inventory只该明确根元信息/hash，不构成所有文件解码通过。Canon本机支持，其它项失败/预算与X-Trans范围见报告。生产decoder仍原1GiB/512MiB门禁；若失败不改阈值或换unsafe探针。

## A/B与代理/窗口

```powershell
python tools/spektralab-research/natural_prepare.py <production目录> <新inputs目录> --edge 1200
```

完整A/B真实保存，方向1；桥沿既定系数，B由实际产品Display函数输出。代理A在线性D50、B在编码sRGB做BOX平均，明确不是最终全图。准备脚本固定本场景窗口坐标，换图时必须先实际看图并改成**新显式研究版本**；不能把此坐标当通用场景检测。

当前执行根`E:/Kitao-Research/color-engine-round6-20261030`，目录`canon-production-v2`、`canon-inputs`。原失败`canon-production`保留，不能覆盖。

## 渲染/预算

固定Spektra源码c1447c6、旧隔离兼容patch DLL/source/resources，使用既有外部目录，不把完整源码/资源复制Git。Python3.13.13、NumPy2.4.6、Pillow12.2.0、psutil7.2.2、研究python-tools下tifffile2026.10.15：

```powershell
$engine='D:/AI AGENT/tmp/color-engine-company-20261030'
$local='E:/Kitao-Research/color-engine-round6-20261030'
$env:PYTHONPATH="$engine/python-tools"
$env:PYTHONDONTWRITEBYTECODE='1'
python tools/spektralab-research/natural_run.py $local $engine
# 只允许全新的同结构输出根；此命令拒绝覆盖已有case
```

每case fresh process；前6代理重复与重开、每route/stock全图预算gate、3native窗口、Portra边缘窗口重复与重开。`natural_case.py`的`case/reopen`可用于显式单项复跑，新目录、同hash，不会自动缩图。

第四个wire窗口是实际看图后补登记的同场景局部：

```powershell
python tools/spektralab-research/natural_extra_window.py $local $engine
python tools/spektralab-research/natural_contact.py $local
```

已有root不能重复extra-window；原`sky_wire`只天空，新增wire_edge(3500,1350,640,640)，并不创建新场景。图像不进Git。

预算：当前nvidia-smi free减256MiB，研究cap2GiB；估计76n+96MiB。超过则REFUSED_RESOURCE_BUDGET，不渲染、不改变尺寸。本轮20MP全图6项都拒绝；其它机器如实际budget允许，须记录真实运行，不预填结果。此启发gate不等于实测峰值，也不是适用所有参数的安全证明；新空间参数/更大中间缓冲需新预算，不复用未经验证估算。

## 既有桥/路线和unsharp观察

```powershell
python tools/spektralab-research/natural_diagnostics.py $local "$engine/references/colour-package" <新diagnostics目录>
```

Colour0.4.6原固定commit独立Bradford核对，A/B最大差先查域/顺序。该报告不把两路线差异当误差门槛。

针对Portra A/B `edge_people`窗口，使用上一批被动`build-trace`库及相同patch资源，环境变量SPK_RESEARCH_TRACE只当前process指向新目录；以原report resolved打开同input并full-render，必须输出hash等于无tap库。读取print_linear和print_scan_finish，文件都留本地。不得更改unsharp hidden参数或直接注册关闭按钮。

```powershell
python tools/spektralab-research/natural_unsharp.py $local
```

可先运行`python tools/spektralab-research/natural_trace.py $local $engine`生成当前root的新`unsharp-trace-A/B`；脚本拒绝覆盖已有trace。小640窗口，不重复巨型full logs。diagnosticPNG只有sRGB transfer+显示clamp用于观察，delta×50不是真实照片；没有创建新无锐化候选。

## 审核已有证据

```powershell
python tools/spektralab-research/verify_round6.py docs/research/color-engine-20261029/company-round6 --local $local
```

校验实际30独立配置+8fresh-reopen/8重复、38TIFF读回、6full拒绝、inputA/B与private output hash；它不替代设备执行。

报告/hash可以Git交付，PNG/TIFF/f32/npy/原片只在授权研究根。产品UI/模型/桥/默认链、旧PV4/5/6、D800白名单保持。自然full-size/肤色/高光/独立验证/许可仍需门禁；LightTable授权不替代Spektra代码或资源授权。
