# 最小家庭接续：gray-boundaries子阶段被动观测（2026-11-02，Asia/Shanghai）

## 本轮结论/停止点

公司只执行1次graywhole，finalRGBA16与公司封存gray0差；已确认本端upsample→exposure bitidentity，actualCPUcast/upload/device常数一致。**跨端首因未闭合，因为home无对应tap。** 不再公司重复观测、不重跑3case/stream/RAW/20MP/六图/候选；先读COMPANY_FILM_SCALE_LOCALIZATION_20261102与DECISION.json。

## 公司实际路径和身份（历史执行记录，不覆盖重跑）

- 原diagnostic source：`E:/Kitao-Research/color-engine-company-diagnostic-20261102-build/source-diagnostic`
- 新observer root：`E:/Kitao-Research/color-engine-scale-localization-20261102-v2`
- 运行source/build：`source-scale-tap-v1` / `build-scale-tap-v1`
- actualDLL SHA：`08b9e146263d6988cb962cb791ad786ca3301b872d471f2ad53e366b9e09cb63`
- 完整localcapture：`$root/gray-once`，其中fullruntimeLUT/sensitivity不进Git
- 公司一次执行开始：execution-receipt.json里的Asia/Shanghai实际时间；1engineCreate/1open/1wholerender/7被动reads，新增数学dispatch0，初始化probe未独立计数。

初始`...20261102`目录是0GPU/0build的setup路径失败，保留；-v2是显式修正路径后的新目录，不自动循环GPU重试。固定工具从原CMakeCache取真实路径，不以HOME拼接推测路径。

## 家庭只做一次同观察（对应设备有条件时，不在公司伪造）

准备reviewedresearch新commit后，在全新外部checkout：

```powershell
git -C <已有固定externalengineRepo> worktree add --detach <新home-scale-tap-source> c1447c6ab618fa4b1cb11fd2fe12826855007059
python tools/color-round11/diagnostic/generate_scale_substage_tap.py <家庭封存diagnostic-source> <新home-scale-tap-source>
```

generator校验公司已封7file身份；home只允许engine.cpp原两处authorized-root字面量归一化，其他numericsource bytes必须相同。它只拷原diagnostic改动，再加被动writes/7reads/mode记录；shader不改、原dispatch顺序不改。source/DLL必须新hash，不能沿用公司身份。

构建沿原home已封相同工具/Release flags（不是换精度/FMA或优化）：

```powershell
& <固定cmake.exe> -S <新home-source> -B <新home-build> -G Ninja -DCMAKE_BUILD_TYPE=Release `
 -DCMAKE_CXX_COMPILER=<原clang++.exe> -DCMAKE_C_COMPILER=<原clang.exe> -DCMAKE_MAKE_PROGRAM=<原ninja.exe> `
 -DBUILD_TESTING=ON -DSPEKTRALAB_REQUIRE_VULKAN=ON -DSPEKTRALAB_BUILD_TEST_DLL=ON `
 -DSPEKTRALAB_BUILD_NATIVE_RAW=OFF -DSPEKTRALAB_BUILD_DESKTOP=OFF `
 -DSPEKTRALAB_VULKAN_HEADERS=<原VulkanHeaders/include> -DSPEKTRALAB_GLSLANG=<原glslang.exe>
& <固定cmake.exe> --build <新home-build> --target spektralab_engine_shared --parallel 4
# 复制对应原toolchain运行DLL到新build/engine；不用系统PATH/driver改动
```

失败留输出并停止，不自动重试；不重做diagnosticframework，不生成新fixture，不调任何渲染参数。此tap只支持一批单graywhole，第二次open会导致重复观测文件明确拒绝，所以Output必须新建。

```powershell
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
python tools/color-round11/diagnostic/run_gray_scale_tap.py `
 --checkout <新home-source> --library <新home-build/engine/spektrafilm_engine.dll> `
 --resources <已锁定73resources+34SPIRV根> --output <全新home-gray-once目录> --role home
python tools/color-round11/diagnostic/verify_gray_scale_tap.py <home-gray-once> --role home
```

runner只加载固定gray17×33+99resolved/完整resources。首次前execution-before记录实际homeGPU/driver/platform/工具source/DLL/runtime/flags/objectcommands；1wholerender。homefinal必须对封存home-gray逐通道0差，验证器不把公司常数填成home；**CPUcast/upload/device相同、EV0bitidentity和mode本身在验证器中是实际观察值，不作为必须等于公司结果的断言**，否则会隐藏待定位差异。

## 家庭最小材料清单（完整localcapture）

- film-sensitivity.cpu.f64（243double）
- tc-b-matrix.cpu.f64；tc-b-matrix.uploaded.f32；tc-b-matrix.device.f32（9）
- tc-lut.cpu.f64；tc-lut.uploaded.f32；tc-lut.device.f32（192×192×3）
- 00-input.device.f32（17×33×3），01-tc（17×33×2），02-brightness（17×33×1）
- 03-upsample、04-exposure（各17×33×3）
- exposure-scale/offset.cpu.f64及.uploaded.f32（各3）
- selected-math-mode.jsonl、run.json、execution-before.json、input.npy、whole-observed.rgba16

完整setupLUT/sensitivity属于原engine/profile-derived runtime数据，只留授权外部本地，不自动Git上传。Git公司`public-gray-taps`只是明确的公开子集；缺完整LUT/sensitivity时complete-localvalidator/pairedcomparator应失败，不伪报材料齐全。

两端完整localcapture到位后仅CPU比较（不渲染）：

```powershell
python tools/color-round11/diagnostic/compare_gray_scale_taps.py <home完整localcapture> <company完整localcapture> <新comparison.json>
```

先比较实际uploadedmatrix/LUT/exposure bytes（CPUpreparation/窄化），再input→tc/b（同一dispatch两个output）→upsample→exposure。返回类别是“actual常数已不同/相同常数下某首个输出不同/尚无差异”，没有跨设备PASS阈值，也不自动归因GPU或要求修复。若公司完整localLUT未迁移，先对hash和摘要，不能宣称逐字节内容全部对比已完成。

## 本次离线指标复现（GPU0）

```powershell
python tools/color-round11/diagnostic/analyze_gray_scale_existing.py <新offline-analysis.json>
python tools/color-round11/diagnostic/analyze_gray_scale_taps.py <company完整localcapture> <新company-analysis.json>
python tools/color-round11/diagnostic/verify_gray_scale_tap.py <company完整localcapture> --role company
```

相对差=abs(home-company)/max(abs(home),abs(company))，任一为0时不定义；same-sign binary32 orderedbits ULP，跨符号不当同级ULP。nearzero1e-6只是描述分组，不接受阈值。原first(2,0,R)和max(32,allrows,G)固定，不扩大样本。

许可和产品边界：不改math/profile/curve/clamp/FMA/driver/threshold、不重开BT、无新stock/C1/锐化candidate。研究输出不等于产品入口或GPL/逐资源分发许可。SharedIncrementEnvelopeUnsharpV1 defaultoff/NOT_ADMITTED，私人A/B偏好NOT_RUN。
