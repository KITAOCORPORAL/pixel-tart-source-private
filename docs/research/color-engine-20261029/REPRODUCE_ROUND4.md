# 第四批复现（公司隔离研究，2026-10-30）

## 接续与素材

从`ce292f2a067e77d1d48fcb5e90fb18b9d642466f`的独立工作树开始。fetch后先读增量、AGENTS、POLICY和本报告。没有明确授权文件则只做契约/CPU/合成/GPU小图；不要扫描无关目录。不要覆盖已有输出。

本机研究根`D:/AI AGENT/tmp/color-engine-company-20261030`；Git不携带外部源码/资源/DLL/大float/原片。根外的旧树不动，命令PATH修改仅当前进程。

## 矩阵/生命周期/重开

```powershell
$dotnet='D:/AI AGENT/.dotnet/dotnet.exe'
$env:RESEARCH_DOTNET=$dotnet
$env:PYTHONDONTWRITEBYTECODE='1'
& $dotnet build tools/color-research-decoder/ResearchDecoder.csproj
python -O tools/color-research-decoder/test_hardened_contract.py -v
python -O tools/color-research-decoder/test_recipe_rejection.py -v
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll lifecycle-test
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll observation-test
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll fixture <新合成目录>
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll reopen <合成目录> <新重开目录>
```

合成版本明确8×4，负/>1+完整域/finite/hash真实调用产品preview/export，不是D800通过证据。V2严格锁source和依赖bytes，改变工具后旧V2必须用原工具读，不能重写其identity冒充兼容。

恢复第二份已授权D80014且精确SHA匹配后，须实际运行（本公司本批NOT_RUN）：

```powershell
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll <授权D80014.NEF> 593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A <新matrix目录> matrix
& $dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll reopen <matrix目录> <新重开目录>
```

必须保存report/recipe/hash及真实预览/TIFF结果，不把旧stage probe当新候选。V2如果新的参数/矩阵观测被拒绝，先记录实际差异，不放宽guard。WB余量不在本批调参/收益复跑。

## Spektra固定源码与工具

外部原始checkout固定`JamesQiu2005/SpektraLab` commit `c1447c6ab618fa4b1cb11fd2fe12826855007059`、tree `f2d76eec2f35583f42783fd0b0bc723b69915b70`。读其AGENTS和Windows文档，原始source2保持clean。

工具沿REPRODUCE_ROUND3完整包hash：LLVM-MinGW20250402 ucrt-x86_64、glslang16.6.0，CMake3.31.10/Ninja1.11.1.4只装研究根；Vulkan-Headers固定b5c8f996...。勿改产品PATH/驱动。原版执行：

```powershell
$root='D:/AI AGENT/tmp/color-engine-company-20261030'
& tools/spektralab-research/build-isolated.ps1 -ResearchRoot $root
python tools/spektralab-research/prepare_resources.py "$root/source2" "$root/build-vulkan" <新原版overlay>
```

公司这组原版11/11是实测，不能预填家庭失败。别的机器以实际返回记录。原脚本CTest非零会停止，这是保留失败的正确行为。

## 生成隔离补丁/trace/断点instrument

在source2同仓库下另建三个detached worktree；不要给已dirty的checkout应用。`apply_company_patch.py`会拒绝dirty、错commit或产品checkout；工具会运行外部gen-project（本次没有Xcode工程diff）。

```powershell
git -C "$root/source2" worktree add --detach "$root/source-patch-new" c1447c6ab618fa4b1cb11fd2fe12826855007059
python tools/spektralab-research/apply_company_patch.py "$root/source-patch-new"
# 如需记录断点float值和实际branch：另一个clean固定checkout
python tools/spektralab-research/apply_company_patch.py <clean-instrument-checkout> --instrument
# 如需定位中性下降：再一个clean固定checkout
python tools/spektralab-research/apply_company_patch.py <clean-trace-checkout> --trace
```

新build使用与原版相同配置，只改-S/-B；完整实际flags在company-round4/patch-configure.log/原始build日志。核心配置：

