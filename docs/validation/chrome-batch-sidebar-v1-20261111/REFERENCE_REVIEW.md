# Image Harvest提取、筛选与分批交接复核

前轮只登记功能参考，没有实际固定源码；本轮补齐，不把旧记录声称成已审查的源码。固定zbw-zbw/image-harvest@c87e23059ac153c7bf166ad677182b3ac5afb4d0，package1.2.7，MIT，Copyright(c)2026 kyriewen。完整MIT和固定引用随扩展分发，逐文件blob/hash见IMAGE_HARVEST_REFERENCE.json。

| 固定入口 | 复核与本轮适配 |
|---|---|
| content/utils.ts parseSrcset / pickBestSrcsetUrl | 候选解析与最高声明尺寸；继续使用已验收光镜parseSrcset严格w/x规则，不换成简单split导致歧义回退 |
| content/extract-advanced.ts extractLazyLoadImages | 读取data-src/data-original/data-lazy-src/data-hi-res-src/data-full-src及data-srcset；img/picture范围的惰性属性子集已标注适配来源 |
| sidepanel/filter.ts filterBySize/filterByType | 尺寸/格式独立筛选；原图未下载时不拿页面缩略图natural尺寸假称原图，未知尺寸明确保留/排除 |
| content/batch-sender.ts addToBatch / addManyToBatch | 原实现50条metadata/100ms缓冲用于减少IPC风暴；本轮500元素上限/轻量标题及扫描队列、UI1.2s状态节流，原图串行逐项Native请求，避免50份原图驻留 |
| shared/export-eagle.ts detectEagle / exportToEagle | 原实现按10个URL一批及HTTP状态累加；本轮不能复用该成功判定，必须按每张的Native实际AssetId/hash/目的地回执标记成功 |

没有引入Image Harvest云服务、收费校验、遥测、自动滚动或像素转换。侧栏UI为独立实现，不复制其PReact/商业服务依赖。扫描只根据当前页DOM属性和已解码同源像素生成小预览，跨域画布受限显示占位，不为预览抓全部原图。元数据推断格式/尺寸与桌面实际字节验证分开。

Chrome官方Side Panel API使用工具栏用户手势打开；扫描是另一个明确点击。只新增sidePanel权限，其余沿用contextMenus/activeTab/scripting/storage/nativeMessaging和仅在选择发送时请求实际图片源域名。没有tabs/history/cookies/downloads/webRequest或持久全站内容脚本。
