# 北尾鸲归片助手授权状态只读审计

审计日期：2026-07-30  
审计对象：北尾鸲归片助手 1.3.0  
审计方式：只读检查源码、发布目录、安装脚本、本机已安装文件、隔离安装记录、测试代码和商业版回传报告。除生成本报告外，未修改代码、授权配置或安装包。

## 一、最终结论

当前 Release 版没有连接任何真实生产授权平台。

当前 Release 启动时读取到 `Provider=None`，`LicenseProviderFactory` 因此返回 `UnavailableLicenseProvider`。它不是 `MockLicenseProvider`，也不是可工作的 `CryptolensLicenseProvider`，更没有 Keygen 或 Devolens 实现。

当前没有配置真实 Product ID、RSA Public Key、客户端验证 Token，也没有本地专业版授权凭证。因此，目前不存在能够真正激活 Release 版的许可证；在激活框中输入任何格式正确的代码，都会得到“授权服务尚未配置”，不会解锁专业版。

**当前没有真实激活码生成后台，必须先创建并配置生产授权平台。**

代码中已有 Cryptolens 适配器，因此若沿用现有架构，应在 Cryptolens 后台建立真实产品和许可证。当前不存在已建立的 Cryptolens 产品记录，Product ID 为 0；“北尾鸲归片助手 / KitaoPhotoSelector”只能作为建议创建的产品名称，不能视为已存在的后台产品。

## 二、13 项明确回答

### 1. 当前实际使用的 ILicenseProvider 是什么

实际使用的是：`UnavailableLicenseProvider`。

证据链：

1. `App.xaml.cs` 调用 `LicenseConfigurationService.Load()`；
2. 当前配置 `Provider=None`；
3. `LicenseProviderFactory.Create()` 对既非 `Cryptolens`、也非 `Mock` 的 Provider 返回 `UnavailableLicenseProvider("授权服务尚未配置。")`；
4. 本机已安装 1.3.0 的配置同样是 `Provider=None`。

因此当前运行时提供器名称为 `None`，状态是未配置的免费版。

### 2. Release 构建默认使用什么实现

Release 默认使用“其他实现”：`UnavailableLicenseProvider`。

- 不是 Mock；
- 不是已配置的 Cryptolens；
- 没有 Keygen；
- 没有 Devolens；
- 不存在第二个生产提供器。

`CryptolensLicenseProvider` 只有在以下条件全部满足时才会被创建：

- `Provider` 等于 `Cryptolens`；
- `ProductId > 0`；
- `PublicKey` 非空；
- `PublicValidationToken` 非空。

当前四项均未满足。

### 3. appsettings.license.json 的 Provider 当前值

当前值：`None`。

已核对位置：

- 源配置：`src/RAWSelectionAssistant/appsettings.license.json` → `None`；
- 发布配置：`artifacts/publish/win-x64/appsettings.license.json` → `None`；
- 本机已安装配置：`C:\Program Files\北尾鸲归片助手\appsettings.license.json` → `None`。

示例文件 `appsettings.license.example.json` 的 Provider 是 `Cryptolens`，但它只是空白示例，不会复制到发布目录或安装目录。

### 4. 是否已经配置真实 Product ID

否。

- 源配置 Product ID：0；
- 示例配置 Product ID：0；
- 发布配置 Product ID：0；
- 本机已安装配置 Product ID：0。

当前没有可报告的真实后台 Product ID。

### 5. 是否已经配置 RSA Public Key 或其他公开验证密钥

否。

- `PublicKey` 字段存在，但内容为空；
- 没有可显示的末尾四位；
- 没有发现其他生产公开验证密钥字段；
- Mock 使用的 HMAC 测试签名密钥由测试提供器实例随机生成，不是生产 RSA 公钥，也不会用于 Release 激活。

### 6. 是否已经配置客户端允许使用的验证 Token

否。

- `PublicValidationToken` 字段存在，但内容为空；
- 没有可显示的末尾四位；
- 没有发现 Keygen、Devolens 或其他平台 Token；
- 没有发现管理员 API 密钥、私钥或产品管理令牌。

### 7. 是否已经连接真实生产授权平台

否。

代码中存在 `CryptolensLicenseProvider`，API 根地址写为 `https://api.cryptolens.io/api`，并实现了 Activate、GetKey 和 Deactivate 请求。但当前配置不会实例化它，也没有真实账号、产品、Product ID、公钥或 Token 的端到端证据。

商业版回传报告也明确写明：

- 生产授权服务未配置；
- 当前 Provider 为 `None`；
- Cryptolens 尚未用真实账号联调；
- 不得宣称生产授权已经上线。

### 8. 当前是否存在能够真正激活 Release 版的许可证

否。

原因：

- Release 实际提供器 `IsConfigured=false`；
- `LicenseService.ActivateAsync()` 会在调用平台前直接返回“授权服务尚未配置”；
- 本机 `%LocalAppData%\KitaoPhotoSelector\License\license.dat` 当前不存在；
- 测试 Mock 激活码不会被 Release 启动链识别；
- 即使创建了一个 Cryptolens Key，当前 `Provider=None` 的 Release 也不会发送验证请求。

