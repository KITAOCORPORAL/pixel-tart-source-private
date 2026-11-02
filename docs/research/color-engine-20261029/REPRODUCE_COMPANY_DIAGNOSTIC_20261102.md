# 公司公开合成诊断：精确复现入口（2026-11-02，Asia/Shanghai）

已审查家庭base `93b553d1c22937a2870743a40adc43d6df8b526d`。使用`tools/color-round11/diagnostic/run_company.ps1`，不要照抄旧cross-device README的历史执行命令。以下为本轮**实际公司路径记录**，本轮已执行一次，不能向这些现有Output重跑/覆盖；如另获授权复跑，必须新Output并重新冻结身份，失败保留且不自动重试。

```powershell
$env:PYTHONUTF8='1'             # current process only
$env:PYTHONDONTWRITEBYTECODE='1'
& tools/color-round11/diagnostic/run_company.ps1 `
 -Checkout 'E:/Kitao-Research/color-engine-company-diagnostic-20261102-build/source-diagnostic' `
 -Library 'E:/Kitao-Research/color-engine-company-diagnostic-20261102-build/build-diagnostic/engine/spektrafilm_engine.dll' `
 -TraceLibrary 'E:/Kitao-Research/color-engine-company-diagnostic-20261102-build/build-trace/engine/spektrafilm_engine.dll' `
 -Resources 'D:/AI AGENT/tmp/color-engine-company-20261030/resources-patch' `
 -Output 'E:/Kitao-Research/color-engine-company-diagnostic-20261102-run' `
 -Python 'C:/Program Files/Python313/python.exe'
```

运行前入口先collect_build_identity，覆盖逐source/tool/runtime/object命令/resource/SPIRV；失败即停止，没有GPU时也不能抄家庭收据填company。当前机器GTX1650/572.70/Vulkan1.4.303。固定源码c1447c6及8stream frozen hash/73resources34SPIRV匹配；没有可复用synthetic/trace库，按现有build流程在全新目录生成构建（详细actual配置/调用/日志/provenance在证据目录）。没有改generator/诊断工具。

构建实际参数（不是旧README复制的未核实例）：

```powershell
& tools/cross-device-diagnostic/build_package.ps1 `
 -FrozenStreamSource 'E:/Kitao-Research/color-engine-round8-20261030/source-stream-v1' `
 -EngineRepository 'D:/AI AGENT/tmp/color-engine-company-20261030/source2' `
 -ResearchRoot 'E:/Kitao-Research/color-engine-company-diagnostic-20261102-build' `
 -Fixtures '<本研究工作树>/tools/color-round11/diagnostic/fixtures' `
 -CMake 'D:/AI AGENT/tmp/color-engine-company-20261030/pytools/cmake/data/bin/cmake.exe' `
 -CompilerDirectory 'D:/AI AGENT/tmp/color-engine-company-20261030/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin' `
 -Ninja 'D:/AI AGENT/tmp/color-engine-company-20261030/pytools/bin/ninja.exe' `
 -VulkanHeaders 'D:/AI AGENT/tmp/color-engine-company-20261030/Vulkan-Headers/include' `
 -Glslang 'D:/AI AGENT/tmp/color-engine-company-20261030/glslang/bin/glslang.exe'
```

本轮read-only独立验证/比较（这些命令不渲染，可使用新比较输出文件）：

```powershell
python tools/color-round11/diagnostic/verify_run.py docs/research/color-engine-20261029/company-diagnostic-20261102/company-package --role company
python tools/color-round11/diagnostic/compare_packages.py `
 docs/research/color-engine-20261029/home-round11/diagnostic-home-v2 `
 docs/research/color-engine-20261029/company-diagnostic-20261102/company-package `
 <全新comparison.json> --left-role home --right-role company
```

同shared validation分别验证左右，而非信run的PASS。whole/rows1/7/17/64及tracedfinal同设备0差门槛不变；跨设备只输出完整变化数/首差/absmax/mean/quantiles/firststage，不自动制定可接受阈值。测试22+3通过，均只操作公开小文件/临时副本，无GPU。

第十三批candidate与人工偏好不在入口中执行，不改包络/曲线/旧unsharp；私有A/B/RAW不需要也不迁移。产品/UI/桥/default/PV4/5/6不改，driver/networkpersistent配置不改。外部engine资源和DLL/SPIRV不提交，3小input/output/阶段是既有公开合成包，可Git携带。
