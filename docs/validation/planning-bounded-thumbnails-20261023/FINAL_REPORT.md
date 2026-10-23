# 策划项目自有有界缩略图性能交付

2026-10-23 Europe/Berlin。**限定自动功能/性能验证PASS；原生JPEG/副本保存退出新进程重开仍BLOCKED/NOT_RUN；整体PARTIAL，诊断版不能发行，无安装包。**

## 起点和产品
fetch及独立ls-remote确认company/planning-owned-jpeg-20261023=dcf907f4c8cb9b649db1af680e6ad8feb9e59be0，已读该分支报告及两处指定实现。新独立分支company/planning-bounded-thumbnails-20261023，新工作树E:/Kitao-Worktrees/planning-bounded-thumbnails-20261023。49旧树/423脏文件及旧数据根和诊断ZIP保全，无reset/clean/stash/rebase、main合并或force。

**产品提交e6ddb8d1018713fb48fbaa7e7cf3a3b23df0cc30**：11文件231增55删；证据另提交。不改ColorStudio、RAW、既有文档PNG往返。

## 产品实现
PlanningBoundedV2：所有自有来源先绑定当前项目/文档/引用/受管相对路径、拒绝重解析、1..64MiB/40MP/16384准入。OpenVerifiedRead用81920字节缓冲增量SHA256，同一个拒绝写删的可seek读句柄保持到解码完成，不再分配整份编码文件。缓存命中也走源副本完整hash与头准入，副本缺失/变化不回退外链/旧缓存。

列表/镜头缩略图最长边512；汇总2048；放大4096。WIC BitmapImage在EndInit前指定DecodePixelWidth/Height；DelayCreation只检查帧头，不先CopyPixels全尺寸。EXIF1..8只对有界像素整数置换，5..8宽高交换；原小图不放大。一个全进程decode gate限制并发峰值，工作在后台；取消在hash/头前后、WIC调用之后、方向行循环、缓存提交前后检查。WIC本机同步decode进行中不能抢断，但取消结果不发布。

新缓存目录PlanningBoundedV2，**编码PNG且扩展名.png**（旧版确为PNG字节却.jpg，旧缓存保留不用）。key含root/相对路径/项目/文档/引用/sourceSHA/尺寸版本，缓存本身size/hash/预期像素尺寸验证。原子stage+PNG SHA receipt写入，损坏缓存只重建确切key。CacheHit不会重编码或改写文件；仍校验源副本，不能作为原图备份。

刷新采用取消token+generation+project/document快照，先清空旧可观察图片、立即让视图清掉旧Image.Source而不重建正在编辑正文，后台逐项处理后一次性发布。项目切换在等待保存前使旧任务失效，旧任务不能写入新项目列表。同root不同项目隔离key，shot grid/抽屉/参考区优先用已验证冻结BitmapSource，不重新从路径解码缓存。放大预览也有取消/窗口请求代次和当前项目检查；关闭/切项目不会让旧完成任务重开图片。

不支持/损坏图保持“已保存但无法预览”，完整副本文件可访问但无不安全缩略图，项目仍可打开。PNG/JPEG白名单及EXIF/身份/大小/hash限制保留；其他格式不声称支持。

## 同机同夹具前后数值
最终冻结包对比基线发布产品4997c3059c5737405921d88ba6fd74eb2be2496e，使用完全相同test DLL（hash见FINAL_PERFORMANCE_RUNS.json），分别复制/绑定旧发布DLL与新发布DLL到新testhost，各3个独立进程，全部6/6 Passed。夹具：6张6000×4000/24MP合成PNG/JPEG、36镜头、实际JSON/SQLite；SHA清单LARGE_FIXTURES.json。采样5ms记录测量期间WorkingSet/PrivateMemory峰值，不是内核级绝对峰值；开源参考/生成工具只用于本地合成验证。

|中位数|优化前|最终优化后|
|---|---:|---:|
|冷打开|3220.8302ms|794.9298ms|
|热刷新|2878.1782ms|51.5776ms|
|切换空项目|36.8598ms|38.6723ms|
|测量期间采样峰值WorkingSet|565411840 B（539.2MiB）|131289088 B（125.2MiB）|
|采样峰值PrivateMemory|551608320 B|46440448 B|
|托管分配总字节|1175241840 B|8578112 B|
|源已校验缓存命中/缓存文件未重写|0/6、0/6|6/6、6/6|

