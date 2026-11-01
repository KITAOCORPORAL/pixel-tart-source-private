# 第十二批只读因果分析工具

限定：既有NEX5R和R6的代理/保存结果/阶段数据。工具不调用原engine DLL，不导入GPU包，不解码RAW，不写生产src。

- analyze_existing.py：固定选择的零值来源、邻域解析、R6固定ROI逐阶段统计。CPU解析近似必须与真实保存阶段区分，不能用其误差声称色准。
- analyze_paper.py：读取local-only CPU setup数据，分析纸张/扫描链。只限固定PortraA99resolved。
- audit_prior_intermediates.py：从已经保存的生产master重算旧端点足迹及R6显示proxy；不是RAW重解码。
- visual_correspondence.py、local_domain_details.py、endpoint_details.py：自然坐标/邻域/CSV/图像写入明确本地研究根，Git只汇总hash。
- test_causal_analysis.py：15项只读分析语义/已保存证据测试，不是重跑原验收。
- build_cpu_probe.py：仅编译调用固定外部core的CPU setup辅助；源码在本地round12-cause-v2/cpu_setup_probe.cpp（hash见cpu-setup-identity），不链接GPU/pipeline，不载入照片。没有该外部本地源码就明确缺资料，不自动下载/捏造。脚本拒绝覆盖既有exe或identity。

## 复核现有成果（不重生成）
环境沿用N:/pixart/ColorResearchHome-20261030/deps/python-frozen，设PYTHONDONTWRITEBYTECODE=1。

	python -B tools/color-round12/test_causal_analysis.py -v

固定既有数据位于round11/natural-inputs-v1、natural-coverage-run1、natural-stage-finite-v1；结果私有路径round12-cause-v2。首次blob key错误的round12-cause-v1保留。不要覆盖已存在结果，也不要运行GPU/redecode来代替缺失数据。

本批没有完成两个成熟photo实现的固定域对照；候选方向不是已批准算法。原profile/GPL代码/资源不进Git，CPU helper只本地；自编解析公式是原因核对工具，不是复制或注册产品实现。
