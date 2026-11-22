# 产品线交接

入口REPORT、ROOT_FINDINGS、FINAL_COMPARISON与RECEIPT。本轮完整准入门禁通过，可交产品线集成审查；不等于普通EXE或所有大图稳定。

产品链cf5a63d3→11a424c9→ac1c57e9689428cea3f6938e06ad26209c7fc3d5。独立树N:\pixart\guangjing-precision-rootcause-20261122，分支fix/precision-reopen-rootcause-20261122。FOLLOWUP_ONLY.patch适用11a，COMBINED_D07.patch涵盖完整产品链，均apply --check验证，正式产品未自动合入。

真实根因是不可达大float尚未回收、LOH空洞不足连续分配及第二次ushort+float重叠；旧数组/旧VM根数0，不包装成引用泄漏。诊断暂停/dump绝不作为PASS。产品用现有buffer内独占RGB48源+条带float消费；不变数学/配方/方向/ICC/解码器、不新增全局框架。观察器不展开源、释放等待实际任务是宿主修正，分开说明。

处理40MP三轮、中性三轮、Sony两轮全通过，逐样本及显示SHA一致；最低最终工作集余量约131MiB。中性峰值仍逐轮增长，有限测试不能保证无限稳定；空间/图/整帧fallback资源不作新承诺。

旧STOP、候选、SOS与采样皆保留，dump/照片/载荷/实际TIFF仅LOCAL_ARTIFACTS本机路径，不入Git。源材料差异和零RAW边界延续，旧账本/余额未动；高光/锐化保持封存。普通EXE验收由产品线完成。
