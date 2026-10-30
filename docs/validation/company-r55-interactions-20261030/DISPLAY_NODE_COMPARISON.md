# R55BoardResponse2展示对照记录
- 节点：灯板材质显示；非照片像素节点。输入encoded SDR RGB8 D65板材基色、亮度10–160%、flare0–1，输出同域板材颜色。无照片alpha/ICC/WB/RAW转换，PV4/PV5/PV6数学不变。
- 两个固定来源：Windows cb8f4c89d845634688627f1c01225cb5d851248f / Core/Optics/LightResponse.cs；原版8e1868df40e69e6b1a08e207d7ab05cf463532ab / Sources/LightTableCore/LightResponse.swift。文件hash与函数见OPTICAL_REFERENCE_COMPARISON。MIT正文在发布包及源码；Swift依赖LightTableEDR未引入，Windows来源与原版同源，不称独立科学oracle。
- 复用PanelWhite/时间曲线，WPF显示适配R5.5指定两基色；>100%剩余编码余量映射显式新展示版本。无CSS滤镜替代照片数学，不增加新空间像素算法。
- 实际采样151个brightness、201个时间点；对固定源码的标量公式最大误差2.23e-16/0。可复跑compare_optical.py，需使用已登记参考目录，不自动下载或执行附档代码。原始OPTICAL_SAMPLES为实际最终发布程序集输出。
- 时间差异：Windows2s与Swift1.1s，保持Windows2s，不称相同物理模型。WPF轻辉光只是DropShadow展示，真实镜片折射散射未做。
- 原图/比较/重新比较/两次真实剧照抓取/完整源输出及编码PNG前后相同；LAMP_ISOLATION/原始TRX留证。没有平均差掩盖个别像素变化。
- 范围：synthetic已知像素与现有授权性能源；这是显示材质，不用16bit/float/RAW误差冒充光学测量，不声称相机或实测胶片标定。CalibrationEligible=false。
- 准入：EXPLICIT_DISPLAY_ONLY_DIAGNOSTIC。新照片色彩/光学变换仍必须按AGENTS/POLICY取独立成熟实现、域与自然输入对照，不因本显示适配自动放行。
