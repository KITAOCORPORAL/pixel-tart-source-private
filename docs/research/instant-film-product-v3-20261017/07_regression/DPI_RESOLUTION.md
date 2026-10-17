# 原17项DPI失败：本轮真实生产流程关闭逻辑证据缺失

首次本树RAWSelectionAssistant.DpiTests：91总计，74通过/17失败/0跳过。逐原名和参数/原错误见DPI_FIRST_FAILURES.json，保留原TRX。缺失原因是artifacts/current-run-visual/current-run-visual-evidence.json及其当前图片/metadata不存在，不是通过改断言解决。

构建本树UiReview生产器，按仓库tools/RC12ProductVisualHarness/Invoke-RC12ProductVisualHarness.ps1真实运行；进程在独立非输入桌面，未SwitchDesktop、未发送用户输入/更改物理DPI/系统ICC；106个真实shell/XAML状态依次进程隔离，32个100/125/150/200逻辑DPI截图及各metadata重新生成，没有复制其他构建截图。完整样本source和exeSHA、106图片+metadataSHA在CURRENT_LOGICAL_PRODUCER_IDENTITIES.json。绑定本树源码ec9b6af4；本轮后续生产src像素/控件代码无更改，研究工具提交不冒充新生产图。

生成后原DPI套91/91通过、0失败、0跳过。未修改任何DPI测试断言。metadata显式automated-logical-simulation和physicalDpiManuallyTested=false；**物理DPI/真实前台交互仍NOT_RUN**。

原普通输出发布尝试被本轮UiReview生产器锁DLL而失败（原日志保留），未杀用户进程/重试覆盖；用全新--artifacts-path隔离构建和全新发布目录成功，避免干扰当前生产过程。
