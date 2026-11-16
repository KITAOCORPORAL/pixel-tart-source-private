# 公司普通 TIFF 越限后的隐藏预览修复

2026-11-16，Europe/Berlin。状态：**公司原授权样本的普通 App 原生功能闭环、2GiB资源守卫及完整逐行像素比较通过；截图和数值缩放读回限制保留。**

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

## 最终普通 App 原生闭环

用户原生导入后，由产品接续会话f438b3d4独占R2窗口执行原生按钮、系统目录/文件对话框和项目保存/重开；本内存会话停止并发输入，只读核对实际项目、产物、二进制和资源记录。记录不是固定对话框的离屏宿主。

普通进程12852完成导入、适合/100%/缩放按钮、实际TIFF导出、项目保存、自然空闲、最小化恢复及正常关闭，exit0、无STOP。保存文件ordinary-tiff.ptstudio.json的SHA42cc8b89ae1f3ea5ea586253ebcc0526dc0cec7e6a7f90c35b06ecbc22304baf，包含唯一授权路径、0da1bf38源哈希和首份实际导出的同一活动版本。

新进程21192从系统文件对话框打开该项目，原图及曝光0读回，原生执行适合/100%/缩放后实际再次导出，再正常关闭，exit0、无STOP。两次启动的EXE与产品DLL逐字节一致、运行根一致，PID不同。

| 普通运行 | PID | 私有提交50ms峰值MiB | OS工作集峰值MiB | 退出 | STOP |
|---|---:|---:|---:|---:|---|
| 首次 |12852|1174.80|924.00|0|无|
| 独立重开 |21192|958.20|828.43|0|无|

2GiB=2048MiB；阈值保持，未强制GC。全程保留原始采样、第一轮135231条及第二轮27554条。阶段峰值如下；手工阶段标记包含对话框和等待时间，不是单独编码函数的分配普查。

| 运行 | 阶段 | 私有峰值MiB | 工作集峰值MiB |
|---|---|---:|---:|
|首次|native-import-and-settle|999.53|870.45|
|首次|native-loaded-natural-idle|881.68|789.05|
|首次|ordinary-fit|949.16|541.52|
|首次|ordinary-100-percent|952.76|545.41|
|首次|ordinary-zoom-out-from100|949.14|540.18|
|首次|ordinary-first-export-dialog|977.30|690.04|
|首次|ordinary-return100-and-save|949.82|545.24|
|首次|ordinary-minimized-idle|949.54|545.12|
|首次|ordinary-restored-idle|964.51|547.47|
|首次|ordinary-natural-close|955.03|549.57|
|重开|ordinary-open-saved-session|958.20|828.39|
|重开|ordinary-reopen-fit100|915.80|804.51|
|重开|ordinary-reopen-zoom-out|918.80|806.51|
|重开|ordinary-reopen-100-return|923.52|811.01|
|重开|ordinary-second-export-dialog|946.15|820.48|
|重开|ordinary-reopen-natural-close|926.07|820.20|

### 两份普通 App 实际产物独立读回

实际输出保留在产品线continuation-20261116/tiff-exports-first与tiff-exports-reopen。独立只读进程55256在同一2GiB守卫下exit0、无STOP，逐行读取完整5184行，比较所有输出存储通道、RGB48样本及元数据；不是抽样或仅比较缩略图。两份均7752×5184、RGBA64/每通道16位、方向1，全部存储通道一致，RGB48与原输入5BE9B355F579CFDC2AEA9FCFEC8E0208A2489B605E85DB84E6BCE688F63FB8AA完全一致，ICC3144字节/SHA2B3AA1645779A9E634744FAF9B01E9102B0C9B88FD6DECED7934DF86B949AF7E保留。两份文件SHA匹配各自Completed侧车，源文件0DA未变，冻结调整fingerprint相同。

正式测试源码62191dff与产品整合e8180221937fa4abb578a16faacebe99a5d4ac1b的整个src/tests Git树完全一致：src623bad114418fa984fc86762e78a6004e7d2a614，tests1ba50b4a2262c6cc9d22a824d9a384722272c8cc。不同InformationalVersion的发布二进制不声称字节一致。sRGB最小修复a9932b73和隐藏预览修复62191dff均已交产品线，打包由产品接续会话统一完成，本会话未发布分叉产品包。

### 验收边界

原生UIA回执确认窗口最大化、原生按钮和系统对话框执行；照片页没有数值缩放属性，故100%/缩放动作有原生执行回执，不声称额外数值读回。截图采集仍不可用，不声称屏幕像素或显示器色准验收。首次后台/最小化下的试探回执保留为诊断，不提升为可见窗口证明。

新增相机RAW输入/解码动作0：只允许原授权已显影TIFF，保存项目/导出侧车RawDefinition和RawInput为空，使用栅格/WIC链；普通进程未安装独立相机解码调用计数器，受控宿主的请求计数0另列。高光1bfdfbe9继续研究，其他RAW后继授权不变。原公司项目、客户结果、预约、原dirty及旧STOP均保留。产品手机/iPhone等其他模块状态由产品线独立报告。

最终独立联合审查见ordinary-final/FINAL_NATIVE_REVIEW.json、ORDINARY_PAIR_VERIFICATION.json、SESSION_AND_PROCESS_BINDING.json和两进程guard回执。本机最新结果为E:/Kitao-Deliveries/tiff-srgb-company-20261116/RESULT.json。
