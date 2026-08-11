# 像素蛋挞在线选片 V1 桌面端与微信小程序规划报告

## 桌面端完成范围

- 在线选片已成为正式一级入口。
- 本地项目工作区支持项目创建、规则、照片、客户进度、最终结果与归档。
- 固定四个项目页签：照片、规则与发布、客户进度、结果与归档。
- 页面采用照片优先布局，不做运营 Dashboard。
- 本地 Workspace 支持多项目合并保存、重启恢复和损坏文件明确报错；不会把损坏 JSON 静默当空数据覆盖。

## 代理 JPG

- 默认 2560 长边、质量 85、sRGB。
- JPEG/PNG/TIFF 通过 WIC 解码；RAW 复用同一 LibRawDecoder。
- 代理编码不复制 EXIF、GPS、完整路径或内部备注。
- 使用 owned staging、CreateNew、AutoNumber、Flush；竞争者文件不会被误删。
- 导入前先持久化 Queued，成功或失败后再次持久化；重启后的 Queued/Processing 转为明确可重试状态。

## 上传与 Provider

- 已建立 `IOnlineSelectionProvider` 合同：创建、更新、上传、发布、查询进度、获取最终结果、取消发布和删除云副本。
- Release 默认实现：`NoneOnlineSelectionProvider`。
- 未配置时显示“在线选片服务尚未配置”，不崩溃、不白屏、不以 Fake 数据冒充生产服务。
- Fake Provider 只存在于测试项目。
- 上传队列领域模型支持排队、进度、失败、重试、暂停和恢复；真实云上传未配置。

## 规则、客户结果与归片同步

- 领域模型覆盖 SelectionProject、SelectionAsset、SelectionRule、SelectionChoice、SelectionComment、SelectionPublish、SelectionClientSession。
- 项目与素材状态有中文 UI 映射。
- 发布前验证项目、规则和 Ready 素材。
- 最终结果归档使用 CreateNew、AutoNumber、Flush，不覆盖旧归档。
- 归片同步只接受 RAW 扩展作为候选，避免 JPG/PNG 与自身误匹配。
- 本地结果归档和 Workspace 属于敏感业务状态；诊断包不包含这些文件。

## API Contract 与服务端骨架

- 合同：`docs/architecture/OnlineSelection_API_Contract_v1.md`。
- 项目：`src/PixelTart.SelectionApi.Contracts/`、`src/PixelTart.SelectionApi.Skeleton/`。
- 服务端骨架明确 `IsProductionConfigured=false`、`StartsLocalListener=false`。
- 桌面 Publish 不引用 API Skeleton，因此不会启动 localhost 服务。
- 安全规划包含随机 PublicId、可撤销 Token、Signed URL、访问有效期、可选 PIN、Rate Limit；客户侧不接触后台管理 Token。

## Storage 与安全边界

- Desktop Workspace、结果归档和代理 JPG 仅保存在应用数据目录或用户明确选择的目录；它们属于敏感业务数据，不进入诊断包、Publish 或安装包。
- 本地文件写入使用 CreateNew、AutoNumber、Flush 和原文件保留语义；删除云端副本的合同不得删除本地源文件。
- 生产对象存储规划使用私有桶、短时 Signed URL、生命周期策略和服务端审计；当前未配置真实 Bucket、密钥或生产 Token。
- 客户端仅接收随机 PublicId、受限会话和必要代理图，不暴露 RAW、本地路径、管理令牌或内部业务字段。

## 微信小程序 V1 可评审原型

- 目录：`prototypes/wechat-selection-v1/`。
- 页面：首页、图库、单图、已选、确认。
- 原型只覆盖选择、收藏、备注和二次确认。
- 不包含支付、商城、预约、优惠券、社交或会员功能。
- 客户侧规划不显示 RAW、本地路径、内部备注、后台状态、财务、工作人员和完整 EXIF。

## 未完成的生产依赖

- 微信 AppID。
- 生产服务器和数据库。
- 正式域名、HTTPS 证书和合法域名配置。
- 对象存储、Signed URL 与生命周期策略。
- 生产密钥、访问令牌、Rate Limit 和审计基础设施。
- 微信生产发布、合规审核与运维监控。

因此 Stage 9 Production Cloud Deployment 未进入，也不得把当前 API 骨架或小程序原型表述为已上线服务。

## 验证

- Online Selection Core 与 WPF 专项均通过。
- Provider None、代理文件所有权、损坏 Workspace、队列恢复、RAW-only 结果同步均有专项覆盖。
- Debug 全量：1922/1922。
- Release 全量：1922/1922。
- UI 证据包含在线选片首页、项目页和新建页。
- 安装版真实点击已验证在线选片入口和 Provider None；由安装二进制经实际创建/导入入口写入的隔离 Workspace、Asset 与代理 JPG 存在，`SelectionProxyReady=true`。
- 系统文件对话框在隐藏桌面未可靠关闭，因此项目页打开、四页签和结果同步仍为 `InstalledUiVerified=false`；没有用 fixture 冒充这些 UI 操作通过。

## 状态

- Desktop：`CodeVerified=true`，`AutomatedVerified=true`。
- API Contract / Skeleton：完成本地规划与无监听骨架，未部署。
- 微信小程序：可评审原型完成，未配置 AppID、未发布。
- `InstalledUiVerified=部分通过`
- `UserVerified=false`
