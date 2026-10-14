# 失败、修复及验证边界

- 初始完整Core：1682 pass / 7条件skip / 0 fail。
- 初始完整WPF单进程无进度挂起，testhost锁定DLL；记录hung-testhost.json后只结束本R7进程。未把中断报PASS。
- 改逐类进程隔离；首次脚本只发现每文件首class，随后修复枚举并补跑全部遗漏类。两个证据校验类60秒超时，600秒窗口重跑121例通过。最终脚本枚举全部TestClass并对两类使用600秒。
- 基线两个失败：CanvasMenusExecuteClipboardAndShareZoomStateAtAllViewportSizes（paste.IsEnabled）；ClosingOwnerWithSavedCanvasDefersCloseAndCoalescesRequests（SQLite Projects表不存在）。来自未改R6组件，同名最终结果单独报告。
- R7编译迭代中出现局部变量width/p重名，修复并保留日志。
- 重复表头首轮测出chunks=2但PageCount=1；Table.BreakPageBefore不足，改用显式分页段落，预留4DIP测量余量。测试断言每段各一页，并追加XPS打印序列化检查。
- 中间冻结WPF有一次PageContract目标替换UnauthorizedAccessException；原失败保留，同源码单例重跑通过。不能删除失败记录或称修复了未定位的环境原因。
- 发布首次与构建并发读写Core DLL，MSB3248；保留失败日志，改顺序执行后发布成功。
- 图像审查发现查找页签白底浅字、两个设置面板堆叠；已修复深色页签、互斥显示、Esc优先关闭，不生成假七页签。
- 部分既有完整WPF用例调用Window.Show创建合成HWND/弹出菜单。本轮全量回归执行了这些自动测试；没有使用Computer Use，没有人工Windows/WPS操作。自动HWND测试不标为Windows实操批准。
- 测试生成ui-guardian文档在本工作树保存副本后恢复原Git字节；其他工作树和用户修改保持不动。

最终冻结9446c19完整WPF：1583通过、2失败、23跳过。页面覆盖原文件错误在隔离Start-Process方式可重现；直接命令与发布专项通过。新输出路径测试可通过，但没有保留该弱化补丁；最终源码仍保留原覆盖断言，问题OPEN。最终画布剪贴板通过，SQLite Projects用例仍失败。
