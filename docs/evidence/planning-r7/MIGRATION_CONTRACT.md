# R7 迁移与数据合同

源基线：62f06f3b5d9691e605b88c8944346bb96d59a3ac。产品冻结源码：9446c1997ea88575072f0f9a8b72e4bc95eb8687。

## 向前兼容
- PlanningRichLayout 保留 Version=1，增加 HeaderFooter 可选属性；旧 JSON 缺失时使用全空/不显示页码默认值。未改项目身份、镜头身份、R6 草稿存储位置或恢复点格式。
- PlanningBlockSemantic 增加 RepeatHeader，默认 false；随 Layout 而非仅 FlowDocument.Tag 存储。HeaderFooter 由 PlanningDocumentLayout.Capture/Apply 携带，RTF负责正文，Layout负责页面附加信息。
- RichDocumentHistory 的同一 RTF+Layout 快照覆盖正文、页设置、表格和页眉页脚。页眉编辑“取消”不调用持久化，应用通过一次历史动作；旧编辑器仍有R6代次/退休保护。
- 旧应用回读R7新增字段不是本次保证；保留原件和恢复点，不能用旧应用保存覆盖新页面语义。

## 纸张导入
Grok D-PL-11 / CR-PL-09：一节A4、保留方向，左右边距乘目标宽/原宽，上下乘目标高/原高。A4纵向11906×16838 twip，横向交换。Letter/A3不沿用原尺寸；原件A4不报转换。无效尺寸或边距拒绝导入。多节保留最后节几何/页面附加内容并明确报告折为单节；不是完整分节支持。

## 查找替换
仅普通正文，包括段落内跨Run、表格单元格和嵌套列表。段落、单元格之间不拼接匹配；查询/替换不包含换行。大小写采用Ordinal/OrdinalIgnoreCase；全词以Unicode字母数字、组合标记和下划线作为词边界，中文连续词串按同一边界规则，不含分词器。
替换保留未修改Run文字和格式；替换字符串使用命中首Run格式。全部替换逆序执行且仅一个历史动作；零命中/文字不变不新增历史。普通正文Ctrl+F/H与列表搜索按焦点路由。
摄影结构字段未纳入批量替换；仍走分镜表/检查器的StageShot/Flush及已有结构同步合同。没有伪造“包含结构字段”选项，也未宣称完成跨正文+镜头的复合批量撤销。

## 表格
- 通过占用网格验证合法矩形，拒绝空洞/重叠/不规则区域。合并按行优先顺序搬移原Block，拆分保留全部内容在左上格并补空格，不猜测如何重新分摊内容。
- DOCX原生gridSpan/vMerge restart/continue；导入恢复ColumnSpan/RowSpan，校验延续格跨度和额外文本，不能默丢延续格内容。
- 当前只支持单RowGroup。纵向合并表增删行列先拒绝并要求拆分。表内列表、图片块、嵌套表格不做无损承诺：导入前拒绝；导出写临时文件、失败不替换目标。部分行内图片可能导出但导回仍拒绝，不能称无损。
- 重复表头保存到Layout，DOCX写tblHeader。预览/打印/PDF使用PlanningPaginator，把普通无纵向合并表测量成多个页段，每段含真实复制表头行，非覆盖绘制。表头表强制独立起页，可能额外留白，与连续正文布局有差异。
- “重复表头＋纵向RowSpan”“单行高于一页”明确拒绝；复杂嵌套表头和任意分节仍未覆盖。Excel公式/工作簿未增加。

## 页眉页脚及输出
单节纯文本页眉、页脚、独立首页、起始页码、首页隐藏页码、固定“第N页/共M页”页脚。每字段单行最多300字；无奇偶页、左右位置、任意字体、品牌/日期token或复杂域编辑。
预览/打印/PDF以同一个PlanningPaginator预留上下空间绘制，不覆盖正文。打印验证到XPS序列化；真实打印机和Microsoft PDF驱动实操NOT_RUN。内置PDF仍为图像式PDF，不是可选中文本。
DOCX原生header/footer references、titlePg、pgNumType和PAGE/NUMPAGES简单域。导入保留本合同，遇复杂域/图片/奇偶页明确报告。原生Word/WPS分页与域更新未实操，NUMPAGES缓存值依赖外部软件更新；不是完整Word保真。
