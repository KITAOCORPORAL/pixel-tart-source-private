# 编辑版本与导出

StudioEditVersion 保存版本 ID、名称、创建时间、LibraryId/AssetId/hash、Look、Stack、Film、Engine、ExecutionMode；ProcessingVersion 来自该 Stack。

旧 v1 会话继续读取，首次保存/创建版本时使用 Target.Id 作为默认版本 ID。创建版本复制参数；切换先保存当前快照，取消活动计算并增加 activation revision，然后使用既有载入链。编辑历史身份使用 ActiveEditVersionId，避免不同版本共用历史。界面提供新建、循环切换和原保存入口。

StudioExportReceiptStore 在输出文件存在且编码成功后计算 SHA256，以临时文件+原子移动写入 .kitao-export.json。清单包含源身份、EditVersionId、ProcessingVersion、格式/阶段、输出路径/hash。现有路径不覆盖；已存在输出或 sidecar 时该项失败。取消/编码失败不会产生成功清单。文件成功但清单失败时输出可能保留，UI 报失败，不能认作整体成功。

自动测试：独立版本曝光切换、会话重开、身份校验、清单拒绝缺失输出。既有批量同步/导出回归通过。限制：版本命名/删除管理、跨会话独立版本目录、完整发布配方快照和在线选片交付链未完成；当前版本存于 .ptstudio.json 中。异步旧图保护复用既有 revision/cancel 机制，未做本轮长时间手动拖动实测。
