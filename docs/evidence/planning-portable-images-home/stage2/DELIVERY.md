# 阶段2：真正根目录搬迁和故障恢复

合成项目同图两次独立ID插入正文，再在表格第二行第二格插入第三图片及图注，保存。把策划JSON与images整体复制到新临时根，关闭原编辑器保存并将原目录改名撤走，Assert原路径不存在后从新Store重开，不复制来源图库作为回退。

验证：三ID/哈希/实际表格位置与图注相同；基础/进阶切换；图片改标题/图注/宽度后撤销重做；富文本新增第四图后撤销重做；PlanningPaginator实际分页；DOCX四个真实ImagePart、表内Drawing及OpenXML Validator通过；导入内容保留图注；再次重开仍四图片且原根不存在。

旧绝对文档整体移动后，读取不改原JSON，主文件锁失败保留原字节与相对路径草稿，释放后新VM恢复草稿并提交迁移。缺图/篡改哈希明确指图片ID/标题；不猜同名文件。清单证明undo/redo与DOCX恢复副本仍保留，孤立副本只列UNREFERENCED_CANDIDATE_KEEP。

新增3项搬迁WPF测试与原6项托盘测试共9通过0失败0跳过。初次真实搬迁测试发现表内图注与后续Run合并导致EditDocumentImageAsync只改元数据不改正文，修复为在对应图片后、下一图片前的段落文本范围定位替换，支持跨Run；保留stage2-caption-failed.trx。未放宽图注/DOCX断言。

样本：artifacts/portable-evidence/legacy-before.json、legacy-after.json、relative-after.json和moved-synthetic.docx；只合成，绝对路径示例为临时测试根，不涉及私人资料。最终证据再输出脱敏结构/原始测试包，不提交副本图片或数据库。
