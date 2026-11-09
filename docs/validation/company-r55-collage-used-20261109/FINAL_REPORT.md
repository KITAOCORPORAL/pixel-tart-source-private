# R5.5 拼贴素材“已用”角标交付

2026-11-09，Asia/Shanghai。**“已用”角标闭环已验收，完整全模块诊断包已更新。** 验收范围是完整App的原生UIA和受影响回归；整体仍是完整范围诊断预览，不是全产品/设备/物理鼠标/DPI发行验收。

## 身份与最小实现

从收藏交付42476a1c1ab73f8aedf3ada973c8302efa83204f接续，已核验功能源码623d0a8c与该交付src零差。fetch没有更新收藏分支产品，独立工作树E:/Kitao-Worktrees/r55-collage-used-20261109，分支feature/r55-collage-used-20261109。开工清点67树，10树有脏状态，asset-library-v1有8个未推提交；全部保留，不reset/stash/clean或合并旧树。在线选片脏树只读取指定provider源码路径，未写入。

产品功能源码：**02702071e1eda7e8f51c806a7e1aeb8fa9bb18c2**。只改2个产品文件：现有FreeCanvasView.cs的缩略图模板及Changed接线，新增同目录FreeCanvasView.Usage.cs的派生集合与可见性converter。复用现有CanvasEditor.Changed、素材CanvasObject、AsyncThumbnail和列表；没有新建素材ID/导入流程/评分/收藏存储。

使用当前CanvasDocument.Objects中非空LibraryId+AssetId派生只读集合。重复实例、隐藏/锁定实例都计数；集合只判断是否至少存在一个对象。移除最后实例清除，撤销/重做按文档重新派生。选择变化不重建集合，变换不重建素材列表；切画布由新view的当前文档初始化，切库由新素材行的库身份匹配。派生状态不写入工程/收藏JSON，也不作为画布对象或导出像素。

## 真实入口验收

最终发布DLL的29份共享依赖已复制并逐项hash绑定到Core/WPF测试宿主。**Core16 + WPF相关回归14 + 三个独立完整App原生UIA进程3 = 33 PASS，0 FAIL，0 NOT_RUN（仅所选范围）**。

|场景|实际入口和检查|结果|
|---|---|---|
|首次加入|素材栏原生UIA选择→加入所选素材，真实缩略图及原生UIA“已用”可见|通过|
|移除最后实例|画布“编辑→移出画布”，角标消失|通过|
|撤销/重做最后删除|真实撤销/重做按钮，角标随1/0实例切换|通过|
|同素材多个实例|再次加入；只移出当前新增实例，另一个还在，角标保留；撤销/重做也保持|通过|
|保存与应用重启|原生名称字段/保存；独立进程从真实“画布→打开画布…”菜单恢复，角标重新派生|通过|
|切换画布|真实“新建画布”菜单空白时不标；回原工程再标|通过|
|取消收藏|真实收藏筛选/取消收藏，筛选项移除但画布对象仍1；回素材库仍显示已用|通过|
|切素材库|真实当前库/最近库/打开切换菜单A→B→A；另一库未用不误标，加入后标；回A新空画布不标，原保存工程标|通过|
|稳定身份与隐藏/锁定|同assetId不同libraryId不交叉；无素材ID对象不标；隐藏/锁定的剩余实例仍计入|通过，WPF状态测试|
|透明PNG与源保全|原生导出按钮及系统另存为，源hash不变、工程不含UsedMaterialKeys|通过|

完整App PID：**27932 → 4680 → 13068**。初始隔离库/确定性PNG及导入由现有完整App命令准备；被测收藏、素材栏、移出、撤销/重做、画布菜单、最近库切换、保存与导出动作使用原生UIA。不是直接调用Editor.Add/Remove来替代被测用户入口，也不是物理鼠标/DPI验收。OFFSCREEN截图只作布局观察，已目检缩略图右上角标，不冒充桌面截图。

最终PNG1920×1080，透明像素2,006,100，非透明像素67,500；SHA256 **3F4189A9750C1A8D174748A61E63D47B0D8936642144F0D081F2FCBE6F8C96CD**。原确定性PNG SHA010D792D...保持。无需读取任何真实RAW或私人照片。

## 完整诊断包

包：**E:/Kitao-Deliveries/r55-collage-used-20261109/Guangjing-R55-CollageUsed-Diagnostic-win-x64.zip**。

- 77,197,962 bytes；SHA256 **7CEC005C3E3455960059A341A6C115C479783CBB2F8187BD6A9FEDF884BF9FED**。
- 296文件逐项ZIP解压读回SHA一致（清单295项+清单自身）；win-x64自包含，无安装器，包内没有测试宿主、运行数据库、合成图片或证据目录。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全模块/策划15区/教程退役保持。三模块DLL齐全，10主导航+工具箱由发布EXE/CMD真实启动原生UIA读回，均正常关闭且退出0。
- 生产raw_r.dll保持F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9；没有混入A观测库/改RAW算法。本轮Open/Unpack/Process/MemoryImage均0。
- 入口Start-Guangjing-Diagnostic.cmd/.bat；默认数据根为独立%LOCALAPPDATA%/KitaoDiagnostic/R55-CollageUsed-02702071，可由明确的绝对隔离根覆盖；不打开日常数据。

早期构建缺System.IO、模板缺x命名空间失败、冒烟工具在Shell就绪前检查导航和附加进程退出码错误均保留。模板是实际产品问题，已在产品源码冻结前修正；后两项只修工具，不改发布产品、不把失败改写成最终PASS。最终Core/WPF构建保留既有警告，未宣称零警告；无构建错误。

## 在线选片核对与下一片

详细接口/真实App接线/调用矩阵和首片见ONLINE_PROVIDER_NEXT_SLICE.md及文件级SHA清单。正式App已有真实代理JPEG服务及JSON本地持久化；默认仍NoneOnlineSelectionProvider。仅接口声明不代表远端create/get、上传队列或结果刷新已接入UI。

下一片限定单项目/单张代理/单客户受保护入口/确认后回写当前桌面项目。可复用代理、队列、workspace、结果同步；需先固定后端API v1、鉴权、幂等、规则传输、发布凭证和确认结果协议。当前Publish没有发送SelectionRule，代理Ready也不等于云确认上传；必须分别补齐。可先开展DTO/adapter和真实桌面接线的受控契约测试，但真实联网验收仍依赖可用HTTPS测试部署/客户入口，不用mock PASS宣称服务上线。本轮provider/部署/服务请求均0。

HANDOFF与R55大纲按Asia/Shanghai更新。最终LOCAL_HEAD/REMOTE_HEAD和干净状态在普通推送后另存本地PUSH_READBACK，避免自引用SHA。所有旧工作树、未推成果和失败证据保留；收尾记录到1棵研究树在独立任务中推进，本任务未写入该树，不将其回退。A原生问题状态仍独立OPEN，不在本包声称修复。