```powershell
$cmake="$root/pytools/cmake/data/bin/cmake.exe"
$tool="$root/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin"
& $cmake -S <patch-source> -B <new-build> -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_CXX_COMPILER=$tool/clang++.exe" "-DCMAKE_C_COMPILER=$tool/clang.exe" "-DCMAKE_MAKE_PROGRAM=$root/pytools/bin/ninja.exe" -DBUILD_TESTING=ON -DSPEKTRALAB_REQUIRE_VULKAN=ON -DSPEKTRALAB_BUILD_TEST_DLL=ON -DSPEKTRALAB_BUILD_NATIVE_RAW=OFF -DSPEKTRALAB_BUILD_DESKTOP=OFF "-DSPEKTRALAB_VULKAN_HEADERS=$root/Vulkan-Headers/include" "-DSPEKTRALAB_GLSLANG=$root/glslang/bin/glslang.exe"
& $cmake --build <new-build> --parallel 4
# 像build-isolated一样复制此tool目录运行所需DLL到build/engine，不放产品目录
& "$root/pytools/cmake/data/bin/ctest.exe" --test-dir <new-build> --output-on-failure
```

## case114/index13红→绿（不改阈值）

`bt_probe.cpp`目标在patch/instrument checkout由generator添加。probe对mode3读实际output，mode6/7只在instrument shader读bp/branch；不要拿未instrument时6/7的identity输出当断点。

1. 保留原版资源与其SPIRV hash。用patch build的probe指向原版资源，记录原GPU±6ULP输出。
2. instrument shader分别用默认flags、加`-Os`编译，probe读bp/branch；输出13位置含`0x3da66554`。两种编译命令固定`glslang -V --target-env vulkan1.0 [ -Os ] -o <单独spv> <source>`。
3. 在**单独测试资源副本**中只替换decode shader为原版`-Os`输出，运行原pointwise/CTest：公司重现10/11、index13差5.51e-5。
4. 同副本替换成补丁`-Os`输出，pointwise/CTest为11/11。完成后恢复原SPIRV并hash校验。本次没有修改原build/原资源。
5. 不用-Os只是“证明另一条优化路线可复现”，不谎称它就是家庭编译参数；家庭最终driver机器码未知。

原始/补丁各source/SPIRV和命令见bt-shader-compilation.json；补丁源码生成后hash应EE3369...44FFF。本地补丁版本仍GPL隔离研究，不迁入产品。

## 两参考数值

- 下载Colour commit0c6f868...对应源，隔离`pip --target <references>/colour-package colour-science==0.4.6`；原模块hash须和完整commit下载相符。
- FFmpeg commitb08d796... `libavutil/csp.c`只保存在references本地；sha应302474...F3CC7。
- `python tools/spektralab-research/bt_references.py <references> <clang.exe> <新report.json>`。脚本只本地提取FFmpeg原函数编译执行，源码不进产品/Git。保留两个项目常数/负数策略不同，不把最终差当同域误差。

## 公司确定性矩阵、重开、TIFF和ABI

隔离安装`tifffile==2026.10.15`到research/python-tools，当前进程PYTHONPATH指向它；NumPy/Pillow版本固定并记录。对original/patch各运行：

```powershell
python tools/spektralab-research/company_matrix.py <固定source> <build> <overlay> <新输出>
python tools/spektralab-research/company_abi_probes.py <固定source> <build> <overlay> <新report.json>
python <固定source>/engine/tools/windows_lifecycle.py --library <patch.dll> --resources <patch-overlay> --reference-library <original.dll> --reference-resources <original-overlay> --require-stripe-parity --output <新report.json>
python <固定source>/engine/tools/windows_stripe_regression.py --library <patch.dll> --resources <patch-overlay> --reference-library <original.dll> --reference-resources <original-overlay> --cases no_halation no_couplers --rows 1 7 17 --output <新report.json>
```

9组合为合成图，不能变成自然验证保留组/产品UI验收。grain/glare关闭，任意seed未支持。耗时只是公司实际调用，不推广为基准。

trace build使用原版shader与同overlay，`company_matrix.py ... --trace <新tap根>`；为每组合设SPK_RESEARCH_TRACE，本次已验证被动读回不改像素。`summarize_neutral.py <tap根> <对应trace矩阵输出根> <新json> --reference <原矩阵输出根>`只提交小数值/hash而非大float。具体原因仍需继续追curve/profile，禁止为了单调而改资源或门槛。
