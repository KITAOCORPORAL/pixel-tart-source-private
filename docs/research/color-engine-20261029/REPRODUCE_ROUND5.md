# 第五批有界复现（公司独立研究）

接续基线`be2c33b6b23986b1275ad29244e0cab3cc41412b`。先fetch增量并读AGENTS、POLICY、第四/五批报告与BACKEND_CONTRACT_V2。产品src/UI/任务桥/注册不改，D80014不重新扫描，WB余量不调参。

所有新输出目录必须不存在；工具不会覆盖原证据。外部源码/资源/像素/二进制留`D:/AI AGENT/tmp/color-engine-company-20261030/round5`或明确的新隔离目录，数值报告可移入研究docs。不要把clone得到的profile或源码放入产品包。

## A. 无GPU两点最小复现

现有Python3.13.13、NumPy2.4.6、SciPy1.18.1：

```powershell
python tools/spektralab-research/curve_minimal_repro.py docs/research/color-engine-20261029/company-round5/curve-minimal-fixture.json <新json文件>
```

fixture只含两个stock R通道相邻原采样点和旧实际log输入；NumPy与SciPy(k1)均应得负delta。不能加平滑/clamp或改阈值让它“通过单调性”。该命令证明采样表来源，不是完整自然图/物理胶片校准。

## B. 实际原engine曲线节点+旧tap

保留原固定`source2` commit c1447c6、原build-vulkan、resources-original，以及上一批`trace2-kodak_portra_400`/`trace2-kodak_ektar_100`。脚本会校验旧tap hash、source commit/clean及runtime profile与固定源码相同，不允许改hash绕过。

小C++驱动**只调用**现有engine的Profile loader、build_interp_tables、print_curves_morph诊断及实际spk_curves shader，链接原静态库，不复制引擎实现：

```powershell
$r='D:/AI AGENT/tmp/color-engine-company-20261030'
$clang="$r/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin/clang++.exe"
& $clang -O3 -DNDEBUG -std=c++20 "-I$r/source2/engine/src" "-I$r/source2/engine/tests" tools/spektralab-research/curve_neighbour_probe.cpp "$r/build-vulkan/engine/libspektralab_engine.a" "$r/build-vulkan/engine/libspektralab_core.a" "$r/build-vulkan/engine/libspk_volk.a" -lkernel32 -luser32 -lgdi32 -lwinspool -lshell32 -lole32 -loleaut32 -luuid -lcomdlg32 -ladvapi32 -o <隔离probe.exe>
# 把同toolchain运行依赖DLL复制到probe目录，像第四批build一样；不写系统PATH
python tools/spektralab-research/curve_root_cause.py $r <probe.exe> <新本地输出目录>
```

每stock只约531/534个RGB查询，local native report包含表/hash与小输入，不重新生成旧巨型trace。输出report.json可提交，queries.f32/native完整小表留隔离根。本轮未改曲线或模型，CDF只诊断“不同数据”，不是新候选。

## C. 固定参考与来源

- NumPy2.4.6 tag对应commit `b832a09cf2a169c833dd2371e7c07aa00b293242`：`numpy.interp`，`numpy/_core/src/multiarray/compiled_base.c:arr_interp`。
- SciPy1.18.1 tag对应commit `e4e854eaa8f18d807cd3496028e257e36caa93cc`：`make_interp_spline(k=1)`/BSpline评估，CDF用`scipy.special.ndtr`。
- `curve_root_cause.py`实际调用已安装参考。确认`numpy.version.git_revision`/`scipy.version.git_revision`，下载对应源码读取实现；Python模块CRLF/LF差异分别保存bytes与newline-normalized hash，不能把原始hash不同直接说完全相同。
- 可选来源追溯：隔离clone `andreavolpato/spektrafilm`，detach到`3bb2c2d2801ff68b92019cf1dbcbb133d60832bc`；两个profile应完整bytes相同，last change为`ad5c8d2b5bbf295f6d3912091f3b137b0aef805a`。读取`density_curves.py`/`fast_interp.py`只算源码证据；本轮没有Numba，未执行完整Python源引擎。

