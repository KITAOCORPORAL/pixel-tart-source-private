# 产品线交接

基线 `d07e4f4370c8e7d8505a46e3085cc6a069fb4bd4`；隔离产品提交 `cf5a63d38365a0093524d87ed7670e3736b7bb24`。分支 fix/legacy-high-precision-export-20261122，工作树 `N:\pixart\guangjing-legacy-export-memory-20261122`。正式产品未合入。

先读REPORT与RECEIPT。LEGACY_EXPORT_MEMORY.patch已在独立临时Git索引的原d07上apply --cached --check，通过且未改写任何旧工作树。补丁只改Core分配/所有权边界及回归测试，无色彩公式/顺序/发行解码器变更。

能证明：40MP中性完整100%周期、原片100%显示＋点栈导出周期过2GiB；两次实际TIFF与原d07独立Core oracle全部样本一致。不能证明：处理后点栈100%预览、所有空间/图栈、Sony方向规范化后同进程完整周期、原0806payload接纳。Sony二次导出前OS工作集STOP仍在，完整产品合入条件未满足。

Core58通过/1个真实RAW测试跳过；WPF20通过。新增RAW0，不接XTrans、不替换发行DLL、不创建cold/rebuild或重型账本、不动旧余额。高光仅恢复4项只读数值条目，私有面板缺失；锐化V1封存。

下一入口：runs/final-sony-normalized-cycle/STOP.json、events.jsonl、RESOURCE_RESULTS.json；需先用分配/实际根证据区分方向临时buffer和旧视口资源，不以Private不降认定泄漏。当前所有失败目录和本地TIFF均保留，路径见LOCAL_ARTIFACTS。

本地复测工具：tools/LegacyExportMemory20261122。正常EXE/人工文件选择器验收不由该屏幕外WPF替身宿主替代。
