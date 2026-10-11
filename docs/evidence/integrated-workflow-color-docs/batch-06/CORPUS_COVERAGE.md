# 本机素材覆盖与授权来源

## 读取规则

只在本地副本解码。照片和衍生图留在ignored artifacts，不提交Git，不公开原路径/客户内容。输入复制时SHA-256核对既有清单；结果记录原文件未改。无生产数据库读取/写入。授权素材不等于视觉验收批准。

## 可用语料

- Sony ILCE-7M4 / A7M4：五组ARW+配对JPEG。授权依据为仓库 `docs/evidence/color-studio-rebuild-2026-10-05/START_STATE.md` 和既有input-manifest；本批11个输入副本核对同hash。ARW全部7028×4688；用户JPEG与RAW不是颜色真值等价关系。
- Canon EOS 5D Mark III：本机Real语料中一张CR2，既有 `docs/color-studio/raw-compatibility/01_RAW_CORPUS_MANIFEST.md` 标THIRD_PARTY_TEST_FIXTURE / LOCAL_COMPATIBILITY_TEST_ONLY，hash **09FEE3EBB29DA5F2F50191BF8B863735A20CD31789B33454ECDE88A67DE457E4**。上游rawsamples.ch Canon页列出相同文件，主页要求上传者提供无copyright文件并供免费测试；没有将免责声明提升成法律担保。本批仅本机兼容测试，不重分发。

公开来源在本地curl核对：https://www.rawsamples.ch/index.php/en/ ，https://www.rawsamples.ch/index.php/en/canon 。未下载替代用户媒体/开启浏览器。授权不明确的新文件没有擅自纳入。

## 内容覆盖与未覆盖

| 类别 | 覆盖 | 证据/限制 |
|---|---|---|
| 两相机真实RAW | PASS | 6文件完整解码/preview/full TIFF/redecode，2相机产品链 |
| Sony A7M4 | PASS | 5个授权文件，native Model ILCE-7M4 |
| 配对JPEG | PASS类型识别，色准NOT_RUN | 五组hash核对，专业decode拒绝；旧渲染语义不变 |
| 人像/混合光/高光 | 内容存在，视觉批准NOT_RUN | 本地配对JPEG联系表显示暗部人像、火焰与环境光；未把看图内容分类当色卡/色准测量 |
| 饱和通道/黑附近 | 实际输出计数PASS | Canon236231 saturated、Sony7711–14268；零通道数也记录。不证明sensor clip映射 |
| 中性灰/曝光阶梯 | SYNTHETIC PASS | 公式测试；真实标定灰卡、曝光阶梯NOT_RUN |
| 负值/大于1 | SYNTHETIC PASS | 曝光与矩阵数学保留；当前真实decoder输出uint16，早期负信号已经不可用 |
| 不支持/缺配置 | 合成/损坏文件PASS | 实际非法ARW返回失败；缺cam_mul/已WB合成拒绝；真实新相机及sRAW专项NOT_RUN |
| C1对比/肤色评价 | NOT_RUN | 不比较不同照片像素，不推测C1私有顺序 |

真实颜色质量结论仅PARTIAL。RAW/JPEG/联系表/完整TRX/生成TIFF均未提交。公开技术来源与第三方素材来源各自审计，不混为授权用户原片。
