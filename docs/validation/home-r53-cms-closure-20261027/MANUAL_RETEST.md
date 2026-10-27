# 人工复测（未执行，不算PASS）

1. 双击完整包 `Start-Guangjing-Diagnostic.cmd`。默认每次全新隔离UserData；同根重启用PowerShell7执行 `Start-Guangjing-Diagnostic.ps1 -DataRoot <带诊断标识旧根>`。不打开网页/资源管理器，不修改系统策略。
2. PS7.5+执行 `Prepare-CMS-Synthetic-Samples.ps1 -Destination <全新目录>`；暗室→文件→打开项目，选film-current-edits工作文件。检查已冻结ICC、来源变体、种子，普通保存重开。
3. 或导入该目录input.tif，颜色管理→依嵌ICC新变体；曝光后左侧“即时胶片”选唯一相纸，右侧选择“使用当前编辑”、固定seed、关闭动画→预览→取消不建历史→显影。确认新变体只读配方，旧版本保留。
4. 保存→正常点X完全退出→同根新进程重开；导出JPEG/TIFF到不同全新目录。JPEG对话框须显示实际空间和8位，默认sRGB；显式P3/AdobeCompat查看ICC；ProPhoto8不作默认交付。
5. 发送出版，选择sRGB/P3/兼容Adobe目标，缩放和文字/透明图片水印；执行输出，检查实际ICC/位深/方向/没有原件覆盖。普通旧输入不应出现未支持managed空间。
6. 打开wide-linear-v2项目：仅曝光/Normal蒙版/几何可用，其他工具拒绝且不改数据；保存重开/导出应保持。V1旧工具宽域裁切不当作V2完成。
7. 换模式/文件/变体、取消显影/导出、退出重启核对无半成品；原生导出中关窗、物理磁盘满均未执行，不从注入测试升级PASS。
8. 显示管理默认off；显式SDR需确认系统未重复转换。多屏ACM/HDR与物理DPI另验，不宣称校准。透明PNG走独立Alpha合同，新照片ICC入口不可静默压白。

当前用户规则禁止Computer Use/Browser/WebView/CDP，自动测试与OFFSCREEN图不能替代上述原生鼠标闭环。
