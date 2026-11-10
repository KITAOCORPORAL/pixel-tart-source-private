# 在线选片整合与 Safari 验收

产品基线8e447075；选择性移植c36e1f38确认回执校验和响应丢失恢复。最终产品源7769b8ee63ad46cd22efb154145766c45b34ba69，Core无HTTP/凭据断言不变。完整模块/策划15项/教程退役保持。原始181de897候选与失败收据保留，不能混入最终版本通过计数。

实际本机验收使用发布DLL绑定后的原生App TestHost、真实HTTP服务、独立故障代理和实际客户JS；DOM adapter不当作Safari证据。首次强制重开导航暴露旧SQLite没有持久化反向选片ID，现已用schema8加法迁移、读写映射和唯一索引修复；旧库升级走原备份机制。旧无反向身份记录不根据项目名猜测补绑。

完整包Start-OnlineSelection-LocalTest.cmd是独立本机测试入口。待用户操作的HTTPS相册使用同一最终包及独立phone-service/phone01数据根；分享链接和连接凭据仅存在本地交付目录，禁止提交或打入包。不要在phone01相册自动点击选择或确认。

收到用户真实Safari成功反馈后，在私有交付根记录PHONE_HUMAN_FEEDBACK.json（success=true、原文userText、deviceBrowser="iPhone Safari"），执行：

```text
python tools/online-converged/collect_phone_feedback.py E:/Kitao-Deliveries/online-selection-converged-20261110
```

它先核对部署字节和服务器已保存确认，再通过同版本原生PC入口收回结果、独立进程重开并实际回到唯一对应归片项目。只有全部通过才产生PHONE_FINAL_RECEIPT；随后把脱敏最终回执和报告提交推送。用户只报告打不开或未确认时不得写成功。

当前状态看docs/validation/online-selection-converged-20261110/PRE_PHONE_REPORT.md。家庭旧c36e1f38相册与整合版证据单列。浏览器扩展继续等待整合版首轮人机验收通过。
