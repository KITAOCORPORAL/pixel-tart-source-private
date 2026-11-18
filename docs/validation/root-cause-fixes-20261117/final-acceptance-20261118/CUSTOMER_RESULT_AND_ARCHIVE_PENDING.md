# 最终433已发布、客户锁定及PC读回

本轮普通最终433确认只有1个相册42af2986-48c3-4b11-907e-532bb6acc6a6，WorkflowProjectId指向原项目08d18374-fb96-4efb-9451-cae9b8c48c40。两张持久编辑PNG均有源hash/编辑版本侧车，代理上传普通按钮实际执行，页面服务确认2/2；普通“发布项目”显示服务已发布。

通过真实Chrome页面依次点击两张checkbox，再点击确认并锁定。客户页显示已选2/2、服务器确认v1/r6，两个checkbox与确认按钮锁定；截图CUSTOMER_CONFIRMED_FINAL.png不含凭据。没有用API写入客户选择或将旧确认改标为新版本。

PC普通“刷新客户结果”完成，页面显示客户确认2张已校验保存、服务器回执v1/r6；本地FinalResults唯一且IsLocked=true。结果ID、所选ImageId、ConfirmedAtUTC、项目/来源与编辑侧车核对见CUSTOMER_LOCK_AND_PC_REFRESH.json。

随后普通“交付结果→同步归片工作区”打开标准“选择选片结果归档目录”。此时文件夹确认未观察到，项目SourceOnlineSelectionProjectId仍null、SelectionInputs为空，因此绝不把同步归片/收回原项目标PASS。当前系统对话框默认位置已是本轮output。

本人需在最终433的“选择选片结果归档目录”窗口选定：

E:\Kitao-Deliveries\root-cause-fixes-20261117\acceptance-433a1d8e-20261117\output

然后点“选择文件夹”。若看不到窗口，先从任务栏恢复本轮最终433光镜，勿操作旧eef候选。路径可直接选择当前已显示output，不需新建或覆盖旧文件。

当前普通链通过到“客户锁定+PC刷新保存结果”；原工作区收回、JPEG导出、保存关闭、独立进程重开、新关闭协议TIFF仍待后续正常UI。空/空白项目名的发送前拒绝、两最终DataRoot普通实例及真机也没有提升为通过。手机仍NeedsLogin且当前旧凭据已过期；没有复用过期登录链接或默默轮换。完整产品状态PARTIAL。
