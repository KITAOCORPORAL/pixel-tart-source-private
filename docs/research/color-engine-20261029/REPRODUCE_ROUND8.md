# 第八批复现：最小可执行流式原型（不是产品注册）

基线`078a77f491d60c373989e15e8ea48c5ae5d4182c`。先fetch并读AGENTS/POLICY、COMPANY_ROUND8_REPORT、STREAMING_IMPLEMENTATION_ROUND8、STREAMING_BACKEND_CONTRACT_V1。第七批原publicABI full预算阻点仍保留；本版新入口已执行fullnative，但首轮组合host预算失败，不能预填全PASS。

本机本轮root `E:/Kitao-Research/color-engine-round8-20261030`，旧engine/toolchain root `D:/AI AGENT/tmp/color-engine-company-20261030`。外部源码/资源/DLL/SPIRV/RAW/像素都不进研究Git。旧root/case不能覆盖；需要复跑请新建相同结构独立研究根并保留新hash，不暗改旧报告。

## 1. 生成外部版本

```powershell
$engine='D:/AI AGENT/tmp/color-engine-company-20261030'
$root='E:/Kitao-Research/color-engine-round8-20261030'
git -C "$engine/source2" worktree add --detach <新external-stream-source> c1447c6ab618fa4b1cb11fd2fe12826855007059
python tools/spektralab-research/apply_stream_v1.py <新external-stream-source> "$engine/resources-patch"
```

generator拒绝wrongcommit/dirty/product tree，compiled严格PortraA99resolved与旧resources集合。8个文件（含API-SPEC研究增补）与本轮source-manifest/source-reproduction应核对。保留原source2、source-patch、allocation树；不改原profile/shader。新库只新增private research symbol，不调用spk_open，不注册产品服务。

## 2. 构建（隔离原固定工具）

```powershell
$cmake="$engine/pytools/cmake/data/bin/cmake.exe"
$tool="$engine/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin"
& $cmake -S <stream-source> -B <new-stream-build> -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_CXX_COMPILER=$tool/clang++.exe" "-DCMAKE_C_COMPILER=$tool/clang.exe" "-DCMAKE_MAKE_PROGRAM=$engine/pytools/bin/ninja.exe" -DBUILD_TESTING=ON -DSPEKTRALAB_REQUIRE_VULKAN=ON -DSPEKTRALAB_BUILD_TEST_DLL=ON -DSPEKTRALAB_BUILD_NATIVE_RAW=OFF -DSPEKTRALAB_BUILD_DESKTOP=OFF "-DSPEKTRALAB_VULKAN_HEADERS=$engine/Vulkan-Headers/include" "-DSPEKTRALAB_GLSLANG=$engine/glslang/bin/glslang.exe"
& $cmake --build <new-stream-build> --target spektralab_engine_shared --parallel 4
# 同第四批，复制该固定toolchain运行DLL到build/engine；不加系统PATH/产品目录
```

Windows自带bcrypt链接用于SHA256核验。初次头include顺序错误的失败build保留；最终5warning具体见build-summary，不声称零warning。固定本机库SHA `be1b408f5007dce56c928dbb7cef8a790376325a099b9679c747cad8c8fd0bb7`；换构建新hash进入request后由DLL自核验，不拿旧hash冒充相同二进制。

## 3. 小whole/stream门禁

使用第七批已有`allocation-measurements-bound`三份完整场景NPY及对应prepared manifests，whole-rgba16.bin是原固定引擎oracle，不重建桥、曲线或BT研究。原`resources-patch`必须完整集合hash相同。

```powershell
$env:PYTHONPATH="$engine/python-tools"
$env:PYTHONDONTWRITEBYTECODE='1'
python tools/spektralab-research/stream_exact_gate.py $root $engine --tag=-新标签
```

