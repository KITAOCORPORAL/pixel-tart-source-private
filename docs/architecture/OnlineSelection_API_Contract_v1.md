# 像素蛋挞在线选片 API Contract v1

## 状态

- 本文是评审用合同，不是生产部署记录。
- Desktop Release 默认 `Provider=None`；未配置服务时本地原型继续可用。
- 生产云需要独立提供微信 AppID、HTTPS 域名、服务器、数据库、对象存储和凭证。
- Desktop 不启动 localhost 服务，也不持有管理端 Token。

## 拓扑

`Desktop → HTTPS Selection API → 数据库 / 对象存储 → 微信小程序`

上传内容默认只有代理 JPG 与最小项目资料。RAW、XMP、PSD、策划、协议、授权书、完整联系人、工作人员资料和本地路径不上传。

## 认证与访问

- 摄影师端：短期访问令牌，服务端按账户和项目鉴权。
- 客户端：随机 `publicId`、可撤销客户令牌、可选 PIN、过期时间与速率限制。
- 图片：短期 Signed URL；不暴露存储管理凭证。
- 日志：不记录完整本地路径、文件内容、客户电话、客户令牌或 PIN。

## 领域对象

- `SelectionProject`
- `SelectionAsset`
- `SelectionRule`
- `SelectionChoice`
- `SelectionComment`
- `SelectionPublish`
- `SelectionClientSession`

## HTTP 端点

| 方法 | 路径 | 用途 |
|---|---|---|
| POST | `/v1/selection-projects` | 创建项目 |
| PUT | `/v1/selection-projects/{projectId}` | 更新项目 |
| GET | `/v1/selection-projects/{projectId}` | 读取摄影师端项目 |
| POST | `/v1/selection-projects/{projectId}/assets` | 建立代理图上传会话 |
| POST | `/v1/selection-projects/{projectId}/assets/{assetId}/complete` | 完成并校验上传 |
| DELETE | `/v1/selection-projects/{projectId}/assets/{assetId}/cloud-copy` | 只删除云端副本 |
| POST | `/v1/selection-projects/{projectId}/publish` | 发布 |
| POST | `/v1/selection-projects/{projectId}/unpublish` | 撤销发布 |
| GET | `/v1/selection-projects/{projectId}/progress` | 读取选片进度 |
| GET | `/v1/selection-projects/{projectId}/final-selection` | 读取客户最终结果 |
| GET | `/v1/client/selection/{publicId}` | 客户项目首页 |
| GET | `/v1/client/selection/{publicId}/assets` | 客户图库 |
| PUT | `/v1/client/selection/{publicId}/choices/{assetId}` | 选择/收藏 |
| PUT | `/v1/client/selection/{publicId}/comments/{assetId}` | 客户备注 |
| POST | `/v1/client/selection/{publicId}/confirm` | 二次确认并提交 |

## 上传合同

1. Desktop 先生成 2560 长边、质量 85、sRGB 的代理 JPG。
2. 请求上传会话后，直接通过 Signed URL 写入对象存储。
3. 完成端点校验长度、媒体类型与服务端对象状态。
4. 单张失败保持 `Failed` 并允许重试；不会让整个项目失去反馈。
5. 云端删除只修改云对象与 `SelectionAsset` 状态，绝不删除本地源文件和代理文件。

## 发布门禁

- 项目有效。
- 至少一个 Asset 为 `Ready`。
- 数量、截止、有效期、加选和锁定规则有效。
- 上传失败的图片必须在摄影师端明确列出。

## 最终结果合同

每项至少携带：

```json
{
  "selectionProjectId": "uuid",
  "imageId": "uuid",
  "originalFileName": "IMG_0012.JPG",
  "selected": true,
  "favorite": false,
  "customerNote": "请保留这张",
  "extraSelected": false
}
```

Desktop 依靠稳定文件名/ImageId 匹配 RAW，不依赖云 URL。客户已确认映射为“待返图”链路，不自动变成“已返图”。

## 生产停止点

本文和仓库骨架不包含真实域名、AppID、管理 Token、对象存储密钥或数据库连接串。取得这些外部资源并完成安全评审前，不进入生产发布。