### 9. 开发测试激活码或 Mock 激活方式是什么

Mock 只通过自动化测试中的内存注入使用：

1. 测试显式创建 `MockLicenseDefinition`；
2. 将测试 Key 定义传入 `MockLicenseProvider`；
3. 将该 Provider 注入 `LicenseService`；
4. 使用内存授权存储、固定测试设备指纹和可控时间；
5. 可切换 `NetworkAvailable` 模拟在线、断网和离线宽限。

测试中存在显式注入的有效 Mock Key，本文只显示允许披露的末尾四位：`LMNO`。完整测试 Key 不在本报告中输出。

Mock 没有真实后台、没有持久账号、没有线上发码页面。`MockLicenseProvider` 的设备记录和签名密钥都在测试进程内存中；重新创建 Provider 后不会形成生产授权数据库。

当前桌面应用没有“开发模式 Mock 开关”。如果要让开发版 UI 使用 Mock，必须改启动注入或增加专用开发宿主；仅编辑配置为 `Provider=Mock` 不会成功。

### 10. Release 版本是否禁止 Mock 专业版

正常 Release 启动路径禁止 Mock 专业版。

证据：

- `App.xaml.cs` 固定传入 `allowMockProvider: false`；
- 没有传入 `mockProviderFactory`；
- `LicenseProviderFactory` 即使看到 `Provider=Mock`，也会返回 `UnavailableLicenseProvider("正式版本禁止使用 Mock 专业版权限。")`；
- 自动化测试 `FormalProviderFactoryNeverEnablesMockByConfigurationAlone` 覆盖了该行为。

需要说明一个边界：`MockLicenseProvider` 类仍编译在 Core 程序集中，并非通过条件编译从 Release 二进制移除。正常配置和正常启动链无法使用它；但如果攻击者修改代码、替换程序集或自行编写宿主，属于二进制篡改场景，不等同于 Release 默认启用 Mock。

### 11. 应该在哪个授权平台后台创建真实激活码

若不更换现有提供器，应在 **Cryptolens** 后台创建。

当前项目没有 Keygen/Devolens 适配器。建议的生产建立顺序：

1. 在 Cryptolens 账号中创建产品；
2. 建议产品显示名称使用“北尾鸲归片助手”，内部名称可使用 `KitaoPhotoSelector`；
3. 记录 Cryptolens 分配的真实 Product ID；
4. 配置许可证模板、有效期、试用、暂停和每个 Key 最多 1 台设备；
5. 创建只允许客户端执行激活、读取许可证和停用的最小权限 Token；
6. 获取用于离线验签的 RSA 公钥，私钥不得进入客户端；
7. 确认 Cryptolens 能生成或导入符合本软件格式的 Key：`KQGP-XXXXX-XXXXX-XXXXX`；
8. 生成测试许可证，先在隔离产品中完成真实联调；
9. 联调通过后再生成正式客户许可证。

由于当前还没有 Cryptolens 产品，无法给出一个“当前产品 Product ID”。当前值只能准确报告为 0 / 未配置。

### 12. 创建激活码后，软件是否可以直接验证

当前不能直接验证。

仅在 Cryptolens 后台创建 Key 还不够，至少还要完成：

- 把 Provider 改为 `Cryptolens`；
- 填入真实 Product ID；
- 填入 RSA 公钥；
- 填入最小权限客户端验证 Token；
- 确保生成的 Key 符合本软件强制格式；
- 重新 Restore、Release Build、Publish 和构建安装包；
- 用真实账号验证 Activate、GetKey、Deactivate 三个接口；
- 验证平台返回的 `licenseKey` 原始 JSON 与 `signature` 确实能被当前 RSA SHA-256 / PKCS#1 验签逻辑接受。

当前 `CryptolensLicenseProvider` 是未做生产联调的适配器。即使把字段填满，`IsCryptolensConfigured` 也只说明配置非空，不代表平台 API 合约、签名序列化、Key 格式和停用权限已经验证。因此，在真实联调通过前不能承诺“创建 Key 后即可直接激活”。

### 13. 当前还缺少哪些配置才能正式生成并使用激活码

必须补齐：

1. Cryptolens 生产账号；
2. Cryptolens 产品记录；
3. 真实 Product ID；
4. RSA 验签公钥；
5. 最小权限客户端验证 Token；
6. 与软件一致的许可证 Key 格式策略；
7. 单设备绑定规则；
8. 许可证有效期、试用、暂停和吊销策略；
9. 购买页 URL（不影响激活接口，但影响商业购买入口）；
10. Activate、GetKey、Deactivate 的真实 API 权限；
11. 平台签名响应与当前验签实现的兼容性验证；
12. 真实断网、90 天宽限、过期、停用和换机联调；
13. 将生产公开配置写入发布使用的 `appsettings.license.json`；
14. 重新发布 self-contained 目录并重建安装包；
15. 对最终 EXE 和安装包做代码签名。