调用固定`$root/build-stream-v1/engine/spektrafilm_engine.dll`。rows1/17/37/113（10case）、首尾和不整除行、默认unsharp、完整frame尺度，0channel差才过；失败立即停止，不放宽阈值。consumer分块1/7，另controls补3/11。

单项例：

```powershell
python tools/spektralab-research/stream_case.py <prepared.npy> <全新输出目录> --manifest <manifest.json> --manifest-sha <完整SHA> --library <stream.dll> --resources "$engine/resources-patch" --rows 37 --consumer-rows 7 --oracle <original-whole-rgba16.bin>
```

caller先NPY/length/hash/finite准备身份，持readlock；native再source/domain/route/fullresolved/backend-self/resourcecollection哈希核验，读回输入pixelSHA闭环。回调错误不跨C，native结束join后释放buffer/DLL。GPU/staging硬cap与hostprocessguard独立，host总进程不由nativevector计数保证。

## 4. 实际失败、重复、height控制

```powershell
python tools/spektralab-research/stream_controls.py $root $engine
python tools/spektralab-research/stream_height_proof.py
python tools/spektralab-research/stream_concurrency_test.py
```

辅助脚本的本轮固定路径/目录名用于精确复现；旧目录存在会拒绝，换root/标签须显式freeze脚本hash，不覆写。controls包含readfail/consumerfail/cancel/unknown/effect/route/hash/budget10失败，repeat2，synthetic4，synthetic标明非新自然样本。负/>1/近零与全frame source身份不混称。

height proof321×430 vs原215，同width/rows37的device/staging/nativehostvectorpeak完全相同，新增whole小oracle exact。busy/cancel测试第二call−30、第一−7、join/live0，不新renderfull。

本轮rows4096在16MiBdevice+1MiBstaging硬预算下实际26，wholeexact。首个rows策略把staging替换旧+新余量算少，硬guard拒绝后改48bytes/pixel staging约束再测，保留失败日志。不要把1MiB staging测试“成功自动减rows”错误当引擎故障。

## 5. 20MP单次执行门槛与记录

本轮已执行一次，不允许对旧root重复：

```powershell
python tools/spektralab-research/stream_full_once.py
```

脚本固定本root，检查10smalloracle＋nativecontrols才投一次，同第七批5496×3670A完整输入。requested127、consumer7、device256MiB、staging64MiB、host512MiB；device还受当前free减256MiB/研究2GiBcap限制，不关闭其它程序。

完整hash `1c4a97a126b420fb24561df5cfb03bfc1b3d7648a5ee77c3d13102c48c2a7b8a`。没有20MPwholeoracle，也没有第二次20MPnative。初callerpostnativeI/O RSS超hostcap：本轮旧report另存，审计report置success=false并把像素命名.initial-host-overbudget。不要恢复默认文件名/把native success当全部门禁success。

修正后I/O验证不新渲染：

```powershell
python tools/spektralab-research/stream_saved_reopen.py <本full输出目录> <新reopen报告.json>
python tools/spektralab-research/stream_saved_rewrite.py <本full输出目录> <新rowIO目录>
```

脚本识别AUDITED_STATUS并读取明确host-overbudget原像素。read-only新进程36.23MB、writer/read新进程38.49MB均fullhash exact，最大单行43,968bytes。**它们不证明修正后的native+I/O整full进程已按512MiB通过**，该门禁仍NOT_RERUN。下一会话若要补，需明确只一个新full任务，使用修正caller及新目录/真实预算，不扩stock/route。

## 6. 已执行证据校验

```powershell
python tools/spektralab-research/verify_round8.py docs/research/color-engine-20261029/company-round8
```

这是对报告断言，不当设备运行。保留原失败/overshoot证据、allocator/source/DLLhash，图像仍授权本地。产品UI/模型/桥/default/PV4/5/6/MatrixV2不改、seed不支持、D80014/家庭GPU/肤色高光缺口保留，BT/曲线/WB/完整桥不重跑。许可和技术分开记录，LightTable条款不能替代Spektra代码/资源授权。
