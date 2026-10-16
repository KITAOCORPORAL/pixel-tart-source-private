# 实机验收记录：NOT_RUN

本轮未确认桌面空闲/独占，继续遵守禁用ComputerUse/Browser/WebView/CDP；未自动打开浏览器、资源管理器或产品窗口。没有修改系统ICC、DPI、日常数据根。

|项目|状态|实际替代证据（不等同）|
|---|---|---|
|新完整EXE真实启动/无异常|NOT_RUN|build/publish/哈希不是启动|
|工作台→ColorStudio前台导航|NOT_RUN|Sony真实ViewModel未创建窗口|
|Canon/Fuji/Sony前台打开、切图中取消|NOT_RUN|授权副本后端解码、pre-cancel/候选native callback分开|
|真实配方保存重开|NOT_RUN_DESKTOP|Sony真实后端SessionStore/工作台VM通过|
|前台TIFF16/PNG16/JPEG对话框和回读|NOT_RUN_DESKTOP|Sony后端全尺寸导出回读单列|
|关闭workspace/进程后的系统自然内存|NOT_RUN_DESKTOP|旧/候选审计子进程内存、退出并非GUI关闭证明|
|物理100/150/200%DPI|NOT_RUN|无改动、无“恢复已执行”声明|
|真实屏幕合成器ICC/跨屏移动|NOT_RUN|只读设备关联、offscreen WPF和合成参考不替代|
|候选独立干净Windows/VM|NOT_RUN|现机预编译成功不等同|

不因上面未运行而等待用户后继续空跑。独立后端/数值/完整自动回归照常完成；未关闭门禁保持诊断版。
