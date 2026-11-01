# 第十三批：单一受约束锐化研究

默认不运行候选。先读取节点比较和HOME_ROUND13_UNSHARP_REPORT。旧PortraA/source/build/resource、产品与历史20MP不变。

本地独立根：N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1。references为固定GPL参考源；reference-build-v2为实际原函数harness；candidate-build-v3为唯一实际执行候选；candidate-synthetic/natural为冻结A/B；review-v1为私有匿名图/float/TIFF读回。不要覆盖已存在目录或重跑原任务。

## 只读复核
设PYTHONDONTWRITEBYTECODE=1，PYTHONPATH=N:/pixart/ColorResearchHome-20261030/deps/python-frozen：

    python -B tools/color-round13/test_frozen_ab.py -v

这是已有数据读回，不产生GPU/RAW调用。15项通过，原失败日志保留。

## 原始执行材料（均要求全新输出）
- build_reference_probes.py：读取固定外部源，提取原函数body和真实gauss，生成本地GPL harness。框架适配不改算法；仅明确分支。archive/original与body/exe hash在reference-files/build。
- run_reference_fixtures.py：9 Lab夹具×5配置。不能把Lab L*当linearRGB或完整应用效果。
- build_candidate.py：读取本地独立candidate_stage-v3.cpp/shared_increment_envelope_v1.comp，添加隔离kernel注册，其它旧源不写；编译原Blur加唯一候选和原output编码。helper和shader按POLICY只本地，hash见candidate-build-v3，不自动下载/构造缺失源。
- run_candidate_ab.py默认关闭，需要显式 --enable-candidate SharedIncrementEnvelopeUnsharpV1；仅existing完整ratio1 stage，先synthetic再natural，已存在输出拒绝；原运行早于CLI启用门禁硬化，executed-vs-final-runner.json保留字节差异，数学/参数未改也未重跑。

原执行参数等价于：

    python -B tools/color-round13/run_candidate_ab.py <独立研究根> synthetic --enable-candidate SharedIncrementEnvelopeUnsharpV1
    python -B tools/color-round13/run_candidate_ab.py <独立研究根> natural --enable-candidate SharedIncrementEnvelopeUnsharpV1

当前冻结目录已存在，因此上述命令应拒绝覆盖。复现实验需新根并显式记录新字节/资源身份，不覆盖旧报告冒充原实测。上游输入只读round11阶段，无RAW解码。

review_ab/numerical_addenda/compare_reference_outputs只处理保存数据；局部包络必须取全图上下文，裁切边缘的描述表不用于技术门槛。TIFF16读回不带产品ICC/项目验收承诺。

候选方向是局部约束，不是已摄影批准。阶跃/脉冲完全失去锐化增益，噪声仍放大。独立评价/用户偏好/其他尺度/产品预览保存导出/跨GPU/许可未完成。六图仍开发样本。
