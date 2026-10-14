# 托盘图片进入策划文档：数据与迁移合同

## 新数据
PlanningRichLayout.Images保存每次放置的PlanningDocumentImage：DocumentImageId、DocumentId、LibraryId+AssetId+ContentHash、CopyPath、CopyHash、Placement、Title、Caption、Width/Height、Order、ShotId以及正文block/row/cell位置描述。
同一素材多次放置生成不同DocumentImageId；图片不成为素材库所有权副本，也不修改托盘条目。新元数据为旧Version=1布局的可选集合，旧文档空集合默认兼容。

## 保存方式
- 新托盘图片转为文档PNG副本，路径位于PlanningProjectStore旁的images/<DocumentId>/<DocumentImageId>.png。
- SHA256验证来源字节与稳定引用相符，副本生成后记录独立SHA256。写同目录.tmp，Flush(true)，原子Move完成后才进入文档引用；异常清理tmp。
- 正文RTF只保存KITAO-IMAGE身份标记，Layout保存副本描述。新托盘图片不会作为pict Base64进入JSON。原有历史RTF嵌图不在本轮批量迁移；确认DOCX导入的图片被采纳为新文档副本。
- Capture/Apply恢复标记为真实InlineUIContainer，保留表格行列位置；历史快照携带Layout，因此撤销/重做仍能找到副本。序列化期间暂替换标记，不触发保存定时器递归。
- 图片副本先完整落盘，随后现有WriteDraftAsync写恢复草稿，再写策划主文件。主文件锁定时不替换旧文件，完整副本及恢复草稿保留供重试。
- 正常取消不写副本；插入准备期间取消/切项目/旧视图回调会清除本批未引用副本。副本失败不写新引用。确认DOCX导入资源生成失败清除本批已创建文件，不应用新文档。
- 副本目录不是缩略缓存。撤销后副本保留以支持重做/恢复，当前没有自动垃圾回收。突发进程崩溃可能留下完整但未引用PNG，不能称跨文件ACID事务；不会留下可见的半张图引用。

## 本地与跨电脑
本地策划JSON与images目录需要一起备份；CopyPath当前为绝对路径，没有实现本地工程移动后的自动路径重写。独立交付给没有素材库的WPS电脑使用导出DOCX，它包含真正ImagePart，不依赖本地PNG路径或素材库。

## 两模式
基本版选封面/参考图/情绪板/指定镜头时直接向同一正文写图片段落、可见分区标题及图注，不只是添加参考图库。镜头绑定存ShotId，并写镜头名称标题。基本版提供标题/说明、宽度、排序、撤销/重做；进阶版从同一RTF/Layout恢复，按当前正文光标或表格单元格插入，图片管理可调整尺寸与元数据。
基本版排序为图片段落移动；表内图片拒绝跨单元格排序，要求进阶正文编辑。自由文档编辑可改正文图注；元数据编辑入口写对应图注文本。不是任意Word锚点/浮动图片布局系统。

## 来源解析与重新关联
共享实际AppDataPaths.Root/InspirationTray/tray.sqlite，列当前托盘及ProjectId匹配收藏，搜索、多选、预览、取消。不调用清空/移除/排序托盘操作。
实际主壳接ResolveStableAssetAsync并传完整哈希；其他库未显式打开时报告Offline，不猜测其他库路径。Available才复制，读到字节再次SHA验证以防解析后变化；Offline/Changed/Ambiguous/Missing均停止对应项插入。
来源重新定位必须点明确入口并确认；只更新来源定位，不替换已保存文档副本。没有自动拉取源文件更新。

## DOCX
已保存副本加载为Bitmap，导出ImagePart及原生drawing；图注保留正文，描述属性存文档图片ID/标题/图注/位置标签，不导出私人绝对源路径。
导回同一文档且ID已存在、图像副本哈希一致才保留原稳定来源；外部/改图/未知ID作为“文档内图片”采纳为新副本，不信任外部文件声明的库身份。外部DOCX清除描述属性时图片与正文位置仍导入，但旧库身份不可保证。
图片顺序/标题/说明/表格位置自动验证；WPS可能改写描述、编码或结构，未实测，不承诺完整WPS高保真身份保持。
