# 本轮基线、提交与保护范围

实际工作树 N:/pixart/kitao-planning-workflows-20261013；分支feature/kitao-planning-workflows-20261013；fetch基线0766207a7e3bc4affc3c28374182ee7277bb54e9。新建独立工作树，不改旧树、不强推、不合并main。

|阶段|代码与该阶段证据提交|推送顺序|
|---|---|---|
|1 摄影资源/灯位/15入口基础|b7d1de7e15d1ecaef0ce612d5d3ca2098a3789cc|专项11通过后推送，再开始2|
|2 同文档模式与结构引用|80c53b15b6c5cf5ece8c29b2e738b3dc03091865|专项15通过后推送，再开始3|
|3 有限DOCX自动闭环|6b3cfc9b91e28675a1b57f59ba846e4de7ea217c|最终专项19通过后推送，再封存Release|

前两阶段的窗口布局和结构接口在第三阶段复核补充生命周期：失效编辑器不能卸载回写；结构未提交字段在切换前收集；程序化导入/撤销不被旧RTF覆盖；预算跨项目只显示本项目结果；模式落盘失败时恢复草稿保留旧模式。

未修改Color Studio的PV4/PV5/PV6数学、ProfessionalRaw、PixelTartFilm、历史渲染器。MainViewModel只加只读账本委托接线；FinanceViewModel只加只读查询方法，没有布局或账本写入变更。其余Grok页面不重排。

没有引入外部包、外部图片/纹理、客户照片、数据库、凭据或研究原件。测试数据全部临时合成，不提交。构建与测试host留ignored artifacts。参考资料SHA见stage1/REFERENCES，原型仅作为本地文本资料，未执行Browser或WebView。

测试生成的历史ui-guardian报告先复制到ignored artifacts/workflows-generated-reports再恢复本工作树基线；不回写其他树。旧源码树的三个未跟踪审计/研究目录仍在。

# 文件类型

新增/修改C#：强类型Resources/Presentation、VM资源与复合快照/模式/DOCX、View资源/模式/DOCX接线及测试。
修改XAML：仅PlanningCenterView内部导航与模式按钮。
Markdown/TXT/JSON：阶段交付、文件列表、参考哈希、15区逐项差异、人工门禁、Word后续依赖与人日、测试摘要、发布组件/全目录哈希。完整路径类型与SHA见FILE_INVENTORY。
