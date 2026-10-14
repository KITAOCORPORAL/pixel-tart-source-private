# R7 修复闭环：根因与证据

基线 8e3e203d71c10f8f89db650a8fabd516a597c957。产品修复源码 1793265990902aa54e0f62befc663e6b04ee83a8；后续a202374仅排除Python生成缓存，src/tests无变化。

## 1. DOCX覆盖失败

### 确定的观察
- 本次修复前直接运行和Start-Process隔离运行都复现页面合同用例失败，原TRX保留。不是“只隔离环境有问题”。
- 导出包的using已结束；失败之后目标/临时文件属性均Archive、无ReadOnly；运行身份相同。文件和目录ACL、SID、运行目录、TEMP、.NET版本随诊断保存。
- 失败后的独占ReadWrite/FileShare.None打开成功；说明检查时没有仍存活的冲突文件句柄，不能据此推断失败瞬间没有短暂读者。
- 同一失败目标上File.Replace立即成功，File.Move(overwrite=true)抛0x80070005。6次隔离诊断中捕获到同样差异；仅在失败时采集，避免前置探针延迟改变复现时序。
- 受控独立文件测试持有明确的Read/ReadWrite|Delete读句柄，稳定重现File.Move失败，File.Replace成功；旧读者仍能读到完整旧文件，新路径打开得到新DOCX。无需改ACL、去只读或吞异常。
- R7原发布KitaoPhotoSelector.dll替入相同测试host后，受控读者用例失败；修复源码组件通过。构建身份与hash记录在证据。

### 根因和范围
旧导出把“替换既有文件”实现为Windows rename-overwrite。该操作对目标读者的语义与ReplaceFile不同，导致即使读者允许Delete共享，覆盖也可能被拒绝。直接/隔离时序影响复现概率，不能把启动方式本身当根因。
修复使用File.Replace(temporary,target,null)替换既有目标，目标不存在时File.Move不覆盖创建；不先删除目标、不重试权限错误。原页面合同覆盖原文件断言保留。
自然失败瞬间的短暂读者归属未捕获：不将它归因于杀毒、索引器或OpenXML泄漏。进程内映射扫描为空；不声称已证明外部占用程序。已证明并修复的是错误的覆盖原语及可控同型复现。

### 安全验证
受控读者成功替换；FileShare.None锁定、ReadOnly、目录CreateFiles ACL拒绝分别抛IOException/UnauthorizedAccessException，旧文件逐字节相同，无*.tmp残留。测试ACL只作用于本次Guid临时目录，并在finally恢复。导出错误带目标路径与明确原因，异常向上传递，不吞掉。

## 2. 画布关闭缺Projects

夹具Fixture()通过真实DatabaseMigrator迁移root/product.db。旧用例构造AssetLibraryPage时仅传assets.db，没有传productDatabasePath。页面→ViewModel采用AppDataPaths.DatabaseFile，即隔离运行目录Data/pixel-tart.db；CanvasProjectsAsync→SqliteProjectRepository.ListAsync读取了另一份未迁移DB。
本次只读sqlite_master证据：5份夹具product.db有Projects；两个修复前运行目录pixel-tart.db无Projects。不是项目表迁移漏写。
产品启动App.xaml.cs先await ApplicationCompositionRoot.CreateAsync，后者先迁移默认数据库且迁移失败即抛出，再创建MainWindow；这是夹具绕开启动后路径注入错误，不是已证明的产品启动缺陷。
修复在该用例显式注入迁移后的product.db及独立online/tray路径；向SqliteProjectRepository写入真实合成项目，调用页面ViewModel.CanvasProjectsAsync核验该ID可见。原画布保存、两次Close请求、延迟关闭、无未处理异常断言全部保留。

## 3. 隔离运行器

- 由安装SDK的VSTest适配器发现TestCase（ID/FQN/DisplayName/Source），不扫描源码正则。
- 完整FQN等值OR表达式写入runsettings，不用~包含匹配；发现数与每个TRX测试名多重集、总计逐项一致，漏跑/多跑均失败。
- 每轮创建带UUID新目录，子进程每组独立temp/runtime；没有读取旧TRX。超时只杀本轮Popen PID及其子树。
- 非零退出、超时、TRX缺失、结果数不符、任何非Passed（含NotExecuted）均使总状态FAILED并退出1。
- 独立单元测试验证缺TRX、错误类名、跳过、正确精确匹配。真实运行的发现1610 WPF / 1689 Core记录另附。
- DLL构建完成才运行；不在正在执行目录替换组件。旧发布组件比较使用独立复制目录，不启动产品EXE。
