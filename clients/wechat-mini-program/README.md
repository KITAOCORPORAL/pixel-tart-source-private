# 微信小程序 Online Selection V1 Mock

这是五页的本地原生小程序原型：`project`、`gallery`、`photo`、`selected`、`confirm`。

- 所有请求经过 `services/api.ts`；页面不直接调用 `wx.request`。
- `services/selection-store.ts` 保留本地选择、收藏、备注和可重试请求队列。
- `api.baseUrl` 默认为空，因此默认是 Provider None / Local Mock，不宣称云端或生产上线。
- 微信 `wx.login` 只产生临时 code；正式实现时由服务端调用 `code2Session`，AppSecret、SessionKey 和长期 Token 不进入小程序代码。
- 正式网络必须使用 HTTPS 与微信后台配置的合法域名。本目录不包含 AppID、密钥、客户资料或真实照片。
