# Runtime User Findings evidence

Release SourceHead: 8f9a28f3c21bcf41e24e700024d14a8648fde5b5

原始用户截图25张，来源G:/UI问题；图片留在本机user-before目录，Git提交清单及哈希，不上传原图；清单记录SHA256，未知SourceHead和DPI保持null。

本轮只Build/Test，之后用户统一实机验收。全部RUX-001～048 Runtime=NOT_RUN、UserAcceptance=NOT_APPROVED；After截图未采集。

最终Release: artifacts/releases/runtime-user-findings-final/publish/win-x64/KitaoPhotoSelector.exe

各RUX result.json记录根因、代码、测试、原截图映射；steps.md仅给人工复测提示。

tests/保留初次失败与修正后结果。旧batch-a launch证据只代表旧批次，不能当作本Release运行证明。DPI91测试读取的旧截图源de4c91a，不能代表本Release视觉通过。

所有审批false。Observer/Recorder未启动，Windows缩放未修改。
