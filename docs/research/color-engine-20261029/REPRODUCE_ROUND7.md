# 第七批复现：输入安全、完整桥、分配下界

先fetch研究分支，基线`6dba62e133f2aff75226661b61dd8850ff5289c7`。读AGENTS/POLICY/COMPANY_ROUND7_REPORT、ALLOCATION_PATH_ROUND7和流式提案。不要重跑曲线、BT64、D80014缺源或六full；产品不注册。

## 1. 新输入准入和失败控制（无需GPU）

```powershell
python -O tools/spektralab-research/test_natural_admission.py -v
& 'D:/AI AGENT/.dotnet/dotnet.exe' build tools/spektra-natural-input/NaturalInput.csproj
# 只校验输出根，不解码RAW；源目录/根本身/已有证据必须拒绝
& 'D:/AI AGENT/.dotnet/dotnet.exe' tools/spektra-natural-input/bin/Debug/net10.0/NaturalInput.dll validate-output <全新E:/Kitao-Research内目标>
```

24控制使用小测试文件和禁止调用的engine factory。拒绝后只有failure report，无engine/upload/result成功。完整prepare math未改，CPU/GPU分开；不要因GPU gate通过省略CPU预算。

## 2. 绑定第六批已有文件（显式只读seal，不迁移）

```powershell
$engine='D:/AI AGENT/tmp/color-engine-company-20261030'
$root='E:/Kitao-Research/color-engine-round7-20261030'
python tools/spektralab-research/seal_natural_manifest.py 'E:/Kitao-Research/color-engine-round6-20261030/canon-inputs' "$engine/build-patch/engine/spektrafilm_engine.dll" "$engine/resources-patch" <新seal.json>
```

sealer必须对上Git冻结第六批manifest/生产报告/资源，不能把任意npy“算hash后批准”。本次原seal SHA `f0744c65454cb07fec47a79cc9a8ece5f717aa956be5a66f01f24ec1e763a3e0`，只是本地明确位置/bytes的身份，不是数字签名授权。

新准备输入则直接产生V2，不需要sealer：

```powershell
python tools/spektralab-research/natural_prepare.py 'E:/Kitao-Research/color-engine-round6-20261030/canon-production-v2' <新研究inputs目录> --edge 1200 --library "$engine/build-patch/engine/spektrafilm_engine.dll" --resources "$engine/resources-patch"
```

本次新目录`$root/bounded-preparation-v2`，10文件hash与旧一致，数学版本仍ExistingResearchD65ToD50V1。新清单SHA `54baac3f0fe1621bdf6671a7849e1596d1b616a776b3278934b305d3a038dcfb`。清单存路径，因此换目录须形成新seal和实际hash，不把旧目录identity无声改掉。

## 3. 有界small真实case/重开/逐行TIFF

```powershell
$env:PYTHONPATH="$engine/python-tools"
$env:PYTHONDONTWRITEBYTECODE='1'
python tools/spektralab-research/natural_case.py case "$engine/source2" "$engine/build-patch/engine/spektrafilm_engine.dll" "$engine/resources-patch" "$root/bounded-preparation-v2/A-linear-d50-proxy.npy" <新case目录> --manifest "$root/bounded-preparation-v2/admission-manifest-v2.json" --manifest-sha 54baac3f0fe1621bdf6671a7849e1596d1b616a776b3278934b305d3a038dcfb --repeat
python tools/spektralab-research/natural_case.py reopen "$engine/source2" "$engine/build-patch/engine/spektrafilm_engine.dll" "$engine/resources-patch" <case/report.json> <新reopen目录> --manifest <同清单> --manifest-sha <同SHA>
```

必须使用准确manifest SHA和输入登记route。旧natural_run/extra_window本批冻结，运行会明确退出，不能自动再投六full或随意追加window。要新window必须在新prepare版本明确登记后才可首次case，不从自记hash当批准。

`verify_round6.py`可追加`--admission-manifest <原显式seal> --manifest-sha <SHA>`审核旧首次输入/重开身份。它不把旧case重贴V2；旧原manifest未包含的额外wire窗口明确legacy-only。

## 4. 完整桥独立参考

隔离reference-tools安装OpenColorIO2.5.1，本机已固定tag commit004f800...；Colour0.4.6沿旧固定包。没有产品新依赖：

```powershell
python tools/spektralab-research/bridge_complete_check.py 'E:/Kitao-Research/color-engine-round6-20261030' $engine "$root/reference-tools" <新桥报告.json>
```

输入中性/原色/饱和/负/>1和自然RGB，实际blob系数/白点；Colour完整色空间path，OCIO MatrixTransform(INVERSE)+GroupTransform CPU处理。OCIO不独立估计CAT，限制明确。full自然negative审计16行块，报告符号来源和数量，不clamp/替换矩阵。

## 5. allocator测量（只small递增，有风险立即停）

隔离新的clean engine worktree到`$root/source-allocation-ledger`：

```powershell
git -C "$engine/source2" worktree add --detach "$root/source-allocation-ledger" c1447c6ab618fa4b1cb11fd2fe12826855007059
python tools/spektralab-research/apply_allocation_ledger.py "$root/source-allocation-ledger"
```

照第四批固定CMake flags构建shared DLL到`$root/build-allocation-ledger`：nativeRaw/Desktop OFF、Release、clang20.1.2/glslang16.6.0、同Vulkan-Headers。诊断只改allocator事件，**不改pixel math、不改资源**，source/DLL hash必须保留。stable VkDeviceMemory是事件identity，不再用会因staging移动的Buffer地址。

```powershell
python tools/spektralab-research/allocation_measure.py $root $engine <新measurements目录>
```

3尺寸321×215/641×429/1200×801，以完整场景代理，不拼独立ROI；8短进程whole/stripe37/stripe113/budget1，保持隐藏unsharp。输入prepare的CPU通道临时量另需预算；本轮<=1MP GPU分配实测<104MB，未靠bigfull试OOM。raw日志留本地，Git只summary/hash/source anchors。

## 6. full不要盲目执行

本次`company-round7/full-path-feasibility.json`是真实决策快照：56N下界1,129,537,920 > 安全准入1,095,761,920bytes，full engine/open/render=0。其快照可能随其它app变化，**不是永远禁止/支持的设备判定**。2GiBcap与reserve不取消；如果未来空闲变多仍需用同源码/allocator测量的新安全上界，不仅比较粗下界就放行。

本轮没有streaming实现；STREAMING_INTERFACE_PROPOSAL_ROUND7仅提案，需新外部版本/源码hash/DLLhash、小whole exact与真正有界分配，再一次full PortraA。不要把该文件里的概念symbol当可调用ABI。

可不访问GPU复算记录的阻点：

```powershell
python tools/spektralab-research/reproduce_full_blocker.py docs/research/color-engine-20261029/company-round7/full-path-feasibility.json
```

这复算当时快照/源码下界，**不重提交full**；不要据当前nvidia-smi used反推过去本进程峰值。

## 7. 结果审核

```powershell
python tools/spektralab-research/verify_round7.py docs/research/color-engine-20261029/company-round7
```

校验24预engine失败控制、输出根、first-manifest/reopen、10准备hash、桥、8allocator/5exact、full阻点。它读取已执行证据，不提交新full。

RAW/所有private像素与第三方source/DLL/SPIRV留本地；Git工具/数值/hash/报告。default-off、no UI/模型/桥修改、no注册、no seed/WB/曲线新实验。GPL/逐资源许可与LightTable非商业条款独立，不能互替。
