# 功能诊断工具

产品源码与状态见CURRENT_RECEIPT.json。保持原项目、预约和手机变更暂停标记，不把自动检查或浏览器元数据当真人手机或相机验收。

- Refresh-FullProductDiagnostic.py --context <本地上下文>：先核对既有索引，再只读检查原凭据、SQLite、服务、Tailscale和包完整性。已知回执更新须先保留原字节，再按明确版本更新索引。
- Continue-CompanyMobile.py --context <手机上下文>：默认只读；本人入网后加--apply，核对原tailnet、公司Self DNS、独立端口和可信HTTPS。完整凭据链接只写runtime/PHONE_URL.txt。
- Native-UiStep.ps1：通过已安装OCU的文档化CLI操作指定正常App，逐步获取新状态并核对PID，不直接使用PowerShell UIA。只保存目标App AX文本，不保存可能混入其他窗口的桌面图。
- Update-R2Evidence.py、Assemble-FunctionalDocumentation.py：分别绑定冻结检查/正常EXE回执，生成42行表及主线边界。
- Finalize-FullProductDiagnostic.py：补全独立服务和启动入口；Verify-CompanyRuntime.py验证冷启动后的双shell复用和7项合成时区观察HTTP分支。
- Seal-FunctionalDiagnostic.py、Verify-FunctionalDiagnostic.py：封包及逐字节验证；封包后校验/远端读回收据不嵌入自己的压缩包。

原生主线未完成、手机和相机外部条件必须明确交接。不要提交或打包PRIVATE文件、数据库或本人授权链接。
