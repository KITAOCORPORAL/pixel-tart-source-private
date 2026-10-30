# Public synthetic cross-device diagnostic V1

本包不含私人照片、原RAW、外部engine资源或DLL。仅固定数学生成的NPY、家庭输出小型RGBA16/float阶段文件及自编驱动。不是自然场景、色卡或物理胶片拟合证据。原 `spk_stream_research_v1` Canon源白名单保持不变；诊断使用独立 `spk_stream_synthetic_diag_v1`，只承认本包3个输入pixel hash与合成source身份。

## 用途与门槛（执行前规定）
- 同一设备 whole/stream：RGBA16每通道 **0差**，rows1/7/17/64，不得放宽。
- 跨设备：只报告首差、变化通道数、max/mean/分位数以及首个分歧阶段；**没有事后设定的PASS色差阈值**。比较前要求输入manifest、完整resolved和逐资源/SPIRV完全一致。
- 12个whole阶段和对应stream条带阶段是被动readback（float32），已在家庭实测不改变最终RGBA16。阶段串联为曝光RGB→负片CMY密度→扫描XYZ中间量→linear sRGB→encoded sRGB，再最终RGBA16 clamp/round。阶段是本固定engine内域，不能当传感器float或真实色准。
- BT code vector另通过 `spk_synthetic_bt_probe_v1` 直接调用现有mode3 decode shader，输出input/output bit values。PortraA仍linear ProPhoto D50不做BT decode；不混淆两个处理域。

## 固定材料
`fixtures/fixtures.json`声明形状、<f4 C-order和来源；`generate_inputs.py <全新目录>`能逐bytes再生。灰阶、近零、负/>1、原色/饱和色和小边缘图共3输入，最大37×21。`resolved-portraA.json`固定原99字段，`expected-resources.json`要求全部实际资源/SPIRV哈希相同，`expected-stream-source.json`允许自然negativeStore root literal变化但其他stream算法字节锁定。

外部engine固定c1447c6...且实际执行代码是已固定stream生成版本，不仅检查commit。`generate_backend.py`在新clean checkout中复制并校验8源文件，添加诊断兄弟入口和必要被动采样；外部GPL生成源码只留本地，不复制进产品。自然白名单保留且家庭实际调用确认合成身份被原入口拒绝。

## 公司设备执行（当前NOT_RUN）
要求公司固定工具、资源仍可核验，指定一个全新诊断研究根。不要将家庭绝对路径当公司存在。
```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONPATH='<明确隔离Python依赖根>'
python tools/cross-device-diagnostic/generate_inputs.py <新fixture目录>
& tools/cross-device-diagnostic/build_package.ps1 `
 -FrozenStreamSource <公司第八批source-stream-v1> `
 -EngineRepository <固定外部git仓库> -ResearchRoot <全新诊断root> `
 -Fixtures <新fixture目录> -CMake <固定cmake.exe> `
 -CompilerDirectory <固定MinGW bin> -Ninja <固定ninja.exe> `
 -VulkanHeaders <固定include> -Glslang <固定glslang.exe>
python tools/cross-device-diagnostic/run_diagnostic.py `
 --checkout <诊断root/source-diagnostic> `
 --library <诊断root/build-diagnostic/engine/spektrafilm_engine.dll> `
 --trace-library <诊断root/build-trace/engine/spektrafilm_engine.dll> `
 --resources <已核对的公司resources-patch> `
 --expected-resources tools/cross-device-diagnostic/expected-resources.json `
 --fixtures <新fixture目录> --resolved tools/cross-device-diagnostic/resolved-portraA.json `
 --output <新公司小结果目录>
```
所有DLL旁需相应runtime DLL（build脚本复制固定编译器runtime），Vulkan从系统加载，不更改驱动/PATH。运行入口核对资源身份；构建报告保留编译器二进制/hash、flags和生成源码身份。`inventory_run.py <诊断root> <run目录> <新provenance.json>`同时支持家庭round10和build脚本布局，记录cache/逐对象编译命令/工具及runtime哈希；这是post-build/post-run清单，不冒充调用前freeze。公司应实际运行后新建本机provenance，不复制家庭字段。

诊断输出和临时negative文件必须处于被复制stream源码编译固化的授权root之下（公司E:/Kitao-Research、家庭N:/pixart/ColorResearchHome-20261030），不能随意指向私人源目录；换root须新生成并记录hash。不要解除原自然入口守卫。`run_diagnostic.py`同样要求全新输出目录。`inventory_run.py`所录工具exe哈希包含Clang驱动可执行文件；公司尚缺完整逐对象命令与全部工具chain库身份时仍保留混杂，不以驱动exe相同宣称所有codegen条件相同。

公司将输出的**合成小数据目录**与provenance合法带回，不需要私人照片：
```powershell
python tools/cross-device-diagnostic/verify_run.py <fixture目录> <公司run目录>
python tools/cross-device-diagnostic/compare_packages.py `
 docs/research/color-engine-20261029/home-round10/diagnostic-home <公司run目录> <全新comparison.json>
```
比较器逐行，U16精确histogram高阶分位数；小型float stage用完整有限值分位数。先校验各结果/阶段hash与shape，非有限/缺失/资源错配拒绝而非默默PASS。浮点值不同不等于误差来源已证明；从第一个whole阶段分歧继续定位，再看stream相同执行序列，不直接调曲线/profile/精度。

## 家庭已执行、公司待执行
家庭RTX5060Ti/610.88：3case×4rows exact；每case12whole+36stream阶段读回，trace final0差；合成身份原入口拒绝；BT直读通过。家庭small输出完整封存在 `docs/research/color-engine-20261029/home-round10/diagnostic-home`，约1.06MB。`company-diagnostic-NOT_RUN.json`明确公司GPU尚未运行。self-comparison只测工具，不当作跨设备结果。

许可证：代码/资源分发许可与技术验收分开；没有将GPL backend、CC BY-SA profile、LUT或厂家数据放入本包。自编generator是隔离研究工具，不据此豁免完整产品的组合/资源分发义务。旧产品/默认recipe均不变。
