# 托盘图片交付复现与门禁

在独立工作树D:/AI AGENT/kitao-planning-tray-images-20261014使用SDK D:/AI AGENT/.dotnet/dotnet.exe。

- 构建Core/WPF测试和tools/VerifiedTestRunner后，使用tools/planning-r7/verified_runner.py运行完整Core（--single-process）与WPF（逐类隔离），结果目录必须独立。
- 保留严格FAILED/NotExecuted；review_gate.py按review-classification.json中的完整FQN逐项判断，新增6个托盘图片测试为required_automatic，不能跳过。
- 只有审阅门禁通过才发布新的win-x64自包含目录；不覆盖旧table-content/R7目录。产品EXE不自动启动。
- 复制正常testhost到新目录，仅替换发布产品DLL并核对SHA；执行托盘图片6例、R6/R7/表格/DOCX原子替换回归。Core完整发布组件回归显式设置PIXEL_TART_BATCH12_RELEASE_CORE，并另跑该身份类证明实际加载发布Core。
- 设置KITAO_TRAY_EVIDENCE为独立目录可产出选图器、基础/进阶正文离屏图、带嵌入图片DOCX及元数据摘要。仅自绘合成图，不能用私人图库或原型参考图代替。

Windows人工键盘/鼠标、物理DPI、WPS真实打开编辑导回保持NOT_RUN。自动创建WPF对象/解析OpenXML不是这些人工验收。

本地策划迁移请保留文档JSON及images目录；当前CopyPath为绝对路径，工程移动后的路径重写尚未实现。跨无素材库电脑交付使用嵌图DOCX；这轮不提供新的工程打包格式。
