# 完整诊断包启动脚本

将这些脚本放到发布根，旁边必须有 app/KitaoPhotoSelector.exe 和 samples/。默认 Start-Guangjing-Diagnostic.cmd/.bat 每次创建独立根并只启动完整EXE，不调用网页、资源管理器或修改执行策略。PowerShell脚本支持复用带诊断标识的数据根。Prepare-Synthetic-Sample.ps1需PowerShell7.5+，只复制提供的合成图并重绑工作文件路径，拒绝覆盖现有目录。

诊断不能发行。源身份/目录/测试/ZIP以 docs/validation/home-r53-stage-a-20261026/FINAL_REPORT.md 为准。
