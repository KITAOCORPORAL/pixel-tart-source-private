# 本轮初次失败与处理

- Core首次：工作台测试仍要求旧透明背景；改为验证实际要求的轻明度差、内距和位置。修正后Core1530/0/4。
- WPF分批：删除定义按钮缺Automation Name；补齐产品可访问名称。
- WPF首次全量：5项。动态固定筛选缺稳定ID；已补齐。旧测试要求色板仍在组合面板和旧颜色解析代码；改为独立入口/真实颜色解析行为。
- WPF第二次全量：Quick Loupe被popup自身MouseLeave关闭；改为真实hover anchor决定关闭，菜单预览由Esc/外部点击关闭。
- Loupe补充测试首次使用Grid入口，而默认是Masonry；改为当前真实入口，事件与关闭断言保留。
- WPF第三次全量：剩1个结构测试要求旧popup MouseLeave回调；替换为入口关闭、Esc和非交互surface无MouseLeave断言，6项定向回归通过。
- 未删除测试、未新增skip，未将旧失败日志改成PASS。
- 第一次WPF全量默认console没有逐项输出，主动中断后改为normal日志重跑；后续完整执行约8分钟，未发生testhost crash。

详见INITIAL_FAILURES.json及最终TEST_SUMMARY.json。