冷打开约减少75.3%、热刷新约98.2%、工作集约76.8%；空项目切换未改善，原数保留。旧cache命中实际上仍全解码/重编码，故hit0。OS文件缓存未清空，未改系统配置；所有原始run.json/TRX/日志/测量保留PERFORMANCE_RAW.zip。数字是合成自动WPF场景，不是原生鼠标/真实摄影项目性能门禁，未通过挑最快/改阈值取得。早期开发perf2三次结果只历史对照，不取代最终六次。

首个perf-before-0失败因新增测试静态调用带CancellationToken签名，旧产品只有无参数API；无参数兼容重载恢复，用反射无参数入口绑定同二进制；该启动/前置失败及最初opt-in NOT_RUN保留，不算优化前有效成绩。

## 最终功能回归与图像读回

|范围|总发现|选中/实际|通过|失败|跳过|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|Planning/ProjectShot Core|1758|63/63|63|0|0|0/0/0|
|Planning/ProjectShot/JPEG/Bounded WPF|1745|100/100|100|0|0|0/0/0|

29发布DLL与testhost逐hash绑定、原始adapter多重身份覆盖无遗漏；新5WPF包含在100内，不重复计分。CacheHit仍验源、实际.png字节、缓存损坏重建、替换/缺失拒绝、EXIF有界尺寸、取消不发布缓存、并发刷新/快速切换、large4096/summary2048边界、保持源hash、旧RTF/DOCX/PNG回归均通过。无符号链接权限策略变更，不沿用旧成绩。

实际15区DOCX仍15个Heading1、4张媒体，镜头区1/参考区3；原小图JPEG EXIF6/8、PNG图像媒体逐像素匹配既有固定Pillow/ImageMagick参考，JPEG附件不嵌入。大图512px缩图独立双参考比较尺寸/方向一致，meanAbs8约1.188..2.536、maxAbs8最高37（不同缩采样核/codec的边界像素差），报告原数不称逐像素等价、专业ICC或RAW通过。详见DOCX_READBACK.json/BOUNDED_REFERENCE_COMPARISON.json/NODE_COMPARISON.md。ColorStudio/PV4/PV5/PV6零改动。

## 一次原生尝试
本轮最终EXE PID8508，新native/runtime隔离数据根，目标控件树可读，真实教程退出点击仍coordinate input geometry is unavailable。已明确阻点即停止，无重复截图/输入空跑。7实际加载组件hash与发布包匹配。JPEG原生预览、镜头自有副本保存退出新进程重开、真实点击项目切换、Word/WPS均NOT_RUN/BLOCKED；VM重建和自动WPF不算原生重启。

## 交付边界
完整Release win-x64自包含主程序WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，290应用文件/264运行资产缺失0，统一隔离UserData启动器。附合成JPEG种子/自有v1包、实际DOCX，不含测试DB/私人输入，不制作Setup。产品与证据分别commit/push，远端独立读回另附。

PNG WIC内部解码buffer是平台实现，峰值报告实测而非绝对上限证明；单次同步WIC不能强制中断，但不发布取消结果；缓存积累不做全局GC（不删用户目录/旧缓存），磁盘损坏/恶意并发置换仍按安全拒绝而非全面攻击模型认证。正式发行/原生闭环仍未完成。

## 最终诊断ZIP读回

332文件，75110198字节，SHA256 03a89ef57cbf7f91ec72c4f31e067381e704048ba64f158803339e25fa3830e3，全部条目hash读回一致，不含DB/UserData/私人输入。ZIP内报告为打包时快照，外部报告后附自身身份避免自引用。

## 正常推送读回

产品e6ddb8d1018713fb48fbaa7e7cf3a3b23df0cc30与独立证据f0753025f7ba32c6f7f35679047904d94bc24501已正常推送公司新分支；独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD，工作树干净。后续仅封存读回，不改变发布组件。