禁止放入客户端：Cryptolens 管理员密钥、签名私钥、可创建/批量管理 Key 的 Token、支付密钥。

## 三、配置载体核验

| 位置 | Provider | Product ID | Public Key | 验证 Token | 结论 |
|---|---|---:|---|---|---|
| 源 `appsettings.license.json` | None | 未配置 | 不存在 | 不存在 | 免费版配置 |
| `appsettings.license.example.json` | Cryptolens | 未配置 | 不存在 | 不存在 | 仅空白示例 |
| 发布目录配置 | None | 未配置 | 不存在 | 不存在 | Release 使用此值 |
| 本机安装目录配置 | None | 未配置 | 不存在 | 不存在 | 已安装 1.3.0 实际值 |
| 安装包载荷 | None | 未配置 | 不存在 | 不存在 | 打包脚本复制发布目录全部文件 |

安装包核验依据：

- Inno `[Files]` 将 `artifacts/publish/win-x64/*` 原样复制到安装目录；
- 发布目录的授权配置为 `None`；
- 隔离安装日志明确安装了 `appsettings.license.json` 并成功完成安装；
- 隔离启动验收读取到的 Provider 为 `None`；
- 当前 `C:\Program Files\北尾鸲归片助手\` 下的 1.3.0 配置也为 `None`；
- 示例配置未进入发布目录和安装目录。

## 四、LicenseService 与 FeatureGate 实际行为

`LicenseService` 的实际启动行为：

- 本地没有凭证时，根据 Provider 是否配置决定提示；当前进入“授权服务尚未配置，当前为免费版”；
- 激活前先检查 Key 格式；
- Provider 未配置时不发送网络请求；
- 成功响应还必须通过离线签名验证才会保存；
- 本地凭证使用 DPAPI；
- 断网只在已有有效签名凭证时进入离线宽限。

当前没有本地凭证，也没有可工作的 Provider，所以离线宽限不会凭空产生专业版。

`FeatureGateService` 对所有 `LicensedFeature` 使用同一个判断：`licenseService.Current.IsPro`。当前 `Current.IsPro=false`，因此所有专业功能均被拒绝，免费功能保持可用。

## 五、Cryptolens 适配器现状与风险

已经实现：

- API 根地址；
- Activate；
- GetKey；
- Deactivate；
- Product ID、Key、MachineCode 和签名请求；
- RSA PEM 或 XML 公钥导入；
- RSA SHA-256 / PKCS#1 离线验签；
- 过期、暂停、设备超限和网络错误映射；
- 失败关闭，不因响应缺少签名而解锁。

尚未完成生产证明：

- 没有真实 Cryptolens 产品；
- 没有 Product ID；
- 没有公钥；
- 没有 Token；
- 没有真实生成的 Key；
- 没有真实 Activate/GetKey/Deactivate 测试记录；
- 没有证明平台签名覆盖的字节与 `licenseKey.GetRawText()` 完全一致；
- 没有证明 Cryptolens 后台生成的 Key 默认符合 `KQGP-XXXXX-XXXXX-XXXXX`；
- 没有生产吊销和换机流程。

因此，当前只能评价为“生产适配器骨架存在，尚未接通”。

## 六、商业版回传报告一致性

`回传给GPT_北尾鸲归片助手_1.3.0商业版报告.md` 与本次审计结论一致：

- 明确写明生产授权未配置；
- 明确写明 Provider 为 `None`；
- 明确写明 Mock 只用于自动化测试；
- 明确写明正式 EXE 不启用 Mock；
- 明确写明 Cryptolens 未做真实账号端到端联调；
- 列出了仍需提供的 Product ID、公钥、Token 和平台配置。

没有发现报告声称“真实授权已经上线”或存在可以发放的正式激活码。

## 七、最终状态表

| 项目 | 状态 |
|---|---|
| Release 实际 Provider | UnavailableLicenseProvider |
| 配置 Provider | None |
| Cryptolens 代码 | 存在 |
| Cryptolens 实际启用 | 否 |
| Mock 代码 | 存在 |
| Mock 在 Release 正常启动链可用 | 否 |
| Keygen | 未实现 |
| Devolens | 未实现 |
| Product ID | 未配置，值为 0 |
| RSA Public Key | 未配置 |
| 客户端验证 Token | 未配置 |
| 本地专业版凭证 | 不存在 |
| 可真正激活 Release 的许可证 | 不存在 |
| 真实激活码生成后台 | 不存在 |
| 建议沿用的平台 | Cryptolens |
| 创建 Key 后可否立即使用 | 否，必须先配置、重发版并真实联调 |

## 八、审计建议

在继续销售或发码前，应先完成一个独立的 Cryptolens 沙盒联调里程碑。验收标准不是“配置字段非空”，而是：真实后台创建产品和测试 Key，干净 Windows 机器成功激活，重启后 DPAPI 离线验证成功，断网进入宽限，在线 GetKey 成功，停用后本机退回免费版且同一 Key 可以在第二台测试机激活。

在上述闭环通过前，不应向客户发放任何所谓“正式激活码”。
