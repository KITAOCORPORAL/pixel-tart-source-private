# 光镜根因修复当前接续

当前分支 fix/root-cause-b0-b1-b2-20261117。产品源码 d07e4f4370c8e7d8505a46e3085cc6a069fb4bd4；当前完整包 Guangjing-RootCause-d07e4f43-Full-win-x64。此前433/cb8c1adc包及失败原样保留。DELIVERY.json是唯一当前交付索引，包外CURRENT_DELIVERY.json另附文档/远端SHA。

当前明确断点：普通EXE PID56424，创建FILETIME134395409734408196，独立根 E:/Kitao-Deliveries/root-cause-fixes-20261117/tiff-d07e4f43-20261119。这些是线索，继续前重查。当前空暗室，未载入工作文件，自动化报30s超时/AX不可用。请通过普通“导入 / 导出→打开项目…”打开：
E:/Kitao-Deliveries/root-cause-fixes-20261117/tiff-d07e4f43-20261119/output/tiff-recovered-exposure01.ptstudio.json

副本SHA474f6d83a3d7b5d5d0008499fff3d43e2c8216108c11e537049d0b9166ed19fb，保存原exposure0.1，指向已授权7752×5184 TIFF，不新增RAW解码。普通载入后继续可见曝光+0.2/撤销重做/回0.1、保存、正常退出、独立进程重开、两次真实TIFF导出和全行像素/位深/方向/ICC。2GiB/50ms、无强制GC保持。

最新事实：cb普通连续编辑/撤销再次2227568640bytes STOP/exit1；d07修正分析缓存持有旧整帧和实际位深计费。41项相关回归通过；受控同输入连续编辑/撤销重做/保存/另一ViewModel重开通过，峰值1321.35MiB。受控结果不替代普通EXE验收。证据入口 retained-preview-followup-20261119/RCA_AND_SCOPE.md、CONTROLLED_REPEAT_RESULT/RESOURCE、ORDINARY_CURRENT.json。

原433普通JPEG同项目主线PASS保持原source：索引两张、曝光0.5、浏览器锁定v1/r6、PC收回、真实JPEG2/ICC3144、保存exit0、独立进程重开/来源重扫。来源路径/数量保持，源子行GUID保存时重建边界不隐瞒。家中0a45ee0e和内存映射17a95b5a/14eda5c7均已包含，不重复应用。不能把原source通过改标为d07整主线通过。

手机同源码服务PID50824/FILETIME134395426034383448、loopback17662已准备，原合成预约revision4全行不变，新设备授权到2026-11-21T06:17:20Z。TailscaleNeedsLogin；新登录URL仅见包外continuation-20261119/TAILSCALE_LOGIN_PRIVATE.txt，设备凭据仅外部runtime。登录后核对正确tailnet/HTTPS再给真实手机链接。受PHONE_HANDOFF_ACTIVE保护，不运行seed/roundtrip/pc-adjust修改原预约。

整体PARTIAL，普通TIFF/若干空名发送关闭边界/双root/iPhone未完成。不再读取旧聊天整段；历史交接在history/，按具体证据取件。
