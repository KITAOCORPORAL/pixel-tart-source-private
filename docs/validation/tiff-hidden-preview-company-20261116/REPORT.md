# 公司普通 TIFF 越限后的隐藏预览修复

2026-11-16，Europe/Berlin。状态：**受控公司样本两轮通过；修复后的普通 App 仍待原生导入与完整复验。**

## 源码身份与最小补丁

产品线最新基线49fbc2f6d47e3bacabb34878e89e37e33523939e已包含两笔缓存补丁及sRGB恢复修复a9932b73。内存候选62191dfface51c981b1361a4333a3c0981067cb1仅改变3份生命周期源码及LightPrintR3Tests，34行增加、7行删除。该补丁来自11月13日已READY的精确封存候选；8份封存/原dirty文件逐一SHA核验一致，仅选择3源码和对应测试，不移植原生输入工具或其他dirty修改。原树未重置、暂存、清理或覆盖。

隐藏InstantV3Editor在DataContext和目标变化时会排队相纸预览；光片页的实时草稿也曾在隐藏时排队。新增Loaded/Visible限制，隐藏时停止gallery调度；光片草稿仅在光片页激活时运行，离开时恢复源。先持久化新页签再恢复草稿控件，避免通知将页签切回。像素公式、历史recipe、方向、位深和ICC未改。

旧采样中241MB byte[] / 482MB Single[]是历史归因证据，当前小样本回归重新复现隐藏控件排队问题。此次普通App STOP未重新采集分配轨迹，不将历史轨迹声称为当前进程的独立分配普查；修复后的普通App是否完全解除越限仍需真实入口复验。

## 普通 App 原失败全部保留

用户第一次确认导入后，a9932b73普通App PID4532触发保持不变的2GiB守卫：50ms采样峰值私有提交2149568512字节，OS工作集峰值1697746944字节。当时最后一份AX快照早于载入完成，不能据此说用户导入了错误窗口。

STOP、原始LAUNCH/EXIT、最后90秒采样见ordinary-failure/；全部134622条原始采样仍在本机原路径，SHA见RAW_SAMPLE_BINDING.json。原launcher在taskkill后未等待退出就写EXIT，故原exitCode=null，不能记为成功；之后进程查询确认已不存在。新launcher等待退出后记录真实退出码。原失败没有删除或提升为通过。

## 回归和二进制绑定

当前代码先复现新隐藏生命周期回归失败，再移植候选。封存文件保留旧时间戳，首次增量构建复用了没有候选方法的旧DLL；该轮单列NOT_CANDIDATE_RESULT。显式Rebuild Release Platform=x64后34/34通过；在49fbc2f6整合基线上又通过34/34。

测试与普通App、诊断宿主的KitaoPhotoSelector.dll逐字节一致，包含SetLightPrintPreviewVisible及62191dff版本号，见HIDDEN_R2_TEST_BINARY_BINDING.json与HIDDEN_R2_BINARY_BINDING.json。不是旧platform输出或仅源码时间戳推断。sRGB原图输入、实际曝光、撤销/重做、保存重开和两次产品命令导出合同继续通过。

## 公司原授权样本受控复测

输入仍是原授权7752×5184 DSCF0347_仿色.tif，没有新增RAW。两轮独立进程都用实际PhotoGradePageView及生产命令，执行适合、100%、20%、100%、实际TIFF导出；首轮保存关闭，次轮从该会话独立重开再导出，同时记录最小化恢复、自然关闭等阶段快照。

| 运行 | PID | 50ms私有峰值MiB | 退出 | STOP |
|---|---:|---:|---:|---|
| 首次 |49764|771.23|0|无|
| 独立重开 |53748|807.17|0|无|

完整5184行读取并比较所有输出存储通道与RGB48样本，两份输出像素一致，RGB48与未调整输入相同；16位每通道、方向1、尺寸及3144字节ICC全部一致。输入文件SHA未变，RAW请求和新增解码均0，无强制GC。比较进程也在2GiB守卫下exit0。

这些窗口在屏幕外、对话框固定，属于受控宿主；不代表普通App/系统文件选择器或真实屏幕已通过。过程50ms峰值是整个进程峰值，各阶段内存来自命名快照；通用guard的startup阶段标签不被解释为逐操作50ms峰值。完整结果见HIDDEN_R2_CONTROLLED_RECEIPT.json和HIDDEN_R2_COMPARISON.json。

## 当前真实入口断点

同一产品DLL的普通App使用字节相同的唯一apphost KitaoTiffCompanyR2_20261116.exe，PID12852、窗口7672378，独立company-tiff-r2-runtime。2GiB/50ms守卫继续运行。原生截图两次超时，点击返回GetCursorPos拒绝访问0x80070005；因此准备了准确窗口和原授权路径供用户重新选择。

修复后的普通App尚未载入照片；原生导入、适合/100%/缩放、实际导出、保存关闭、独立重开和第二次导出仍待完成。最终普通App产物逐行比较也待完成，不能用上面的受控输出替代。

产品线原窗口PID9616、workflow-runtime、项目和手机预约未操作。高光1bfdfbe9保持研究状态。本会话不发布分叉产品包；由产品Codex统一整合。最新本机RESULT.json及产品线TIFF_SRGB_WORKER_RESULT.json为接续入口。
