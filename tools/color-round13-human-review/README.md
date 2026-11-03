# 第十三批既有 A/B：只读、离线人工评价入口

这是一个文件校验器 + 单个本地 HTML，不是新的研究平台或产品入口。只复用既有六张开发图、既有 ROI 和原 X/Y 匿名映射；不计算锐化、RAW、胶片或新候选，不调用 GPU/引擎，不改输入文件。人工偏好未收到，保持 NOT_RUN；当前研究候选默认关闭、NOT_ADMITTED。

## 家庭直接运行

先检出交付的 `research/color-engine-round13-human-review-20261103` 分支。**不得运行旧的 run_candidate_ab.py 或 review_ab.py 来补文件**。后者会生成分析图与 TIFF，本入口仅静态读取其冻结映射，不执行它。

在该分支的检出目录打开 PowerShell；使用已有 Python（3.10+）、NumPy、Pillow。家庭如已使用冻结 Python 依赖，沿用当前进程的原 PYTHONPATH，不另建环境、不下载引擎资源。

```powershell
$Checkout = (git rev-parse --show-toplevel).Trim()
$Entry = Join-Path $Checkout 'tools/color-round13-human-review/prepare_review.py'
$env:PYTHONDONTWRITEBYTECODE = '1'

# 仅列出明确的 61 个必需文件；有缺项返回 2，不写文件、不遍历其他目录。
python -B $Entry --inventory-only

# 只在所有文件通过校验后，创建全新的本地私有目录。
python -B $Entry `
  --root 'N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1' `
  --stages 'N:/pixart/ColorResearchHome-20261030/round11/natural-stage-finite-v1' `
  --output 'N:/pixart/ColorResearchHome-20261030/round13-human-review-20261103'
```

若材料在另一个**已授权迁移**根，显式替换 --root / --stages，只读取这两个根下清单列出的文件。不要搜索其他盘，不用公司照片替代。缺项/漂移拒绝，不自动迁移或重算。输出已存在则拒绝；选择新的后缀，不删除旧目录。

成功后，用本地浏览器打开：

`N:/pixart/ColorResearchHome-20261030/round13-human-review-20261103/review.html`

不启动 HTTP 服务、不访问 CDN、不上传照片；页面直接嵌入原 PNG 字节。**生成的 HTML 含私人照片，不得上传 Git 或对外分享。** validation-local-only.json 为操作人员校验记录，评价人不必打开；同样保持本地。不要提前向评价人展示旧匿名 key、研究候选名称或指标胜负。

## 校验合同 / 必需材料

- 6 个第十一批既有 print_linear float32 输入：完整冻结 SHA、长度和 finite。
- 每图 6 个第十三批 whole 输出（4 个 float32 + 2 个 RGBA16）：共 36 个，逐个冻结 SHA、长度；float 全域 finite。
- 原完整 A/B PNG 共 12 个：RGB、原尺寸、无 ICC/gamma/sRGB/EXIF 等额外显示或方向元数据；逐像素匹配原 round-to-even(RGBA16 RGB / 257) 显示转换。
- 6 个已有匿名裁片：冻结 SHA + X/Y 对应原 PNG 的同坐标裁片逐像素相等。
- 1 个原 anonymous-key.json：与已冻结旧 review_ab.py 中的字面量映射完全一致。通过 AST 读字面量，不执行旧脚本。

合计 **61 文件**。原 full PNG 和 key 没有独立历史文件 SHA，不能伪称有；full PNG 通过冻结 RGBA16 的完整像素对照绑定，key 通过固定旧工具字节和已冻结匿名裁片绑定，现场实际 SHA 另记。入口本身固定四份既有 metadata/tool 的 SHA，漂移即拒绝；不允许由替换清单静默换候选。

本地路径缺项和精确名字见交付 metadata human-review-prep-20261103/MISSING_FILES_HOME_PATHS.json；这是**公司访问原家庭路径**的结果，不是声称家庭机器文件已丢失。不需要 DLL、SPIR-V、完整 LUT/sensitivity、胶片资源或原 RAW。

## 怎么评价

1. 浏览器页面缩放设为 100%。先适合窗口，再 100% 源像素或查看局部；100% 精确定义为 1 源像素 / 1 CSS 像素，不承诺跨显示器物理像素或校准色准。
2. X/Y 按钮或视口聚焦后的空格切换；共用尺寸与滚动坐标，不改某一版的色彩、锐化或亮度。两版缩放插值和浏览器色彩管理通路相同；100% 局部避免缩图掩盖细节。
3. 六场景各记录五项：花瓣/叶片细节是否过软；暗侧黑点/光晕；肤色/饱和边缘不自然彩边；噪声可接受性；X、Y、无明显偏好或无法评估及具体原因。不问哪个更高级。不适用的部位可明确选择无法评估。
4. 只有真人填写。没有默认答案；空白导出仍为 NOT_RUN。答案仅在页面内存，关闭/刷新前点击导出，并核对下载 JSON；没有服务器、后台任务或持久化存储。换场景会保留本页内存答案。
5. 评价 JSON 保持本地，经过授权再提供给研究会话。评价完成前不揭示映射；得到偏好也不自动通过独立验证、产品或许可门禁。

## 当前执行边界

公司没有可用的六图冻结材料，因此真实六图的入口生成与观看 **NOT_RUN_MISSING_PRIVATE_FILES**。已执行 8 项临时合成文件专项测试及 headless Edge 的合成 UI smoke，不能替代摄影偏好、真实六图读回或产品验收。UI smoke 不填写任何偏好，导出 30 项全 null。

离线文件/拒绝测试：python -B tools/color-round13-human-review/test_prepare_review.py。

test_viewer.cjs 只接受名为 synthetic-ui-test 的生成夹具目录，需要已有 Playwright + Edge，是开发自测而非家庭使用依赖。实际 Python、NumPy/Pillow 和浏览器版本由交付执行收据记录；不为此改变系统设置。

交付后停止。等待实际摄影评价，不自动调参、启动第二个像素候选、独立样本实验或产品接入。
