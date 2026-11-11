# 真实 Chrome 简短验收

截至 2026 年 11 月 11 日：环境已准备，真实 Chrome 批量轮次尚未执行。扩展源码版本 0.2.0，冻结产品源码 1698664d12913aadea64bad6f2a68cb52d9ee38d。原生机器回执与浏览器界面验收分别登记。

诊断包：E:\Kitao-Deliveries\chrome-capture-batch-v1-20261111\Guangjing-ChromeBatchV1-Diagnostic-win-x64-r2。
现有加载目录：E:\Kitao-Deliveries\11.10\Guangjing-ChromeCaptureV1-Diagnostic-win-x64\extension，已更新为本轮源码。继续在现有扩展卡片操作，ID 为 obgahfdabcpkohkdcnidpmeooceomogk；原有队列存储保留。

1. 关闭当前光镜诊断窗口，再双击新包的 Start-Guangjing-Capture.cmd。当前旧窗口仍是 69bcf65a；该入口启动本轮 1698664d 全模块桌面，并沿用 company01 数据根目录和相同管道。Chrome 打开 chrome://extensions，在现有“光镜”卡片点“重新加载”。本机 Native Host 的两个注册视图均已准备到新包。无需重建素材库或扩展记录。
2. Chrome 打开 http://127.0.0.1:54394/mixed.html，点击工具栏光镜图标，再点“扫描当前页”。预期合并同 URL 后为 8 项，含 JPEG、PNG、srcset、懒加载和明确的未知尺寸。试最小宽度 1000 / JPEG 筛选、全选、取消全部和单张勾选；再设最小宽度 2000、最小高度 500 并保留未知：srcset 的已知宽度 1600/1800 项仍应排除。扫描后的请求日志应只增加页面需要的资源，不由扩展预取全部原图；日志基线已单独保存。
3. 清空尺寸条件、格式设全部并保留未知尺寸，选择 8 项。统一选“浏览器采集测试库 / 采集项目 A / 采集灵感板 A”，点击“发送所选”。如 Chrome 弹出所选原图域名授权提示，由用户确认。预期 7 项已入库或已有素材，denied-original-1800.jpg 明确失败；同字节异 URL 使用同一素材身份。最终判定需桌面回执中的 CaptureId、AssetId、hash 和目的地。
4. 双击新包 Allow-403-Retry.cmd，然后在侧栏点“重试失败”。预期只重试失败项、保持其 CaptureId，成功项不重发。该开关已在实际合成站点用 HEAD 验证 200；Reset-403-Test.cmd 已验证恢复 403，当前环境留在 403 初始状态。
5. Chrome 打开 http://127.0.0.1:54394/cancel.html，点光镜工具栏、扫描并选择 6 张延时图；发送后点击“取消后续项”。确认后续项停止启动，已提交项继续核对。关闭侧栏和桌面，再用同一 Start-Guangjing-Capture.cmd 重开桌面和侧栏，选择原持久化队列点“继续未完成”；确认保留原条目和 CaptureId、最终回执齐全。
6. Chrome 打开 http://127.0.0.1:54394/ordinary.html，在图片上右键，使用现有光镜原图入口，选项目 B 发送。核对真实回执，侧栏“单图记录”仍能看到此前记录；这是新版单图右键回归，尚不能由 9 项 Node 检查替代。

页面服务器现已运行。若重启电脑后站点无法访问，先双击新包 Start-TestPages.cmd 并保持该窗口运行。服务只监听本机 127.0.0.1。运行 Reset-403-Test.cmd 可开始新的一轮失败重试；它只修改合成站点开关。

可分段反馈：先回“已重载并扫描”和候选数量；随后提供发送、重试、取消/恢复、单图右键的实际结果。回执读回由后续会话按 BATCH_HANDOFF_CONTEXT.json 的现有 company01 根目录继续，只查询本轮 CaptureId，保留历史数据。

本轮 Computer Use 因无法可靠确定当前 Chrome URL 而被工具安全检查停止，未自动执行任何 Chrome 点击、扩展重载或新入库。真实轮次仍为 PENDING；全产品保持完整范围诊断预览、未完成验证/不能发行。
