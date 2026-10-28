# Polaroid 离线研究包（非产品升级）

先读 FINAL_REPORT.md。实际配置：profiles/profile-index.json；对照：results/comparison-portrait.png。实测数据 0，产品接入 0。

## 重跑
Python 3.12，依赖见 requirements.txt（建议安装到本目录 deps，不改系统环境）。脚本自动查本地 deps。生成与拟合不需要联网：

```powershell
& $Python -m pip install --target ./deps -r requirements.txt
& $Python ./offline.py generate
& $Python ./offline.py validate
& $Python ./offline.py apply input.png output.png --amount 1
& $Python ./offline.py apply input.png output-bw.png --bw
& $Python ./prepare_pairs.py filled-intake.json prepared-manifest.json
& $Python ./offline.py calibrate prepared-manifest.json --out ./results/actual-fit
& $Python ./test_prepare.py
```

缺 ICC 时只有确认来源本为 sRGB 才加 --assume-srgb；不能给 AdobeRGB/P3 无标签直接这样操作。完整图像入口限 8-bit，不是 16-bit/RAW 解码器。

C# 对照为实际旧 tone 文件的冻结副本+最小 recipe 类型适配；先 `dotnet build ./v3-reference/Reference.csproj -c Release`，设置 KITAO_DOTNET（可选）后 `python test_offline.py`。冻结源文件 hash 见 evidence。它不引用/构建/启动整个产品。

第三方 JS 参考重跑：通过 acquire-pinned-reference.ps1 只取 MIT Lab 的 pipeline.js/lut.js 与 LICENSE 至 references/polaroid-lab-print-corrector，然后 `node run_lab_reference.cjs`、`python compare_references.py`。第三方源码、网页、手册、光谱配置不随小交付包分发；完整本地研究目录仍可核查。

## 许可/数据边界
候选是第一方独立参数近似，非第三方 LUT 的重采样/拟合。现有仓库条款不变，未授予第三方商业产品发布批准。NASA/Eileen Collins 肖像来自 scikit-image v0.25.2 的逐图 public-domain 记录，只展示效果，不暗示 NASA 背书。各第三方源文件权利与衍生约束见 rights-register.json。合成 fixture 不得充当真实训练照片。




