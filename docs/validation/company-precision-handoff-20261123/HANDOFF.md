# 接续入口

本交接从ce9c38e7404d74d146ec918f862c46a38adfb85b开始，产品源码ac1c57e9689428cea3f6938e06ad26209c7fc3d5。原研究树和公司73988e4f产品树保留；本树仅交接文档/产物比较脚本，无产品源码更改。集成顺序cf5a63d3→11a424c9→ac1c57e9，或组合patch；不要重复应用。见PRODUCT_INCREMENT/INTEGRATION_CHAIN。

公司原0806D680... RGB48实存且完整hash正确；241118208bytes，7752×5184，方向1。MATERIAL_MANIFEST/COMPANY_TASK记录所有授权路径/hash/配方。没有读相机RAW，旧账本/余额未动。

最新结论：公司原载荷首轮处理100%、保存、实际TIFF通过；两份首轮导出各120559104个uint16样本对Core参考差异0，RGB24显示SHA/ICC一致。但重复重开在第二次ushort读取边界PrivateBytes2159071232触发2GiB/50ms守卫，第二轮/第三轮没有完成。独立一次首轮观察只补显示摘要，不是重开通过。资源运行至此停止，失败不重试、不加预算。

看REPORT.md、COMPANY_COMPARISON.json、NEW_FAILURE_BOUNDARY.json、runs/company-processed-three-cycle/STOP.json。API_MEMORY_BOUNDARIES明确Rgb32/Clone/整帧writer/图空间回落何时展开，以及准入16行路径。原Core85/WPF20为上游已封存回执，本轮不重跑。

实际TIFF、payload、原cache、独立build-app/stage、SDK/runtime和完整本地输出在E:/Kitao-Deliveries/precision-product-handoff-20261123，LOCAL_ARTIFACTS列实际文件hash，不进Git。工具tools/PrecisionProductHandoff20261123/verify_company.py只比较产物；源绑定在COMPANY_SOURCE_BINDING。

产品线准入保持PARTIAL/公司重开资源门禁FAIL。只有集成或本公司材料的新失败再驱动针对性定位；不重做旧根因调查，高光/锐化封存不动，不接X-Trans、不替换发行DLL、不新建RAW批次。本轮没有合入或打发行包。
