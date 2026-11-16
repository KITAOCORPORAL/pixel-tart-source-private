# 产品线接续入口 · 2026-11-16

客户端时区 Europe/Berlin。原聊天 01a1d2bb-68a3-7973-b2f9-a5e29a6f79e4 因历史节点超限失效，当前会话已从原断点接续；历史与数据完整保留。

- 工作树：`E:/Kitao-Worktrees/company-functional-20261116`；分支：`integration/company-functional-20261116`。功能源码固定 `e8180221937fa4abb578a16faacebe99a5d4ac1b`；本轮只增加文档/原生验收工具，没有改产品像素代码。39项既有定向回归仍绑定该源码。
- TIFF已完成：普通R2导入→实际导出→系统保存→正常关闭→新PID打开工作文件→缩放切换→第二次导出；再用最终包e818的实际EXE打开同一文件、切换缩放、最小化恢复并实际导出。全部5184行/7752×5184/16位/方向1/ICC3144一致，三份实际文件SHA相同。三进程exit0，无2GiB STOP，峰值1174.80/958.20/954.97MiB。见 TIFF_RECEIPT.json、TIFF_PAIR_COMPARISON.json、TIFF_INTEGRATED_COMPARISON.json。
- 最终e818 JPEG已补完：两张1200×800/8位/ICC3144实际导出→保存→正常关闭→独立重开→原生重扫/匹配→打开保存的编辑文件，曝光0.5与“已保存”恢复。来源仍1、项目仍1、客户锁定v1/r6保持。见 JPEG_FINAL_RECEIPT.json。既有发布/浏览器确认仍是49fbc2f6来源，不重发或改写旧确认。
- 原公司项目的原始来源、客户v1/r4与预约revision2保持；手机预约revision4和设备凭据仍有效，未修改预约。历史a993导入STOP、旧49后续STOP、全部原始采样、源图、工作文件、未提交成果备份及原ZIP均保留。
- 剩余：本人完成Tailscale登录后，核验公司可信HTTPS，再做iPhone→PC→iPhone及两端重开。服务和预约已经准备，当前NeedsLogin。私有登录入口留在包外TAILSCALE_LOGIN_PRIVATE.txt，不进Git或压缩包。
- 验收边界：真实屏幕截图仍不可用，照片页不提供数值zoom读回。原生控件动作、窗口状态与文件/进程合同通过；不声称屏幕像素或显示色准通过。完整产品仍PARTIAL/不能发行。
- 约束：新增RAW解码0；2GiB/50ms守卫不放宽；不用强制GC；高光1bfdfbe9维持研究；不生成安装器；保留用户修改与原路径。

先读本文件和CURRENT_PROGRESS.json，再按DOCUMENT_INDEX.json按需读取证据。不要重载原聊天JSONL或全量采样进入上下文。原数据：`E:/Kitao-Deliveries/company-functional-20261116`；本轮原始回执、产物和项目快照在`E:\Kitao-Deliveries\company-functional-20261116\continuation-20261116`。最新远端提交与交付哈希见包外REMOTE_READBACK.json。
