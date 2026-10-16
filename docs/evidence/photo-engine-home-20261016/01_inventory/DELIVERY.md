# 阶段1：家里本机只读清点

2026-10-16 Europe/Berlin。fetch核验feature/kitao-photo-engine-candidate-20261016为2ca931e0e1550942ed2c37ff58c51a0382a3d18d，与预期一致，无远端新增差异。独立工作树N:/pixart/kitao-photo-engine-home-20261016，分支feature/kitao-photo-engine-home-20261016；原9树及旧源码树3个未跟踪审计/研究目录保留，未重置或清理。

仅核对既有授权清单的精确文件名：N:/pixart/RAW/Real/canon/TNAN8886.CR3与fuji/DSCF0347.RAF SHA同原授权记录；sony/DSC09916.ARW未在此准确位置找到，不搜索私人目录或凭相似名代替。原照片只读，后续计算用隔离副本。既有7个Release路径/EXE hash见EXISTING_RELEASES，全部保留。

Windows11专业版10.0.26200 x64；当前磁盘可用字节在HOST。只读EDID显示AOC Q27P10、ASUS PA279CRV；适配器RTX5060Ti。GetICMProfileW当前关联DISPLAY1=ASUS PA279CV-12026625.icm，DISPLAY2=Generic PnP Monitor-22026625.icm，路径/hash见DISPLAY_ASSOCIATIONS。EDID PA279CRV与profile名PA279CV不一致需要人工核对，文件名/关联不等于实际校准，未修改任何配置。

读取资料DELIVERY、PRODUCT_VS_RESEARCH、DISTRIBUTION_BLOCKERS、RAW_CANDIDATE_DECISION及tools/photo-engine的CAPI/Python链。研究候选与产品身份分开，不使用E:/D:旧构建。AGENTS要求像素改动前2套成熟实现对照，继续遵守。本批先做诊断，不批准显示或Sony生产像素改动。

无Browser/WebView/ComputerUse。没有确认桌面独占，不启动产品或改DPI。小工具仅只读Win32显示配置查询；不是完整产品原生验收。实机项目统一NOT_RUN，不能将上述查询/构建当启动批准。
