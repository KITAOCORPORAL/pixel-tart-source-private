第二次申请 `ushort[]` 前，在公司捕获的这一个时刻，**没有找到指向第一轮旧处理预览的托管强引用**，因此没有可归因的持有者；标记的 `first_processed_bitmap` 弱槽仍指向对象，离线分析记录 `0` 条强根。它与历史上释放前直接持有该图的 `Editor._matchedImage` 是两个时刻。本次快照中，旧 Editor 的 `_source`、`_matchedImage`、`_frozenRawMaster` 和 `_fullRawDisplay` 均为 null。

| 公司已有回执 | 实际观测 |
|---|---|
| 输入绑定 | `CURRENT_INPUT_FIXED_BY_SAME_RUN_EXPORT`。公司原 App/Core 的 PE 版本均为 `2.3.0+f3190f44…`；授权 RGB48 缓存为 `0806d680…`、241,118,208 B。原 LAUNCH 缺 ICC 摘要；同次首导 TIFF 全文件 SHA `12b98366…` 与导出侧车相符，首 IFD 的 34675 标签（类型 7）3,144 B，与本次指定 ICC 同长同摘要 `2b3aa164…`。这只固定本次输入，不证明旧宿主当时读过该 ICC 文件。 |
| 实际暂停 | 2026-11-27 UTC，宿主 `before-ushort-allocation-2` 02:09:39.2244906 → `after-diagnostic-boundary` 02:09:54.6906247，**15,466.134 ms**。如果只取文件标记最后写入时间，READY 02:09:39.231899 → CONTINUE 02:09:54.669342，为 **15,437.443 ms**。这两个口径分别报告。 |
| GC 与强根范围 | Gen 0/1/2：`[19,9,4] → [19,9,4]`，无强制 GC。7 个第一轮标记中，读入数组的弱槽已清空；其余 6 个对象（含处理预览）可寻址，各为 0 条托管强根。暂停期间其他线程仍可前进，不能排除未暂停流程中的短暂持有；四项已结束任务并未完整枚举缩略图任务，也不测 native WIC 占用。 |
| 退出与守卫 | 采集器退出 0、未超时；宿主 **EXIT 0，STOP 为空**，按设计提前退出，未分配第二个数组。Private 与 OS 峰值工作集各自 2,147,483,648 B 守卫、50 ms、600 s；本次 1,819 次采样，Private 峰值 1,961,328,640 B，OS 峰值工作集 2,091,716,608 B。新增 RAW 解码 0。 |

**普通 TIFF 重开：** 原公司 prepared 路径同进程重开仍保留历史 STOP（Private 2,203,717,632 B）；本次未验收这条路径。**三轮容量验收：** 未完成，此次未进入第二次分配，不构成容量通过。稳定入口仍按原交接，不据抓根结果放行 f319。

诊断分支 `research/rgb48-second-open-holders-20261123`，本地及只读远端复核均为 `4f738b9151f8c6c5aacdcf5b5e23237830ba2e06`。固定版本[公司交接说明](https://github.com/KITAOCORPORAL/pixel-tart-source-private/blob/4f738b9151f8c6c5aacdcf5b5e23237830ba2e06/docs/validation/rgb48-holder-boundary-20261123/COMPANY_ICC_HANDOFF_20261126.md)。本回执只摘录已有文件；未重跑、未读取原始 dump。核对用公司原件 SHA-256：`ANALYSIS_SUMMARY` `1657eecadeef9a5c5ca573aac6031bd1133db7de88bb18325ede3fb24ab18019`；`CAPTURE_SUMMARY` `d846e543105a1335b3cbf583ea05726da0bd34d1ec9e3a69b8f911955b24e996`；`EXIT` `d9a1fb31d50e5cf3108c779d5be29672aca91b99f4dcacc2910cd349c3846089`。
