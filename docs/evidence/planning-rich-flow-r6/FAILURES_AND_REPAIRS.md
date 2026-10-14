# 失败与修复记录

- baseline/baseline.trx：2/2失败，分别证明表格整行快照覆盖检查器字段、重绘丢失未到定时器的富文本草稿。不是推测。
- repaired/repaired.trx：对应两用例通过。
- continuous-02：有图RTF重载后的DOCX导出抛出“不支持的内联对象”；适配InlineUIContainer图片后通过。
- continuous-04/05/06：重复导入生成2份恢复点，尝试简单RTF等值/重新序列化不能处理新编辑器规范化；改为导入内容+规范化后的基线会话幂等。
- 编译迭代曾有测试命名参数Title使用等号、图片辅助方法重命名遗漏；日志保留。不是产品运行失败。
- 视觉第一轮：固定38行高裁剪多行动作；日期/时区缺失；页面预览黑字黑底；缩放后组合框仍显示100。均在源码修复后再次渲染并人工查看静态图片。
- 主壳首次合成DataContext未提供IsSettingsModalOpen，离屏设置遮罩显示；补充fixture属性，非产品MainViewModel故障。不把此诊断截图作交付图。
- targeted-01有1个截图opt-in跳过；后续显式设置输出路径，targeted-02及冻结/发布全部0 skip。
- 最终34 WPF + 81 Core，实际发布组件同组重跑通过。全仓套件未执行；Windows/WPS仍NOT_RUN。

早期输出 UNFROZEN_WORKTREE 仅诊断；最终证据绑定526490439dfc4cad26b98627f6b7cbbfd7290c8e。
