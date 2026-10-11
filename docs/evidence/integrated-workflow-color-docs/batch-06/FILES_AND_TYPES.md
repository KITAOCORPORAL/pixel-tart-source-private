# 文件与类型

路径根：N:/pixart/pixel-tart-integrated-workflow-color-docs。

| 文件（根内） | 类型/责任 |
|---|---|
| src/RAWSelectionAssistant.Core/Services/RawToJpeg/ProfessionalRawDecoder.cs | IProfessionalRawDecoder / ProfessionalRawDecoder：独立显式decode，WB选择/实际核验与合同 |
| src/RAWSelectionAssistant.Core/Services/RawToJpeg/ProfessionalLibRawAdapter.cs | 版本门控最小ABI：unpack参数，Color/RawColor读取；校验真实getters |
| src/RAWSelectionAssistant.Core/Services/Projects/ProfessionalRawPipeline.cs | ProfessionalRawDefinition / Contract / Master / Pipeline：PV4、工作域、EV与输出边界 |
| src/RAWSelectionAssistant.Core/Services/Projects/StudioEditVersion.cs | 可选RAW定义/输入，PV来自独立定义 |
| src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs | sessionv1可选字段，选中定义一致性/禁混旧stack |
| src/RAWSelectionAssistant.Core/Services/Projects/StudioVersionRepository.cs | RAW定义校验，保留CAS与来源 |
| src/RAWSelectionAssistant.Core/Services/Projects/ExportRecovery.cs | ExportRecipeSnapshot含可选RawDefinition/RawInput |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.ProfessionalRaw.cs | 新建/应用/预览/导出真实生产命令，任务栅栏/旧fingerprint兼容 |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs | 注入decoder，RAW优先load目录、批量/Publishing/导出冻结定义、失败语义 |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Versions.cs | capture/load/switch/conflict携带RAW数据 |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs | save/reopen可选RAW数据 |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Thumbnails.cs | 取消并阻止旧job覆盖PV4 |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.BatchHistory.cs | 阻止旧同步/历史写入PV4 |
| src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs | IndependentRawMode，旧renderer/复制屏障 |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml | header RAW入口、参数、旧工具隐藏/参考禁用 |
| src/RAWSelectionAssistant/Resources/Studio/zh-CN.json / zh-TW.json / en-US.json | 入口/参数/限制文本 |
| tests/RAWSelectionAssistant.Tests/ProfessionalRawPipelineTests.cs | 数学/域/失败/持久化/合成阶梯 |
| tests/RAWSelectionAssistant.WpfTests/ProfessionalRawWorkspaceTests.cs | 合成及opt-in两真实相机产品链、错误不建版本/保留参数、晚到拒绝 |
| tests/Kitao.ProcessProbe/Program.cs | raw-api/raw-identify/raw-corpus/raw-perf，本地副本file/比较/性能证据 |

未修改旧LibRawDecoder、RAW旧数学或PV1/2黄金定义。没有相机矩阵常数替代真实metadata；唯一固定空间矩阵来自LibRaw输出色空间表。

测试会生成docs/evidence/ui-guardian三份旧报告：本次生成内容已备份本地artifacts，提交前恢复测试前的tracked原字节，不计本批源码；不改原集成工作树两个未跟踪审计目录。旧报告未重新升级人工状态。