不需要下载新照片、购买色卡或重新扫描D800目录。

## D. BT完整引擎/保存/新进程重开

沿用原/补丁四个冻结decode shader（hash见第四批bt-shader-compilation.json）；原DLL/resources与patch均保留。脚本把resources复制到新输出root，Os条件只替换其中decode shader，**不覆盖旧原版资源**。

当前进程`PYTHONPATH`加研究根python-tools（固定tifffile2026.10.15），另有Pillow12.2.0/NumPy2.4.6：

```powershell
$env:PYTHONPATH="$r/python-tools"
$env:PYTHONDONTWRITEBYTECODE='1'
python tools/spektralab-research/bt_complete_path.py $r <新BT输出目录>
python tools/spektralab-research/bt_complete_path.py $r <新BT补充输出目录> --supplement
```

前者32case（两个BT域×原/补丁普通与Os×两个stock×断点/渐变），后者32case（相同断点高度1、饱和/负/>1mixed RGB）。每case写input.npy、raw RGBA16、带sRGB ICC TIFF16、完整resolved recipe；脚本启动新Python进程，验证源input/DLL/resources hash及尺寸/finite，再open/render并对比输出，重新保存/回读TIFF16。reported elapsed含新进程/资源hash，不当性能基准。

完整JSON 99字段并不暴露所有内部默认；特别scanner unsharp{sigma0.7pixel,amount0.7}仍开，固定源hash和实际传播trace补足。关闭grain/glare不等于关闭全部空间节点。兼容BT断点与规范/连续曲线不可混写。

## E. 证明实际decode与后续扩散

创建新的外部clean detached checkout，不编辑原source2：

```powershell
git -C "$r/source2" worktree add --detach <新decode-trace-source> c1447c6ab618fa4b1cb11fd2fe12826855007059
python tools/spektralab-research/apply_decode_tap.py <新decode-trace-source>
```

按第四批同CMake flags构建shared engine到`$r/round5/build-decode-trace`（本轮工具默认定位），其中nativeRaw/Desktop OFF；只添加被动node_decode_input回读。新trace源hash应`ae545ca76c6ab3c9a8a547b1136111268ac50627b66e235697a6a3bae4af9712`。复制同运行DLL依赖。可在新根布置相同目录结构，不改产品配置。

```powershell
python tools/spektralab-research/bt_decode_tap.py $r <BT完整输出> <新decode-tap输出>
python tools/spektralab-research/bt_decode_tap.py $r <BT完整输出> <新gradient-tap输出> gradient
# 利用上一批被动stage build-trace，不重新输出大图，只跟踪21或257宽小图
python tools/spektralab-research/bt_spatial_trace.py $r <BT完整输出> <新传播trace输出>
python tools/spektralab-research/bt_spatial_trace.py $r <BT完整输出> <新gradient传播输出> gradient
```

被动DLL输出须与无tap完整render hash相同。decode只bits0x3da66554所在列变，scanner finish后向邻域扩散；最后U16变化范围和float范围分别报告。

## F. 最终有界验证

```powershell
python tools/spektralab-research/verify_round5.py docs/research/color-engine-20261029/company-round5
```

这是对**已执行证据**的断言，不是再次跑设备。检查原profile负斜率、两参考、无过冲、旧trace一致、CDF区别、64完整render/reopen结果及decode/空间传播。公司原/补丁pointwise各单项回归通过，未重跑整轮V2/全PV4/5/6。

仍保持NOT_RUN：D80014缺源全链/真实取消、家庭RTX5060Ti补丁、Metal全图oracle、自然验证集、产品R5.5 UI/桥接验收、任何seed扩展和广泛空间尺寸规则。LightTable非商业许可与Spektra GPL/资源许可不互相替代。
