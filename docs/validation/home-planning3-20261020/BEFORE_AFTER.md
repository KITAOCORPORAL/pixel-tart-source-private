# 本批失败与最小修复

时间 2026-10-20T23:50:26.895466+08:00（Asia/Shanghai）。所有原TRX保留，不覆盖/删断言。

## 真实产品缺陷：当前分区名与参考分类

原名 `PlanningBatch3BusinessTests.CurrentSectionReferenceClassificationAndFilterUseCurrentNames` 两个DataRow：灯位图→灯光，服化道与道具→造型。
首次REFERENCE_REPRO为2通过/2失败。失败断言明确期望灯光/造型，但实际均为其他；实际业务入口AddDocumentReferencesAsync仍比较旧灯光图/服化道。VisibleDocumentReferences同样未识别当前名称，可能显示其他类别。

修复仅两个ViewModel文件三行条件：接受当前名称并保留旧别名，不变更任何数据ID/存储格式/像素算法/界面。
REFERENCE_FIXED：4/4通过；最终已发布组件本轮新4项、策划专项64项均通过，完整WPF中相同原名/两个行通过。真实WPF资源创建/编辑、正文/镜头、档期、账本、相对图片、草稿和搬迁另有连续测试，不以分类小测试替代完整闭环。

## 新测试自身范围错误，不作为产品缺陷

首次NEW_WPF：1通过/1失败，格式测试调用PlanningProposalPdf.Export(dpi96)被现有产品正确拒绝；公开入口仅允许216/300。原失败保留，测试改用216，未放宽产品合同；NEW_WPF_REFINED 2/2。RTF/页眉页脚/图像/表格断言保留。

## 完整Core额外失败：保留，不在本批刷绿

`RAWSelectionAssistant.Tests.AssetLibraryP35AcceptanceTests.Scenario11_TenKSwitchAndHundredKMetadataPreview`：10,000次切库累计32139.0909ms，既有阈值30000ms，FAILED。
本批完整Core在四片WPF并发运行；这可能是性能观测环境因素，但未做因果证明，不能把它自动归类为纯环境问题。未改素材库、未放宽门槛、未重复刷到通过。原TRX/日志与组件绑定已封存，后续单独性能批次诊断。

最终Core1714结果：1708通过、1失败、5跳过。整体FAIL/PARTIAL诊断版，不能发行。
