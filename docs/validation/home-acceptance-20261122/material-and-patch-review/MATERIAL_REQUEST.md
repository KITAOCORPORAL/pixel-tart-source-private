# 最小公司材料取件清单

当前状态：**待材料**。本轮只复查 MATERIAL_CHECK.json 中三个准确历史文件路径；均不存在。四个既有任务上下文中没有材料传输/收件位置配置，未新建连接、遍历图库或扫描其他磁盘。公司完整EXE虽也不在原路径，但家庭已有同源码完整包，所以取件只需要下面两份文件。

| 必取文件 | 历史授权位置 | 文件 SHA-256 |
|---|---|---|
| `DSCF0347_仿色.tif` | `E:\Guangjing-RawColorChain-20261103\pass4\company-02\default-export\DSCF0347_仿色.tif` | `0da1bf38e42b0b76aee28f2b9a0af013ceff4cb9c46102128732d0626d667c6f` |
| `tiff-recovered-exposure01.ptstudio.json` | `E:\Kitao-Deliveries\root-cause-fixes-20261117\tiff-d07e4f43-20261119\output\tiff-recovered-exposure01.ptstudio.json` | 见下方两条历史观测，当前磁盘摘要待取回核实 |

## TIFF 的已知完整身份

- 7752×5184，RGB48，每通道16位，方向1。
- 全部5184行RGB48像素 SHA-256：`5be9b355f579cfdc2aea9fcfec8e0208a2489b605e85db84e6bce688f63fb8aa`。
- ICC 3144字节，SHA-256：`2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e`。
- 证据：`docs/validation/tiff-srgb-company-20261116/RECEIPT.json` 的 companyInput，以及 `root-cause-fixes-20261117/final-acceptance-20261117/AUTHORIZED_TIFF_CURRENT.json`。上述是历史已测指纹，本轮未拿到文件，不声称重新解码原件。

## 恢复文件摘要存在历史分歧，不能替它选定一个“当前值”

| 历史回执 | 文件 SHA-256 | 可确认范围 |
|---|---|---|
| `retained-preview-followup-20261119/ORDINARY_CURRENT.json` | `474f6d83a3d7b5d5d0008499fff3d43e2c8216108c11e537049d0b9166ed19fb` | 同一文件路径；回执记录UI曝光0.1，没有单独的storedExposure字段 |
| `retained-preview-followup-20261119/ORDINARY_OPENED_CURRENT.json` | `a321c8a74c86b9656b508deb640f247da70dadc18372d78088815a9acd0a21de` | 同一文件路径；明确记录恢复文件存储曝光0.1 |

两条都是合法64位SHA-256，但不同；它们的记录不能证明当前磁盘仍是哪一版。两条原回执和文档摘要均在 MATERIAL_STATUS.json 绑定，未修改历史。取回时须一并给出**公司端当前文件的SHA-256、字节数和修改时间**；这只是两文件的取件清单/摘要，不是要求再复制历史目录。若与两条都不符，先保留差异并检查JSON指向的TIFF、恢复目标和编辑定义，不自动猜版本、删改文件或运行验收。

## 取件范围

只取这份已渲染TIFF及其对应恢复JSON。不取相机RAW、0806D680 payload、缓存、原生解码器、整套公司包或全部历史。家庭固定40MP合成TIFF（文件SHA以5d3aee67开头）及已授权Sony33MP材料均不是这份公司0da1bf38输入，不能替代。

当前没有可用的已配置取件渠道；需要材料持有人在已授权位置提供这两份及上述摘要。未取得之前，当前f84产品继续可用，原图项目保持待验，其他已通过结果不重跑。
