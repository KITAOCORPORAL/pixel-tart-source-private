# 本轮逐模块清单

2026-10-26 America/New_York；PASS仅指本轮自动路由/绑定/相关回归，不是原生业务或发行。

|模块|实际入口|删改|保留原因|验证范围|
|---|---|---|---|---|
|工作台|Workbench|移除任务常驻教程，最近项目空态缩短|保留任务/历史状态和新建命令|PASS route/related tests|
|归片/分片|Workflow / LocalSplit|压缩重复步骤说明|保留来源JPG规则、冲突/未找到、客户JPG限制|PASS route/related tests|
|日历|WorkCalendar|删除标题下功能复述|保留日期/地点/金额单位/分页|PASS route/related tests|
|策划|Planning|单主导入+Auto行；次要入更多；短ID；重复标题/空块收紧|保留完整ID详情、外链/副本、包排除清单、保存失败和取消|PASS routes/narrow125-150%logical|
|素材库|AssetLibrary|索引/分析/批量提示缩短|保留元数据不改源文件、分析前置条件|PASS route/accessibility/layout tests|
|灵感板/自由画布|AssetLibrary / planning canvas open|短错误+详情/日志；布局预览事件合并|保留owned身份校验/显式重载/不回退；v1不含画布|PASS owned/queue regression|
|暗室/Color Studio|ReferenceColor|短只读/撤销提示与中文版实现术语|布局和命令不重排；非相纸标定/默认关闭研究不变|PASS route/relevant layout; authorized photo NOT_RUN|
|联机|Tether|空态与监看范围说明收紧|保留顶层监看/源文件不改/未连接状态|PASS route/startup tests; physical camera NOT_RUN|
|在线选片|OnlineSelection|本地/云服务边界直述|未配置云服务仅本地，不冒称在线|PASS route/persistence tests|
|收支|Finance|本地范围/附件删除说明缩短|保留金额币种和非专业税务工具限制|PASS route|
|历史|History|空态短提示|保留免费/专业历史限制|PASS route|
|工具箱|Toolbox|去宣传和实现解释|Preview仍不可用，未重启隐藏模板|PASS route/policy/command tests|
|发布|Publishing|合并重复安全输出说明|格式位深/配方优先/ICC选择/不覆盖保留|PASS route/render tests|
|RAW转JPG|RawToJpeg|无像素或引擎修改，仅核对入口|旧拒绝/保护行为不变|PASS route; real RAW NOT_RUN|
|整理/拼图|PhotoGrouping / Collage|教程重复收紧|保留源文件/冲突编号、原图导出；真实源保全回归|PASS route/command/3export tests|
|压缩/资料弹窗|BatchCompress / BookingDocuments|缩短复述安全说明|保留独立目录/无覆盖、宏脚本不执行、绑定命令|PASS route/contract|
|设置|Settings overlay|重复说明移除、530px高度收紧|即时保存/字段/关闭可访问名保留|PASS route1180/1920; logical checks|
|帮助/授权|Help / Activation|正常帮助保留；输入诊断组仅诊断gate显示|保留教程入口/反馈隐私/激活状态|PASS route; actual activation NOT_RUN|

隐藏旧水印/FTP/批量重命名等模板未计清理完成；ProductToolboxPolicy当前生产/Preview门禁和主导航证明实际范围。
