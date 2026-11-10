# 本轮代理协调说明

唯一运行上下文：本地E:/Kitao-Deliveries/chrome-capture-v1-20261110/CHROME_FIVE_CASE_ROUND.json；普通JPEG实际AssetId88490206、srcset实际AssetId486ffaf3已经存在，保留不重置。

在用户完成第一组反馈后，用real_chrome_round.py snapshot读回新增真实Chrome记录，按照CaptureId、PageUrl、ProjectId/BoardId、原始hash和关联增量核验。用户界面反馈通过feedback <step> <原文>留痕，不从协议探针伪造绿色提示。

步骤4：先原生正常关闭同版本桌面，保留Chrome。用户在关闭期间创建待处理并报告后，记录对应扩展弹窗URL中的CaptureId（仅浏览器元数据，不能访问扩展内容），用desktop-up恢复同数据根，通知用户同一弹窗刷新再发送。最终CaptureId应相同，原图hash相同，资产数量不增长。

步骤5：使用种子invalidProject指向的隔离“待失效测试项目”。在用户预选但未发送后，invalidate-target仅将该未使用临时项目IsArchived置1，不删除记录、不改A/B/板A。用户不能刷新目的地，应直接发送以验服务端失效保护。界面需报重新选择，库与其他项目关系无写入增量。

有失败先保留原始记录并修复，应用或扩展src变化后重建完整诊断包并重验普通JPEG。测试工具/协调说明不改变产品，不触发不必要的全仓重跑。实际Chrome证据与Native标准I/O探针分开。最终五项JSON只在全部UI反馈和桌面核验完成后签发并推送。
