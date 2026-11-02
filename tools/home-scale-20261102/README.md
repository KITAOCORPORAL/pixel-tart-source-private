# 家庭20261102：单次gray被动观测接续

唯一GPU调用已经完成，不重新运行。使用既有tools/color-round11/diagnostic/generate_scale_substage_tap.py、run_gray_scale_tap.py、verify_gray_scale_tap.py；没有新增诊断框架。runner唯一变化是NVML失败时真实WMI身份后备，保留原失败。

完整授权输出：N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once。公司完整四个restrictedsetup数组未迁移；公司Git只subset，禁止拿它运行fullpairedcomparator后声称通过。

## 只读复核
固定依赖目录N:/pixart/ColorResearchHome-20261030/deps/python-frozen；PYTHONDONTWRITEBYTECODE=1。

    python -B tools/color-round11/diagnostic/verify_gray_scale_tap.py <完整homegray目录> --role home
    python -B tools/home-scale-20261102/test_saved_gray.py -v

比较可重读（全新输出名）：

    python -B tools/home-scale-20261102/compare_available.py <完整homegray目录> <新CPUcomparison.json>
    python -B tools/home-scale-20261102/analyze_propagation.py <完整homegray目录> <新CPUpropagation.json>

这两个只读脚本不import引擎；先完整homevalidate，再校验公司publicmanifest和私有摘要绑定，区分直接bytes与sealedhash-only，缺完整数组的数值误差保持null。compile/source freeze脚本为本次准备材料，不要求重建/重跑：prepare_build.py只构建，freeze_before.py在GPU前记录字节和身份。

11项CPU控制含错role/篡改拒绝、publicsubset不完整、真实WMI、窄化上传device、tc_b/EV0边界。不新建fixture或增GPU。所有生成源/DLL/runtime/LUT/sensitivity留外部本地；Gitpublicsubset仅与公司同17小文件范围。

完整复现交接依旧REPRODUCE_SCALE_LOCALIZATION_20261102.md，本次已执行实例和停止结论在HOME_FILM_SCALE_LOCALIZATION_20261102.md。后续更细指令或控制变量必须另授权，本任务完成后停止。
