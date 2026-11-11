# R2真实Chrome验收及重放入口

本轮四段验收已于2026年11月11日PASS，冻结产品1698664d；最终结果见 ../chrome-batch-r2-real-chrome-20261111/FINAL_REPORT.md。以下是保留现有数据的重放清单，重放结果应另建证据轮次。

包：E:\Kitao-Deliveries\chrome-capture-batch-v1-20261111\Guangjing-ChromeBatchV1-Diagnostic-win-x64-r2。采集桌面入口 Start-Guangjing-Capture.cmd。当前R2已运行；重开前保存当前工作。真实加载的扩展位于 E:\Kitao-Deliveries\chrome-capture-v1-20261110\Guangjing-ChromeCaptureV1-Diagnostic-win-x64\extension，0.2.0，ID obgahfdabcpkohkdcnidpmeooceomogk。若文件更新，在现有Chrome扩展卡片重新加载，保留安装身份与队列。

1. mixed.html：打开 http://127.0.0.1:54394/mixed.html，先记网页加载基线，再点击光镜、扫描，预期8项。PNG/不限尺寸=1；JPEG/最小宽1000/排除未知=4；JPEG/最小宽2000高500/保留未知=1。验证全选8→0和单张1→0。扫描阶段不应新增原图GET。
2. 部分失败及重试：运行包内Reset-403-Test.cmd。8项统一送测试库/项目A/灵感板A，预期7成功1失败。先保留失败CaptureId，再由操作者运行Allow-403-Retry.cmd，仅点“重试失败”；最终8/0/0，失败ID不变，原7项不重发。该轮也可能复用全部已有素材，成功不等于新增资产。
3. 取消与恢复：打开 http://127.0.0.1:54394/cancel.html，建立新6项队列。发送后等“取消后续项”可用再立即取消。已全部完成的尝试不能当取消通过。保存取消队列身份后，关闭侧栏、保存工作并正常关闭桌面，再用同一入口重开及重新打开同一队列，点“继续未完成”；逐个CaptureId应保持。若需要8秒观察窗口，应先备份合成FIXTURE_ROUTES.json，只改合成延时并在结束后恢复原字节。
4. 单图回归：打开 http://127.0.0.1:54394/ordinary.html，图片上右键“发送到光镜”，目的地选测试库/项目B/不加入灵感板，核对实际回执及项目B关联；再从侧栏“单图记录”读取旧完成记录，核对旧ID/AssetId/hash。

测试站点当前在运行；重启电脑后可用包内Start-TestPages.cmd启动。所有原始队列、首轮完成尝试和验收数据继续保留。桌面操作优先使用用户指定agent-desktop；使用即时snapshot/ref并核对实际操作后状态，不把动作投递成功当业务成功。
