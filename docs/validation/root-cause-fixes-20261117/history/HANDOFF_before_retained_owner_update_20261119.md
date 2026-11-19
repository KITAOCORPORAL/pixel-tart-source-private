# 光镜根因修复当前接续

## 当前修订与包

同一分支fix/root-cause-b0-b1-b2-20261117，当前必要内存修订cb8c1adcde14c7092d0bd22ca692d6073d9efa09。Full包App/两服务/native-host/扩展保持整套交付，当前路径Guangjing-RootCause-cb8c1adc-Full-win-x64；原433包和失败现场原样保留。只保留一个当前交付指针，未另起产品线。包SHA及最新文档HEAD见DELIVERY.json/包外CURRENT_DELIVERY.json。

## 已证实及待验

- 原433普通JPEG全主线PASS，输入两张、曝光0.5、真实浏览器锁定v1/r6、PC收回、实际JPEG2/ICC3144、保存exit0、独立重开/重扫均有原source回执。不要将它改标为cb修订完整普通流程。
- 原433实际40MP已载入，但普通曝光0.1之后触发2GiB守卫，PrivateBytes2424029184、exit1。0.1已在终止前自动保存；原输入和Jpeg根未动。见tiff-memory-followup-20261119/ORDINARY_433_EXPOSURE_STOP.json。
- 新修订只为16-bit单曝光、无mask/graph/reference等条件改变缓冲存储，复用原Core函数。旧整帧函数/数学版本未改；跨块/alpha/大图与旧法逐位一致，授权40MP5184行逐行参考一致；受控峰值约868MiB，取消预留恢复。冻结源34项受影响回归通过。都不是新普通窗口PASS。
- 初始“仍准备/加载死锁”被本地只读快照推翻；未据此乱改会话代码。隐藏/禁用数字控件的操作失败与实际可见曝光滑块导致的Guard分别记录。本地堆快照/dotnet诊断工具不入Git或包。

## 当前用户操作与运行根

已启动cb完整包普通EXE，独立根E:/Kitao-Deliveries/root-cause-fixes-20261117/tiff-cb8c1adc-20261119，当前PID56004/FILETIME134395315397272068（仅线索，继续前重查）。普通打开工作文件入口待载入：
E:\Kitao-Deliveries\root-cause-fixes-20261117\tiff-cb8c1adc-20261119\output\tiff-recovered-exposure01.ptstudio.json

副本保留Guard前已保存的exposure0.1，指向原授权DSCF0347渲染TIFF，RAW新增0。接续正常载入、必要编辑/撤销重做、保存、正常关/独立重开、实际两次TIFF导出、全行像素/位深/方向/ICC；2GiB/50ms与无强制GC不变。原433故障不能改标通过。原JPEG源行GUID每次保存会重建的边界不隐瞒。

全产品仍PARTIAL：新普通TIFF、部分普通空名发送/关闭边界/双root、真机尚未全部完成。手机token已过期401、TailscaleNeedsLogin，不用旧链接。历史短交接已完整归档history/，按证据ID取件，不全量重载旧日志。
