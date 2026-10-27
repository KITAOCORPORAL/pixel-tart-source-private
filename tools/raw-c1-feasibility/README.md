# 复现入口

本目录是调查脚手架，不是新的调色引擎。只引用既有Core，依赖已有.NET10、Python3.12、NumPy/Pillow；不装Capture One/REA/LibRaw新版本。

根目录：C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY

报告：C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/docs/research/raw-c1-feasibility-20261027/REPORT.md

独立实验输出仅在该根的artifacts/raw-c1-feasibility-20261027，已gitignore。每次用新的子目录，不覆盖源文件。

## 只读盘点

```powershell
& 'C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/tools/raw-c1-feasibility/Inventory-Raw.ps1' -Root 'N:/pixart/RAW/Real' -Output 'C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY/artifacts/raw-c1-feasibility-20261027/inventory-new.json'
```

这是open/metadata-only，根+一级子目录，不unpack、不hash全库。rawCompression/rawBitDepth保持unknown，不根据文件名推测。选择样本hash在已保存samples-selected清单中，不反复全库hash。

## 当前RAW响应

```powershell
$r='C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY'
dotnet run --project "$r/tools/raw-c1-feasibility/ResponseProbe" -- "$r/docs/research/raw-c1-feasibility-20261027/evidence/probe-samples.json" "$r/artifacts/raw-c1-feasibility-20261027/new-response-01"
```

两D800及X-T5失败探针。默认输出720px处理后的RGB16LE，不是完整TIFF。源SHA每次解码由现有产品合同重新核验，不是全库重复hash。默认definition无局部/几何/胶片，EV五档。sourceManifest明确列出源，不扫描更多目录。

全尺寸pilot：额外传入一个真实sRGB profile文件路径作为第三参数，脚本用Core产品FillRgb16Row和AtomicTiffWriter输出默认EV0 full-size TIFF。此profile必须确认为sRGB，不能任意传ProPhoto给sRGB像素。已执行pilot的profile由Pillow/LCMS createProfile('sRGB')生成，hash见pilot-tiff-inspection。不是C1宽域对照、不是WPF端到端验证。

## TIFF16与相对Lab

```powershell
$r='C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY'
python "$r/tools/raw-c1-feasibility/tiff_gate.py" "$r/artifacts/raw-c1-feasibility-20261027/pilot-full-01/D800-12L-PV4-EV0-full-srgb.tif" --output "$r/artifacts/raw-c1-feasibility-20261027/new-tiff-check.json"
python "$r/tools/raw-c1-feasibility/analyze_responses.py" "$r/artifacts/raw-c1-feasibility-20261027/discovery-01/response.json" --output "$r/artifacts/raw-c1-feasibility-20261027/new-roi-analysis" --lcms 'N:/pixart/Guangjing-R53-Home-20261027/Guangjing-R53-Home-CMS-StageB-Diagnostic-win-x64/app/lcms2.dll' --lcms-sha 87BF94F27F345384420383F247178F825C0101928FEDD3A4887FF60E9D4A76EB
python -m unittest discover -s "$r/tools/raw-c1-feasibility" -p test_probes.py -v
```

两个同源TIFF的比较：tiff_gate.py left.tif --right right.tif，再提供 --source-sha 真实64位RAW-SHA、--geometry-confirmed、--icc-sha 统一输出ICC-SHA、--lcms及--lcms-sha、--roi x0 y0 x1 y1、--output 新结果。几何参数只是操作者已核验声明，不是自动配准。尺寸、方向、裁切、畸变未核实不得比较。只放行unsigned RGB16 uncompressed chunky TIFF，不将Pillow转换为RGB8作为测量。压缩/BigTIFF等未经测试能力不得称支持。

LCMS输出Lab D50，DeltaE76只是输出间差异，不是真实颜色误差；同文件identity零值仅smoke check。相同source SHA是声明，不证明同一个空间ROI自动对齐。ROI亮建筑不是标准灰，肤色尚未覆盖，未拟合任何颜色映射。

## Capture One

先读CAPTURE_ONE_MANUAL.md；Stage-C1Inputs.ps1只复制到新的独立目录并hash验收，不打开C1、创建会话或更改设置。20计划全部未执行。源ID与probe别名映射为evidence/sample-id-map.json。人工导出先一张16bit ProPhoto全尺寸，通过ICC/位深/尺寸验收后再批量。

## 官方资料重读

fetch_sources.py只读公开HTTP，缓存到用户Temp，不下载到原素材目录，不打开浏览器。固定commit/文章ID和内容hash为已提交证据。商业软件资源、第三方源码不随项目发布。部分旧链接失败已记录，不能把fetch退出0等同所有文档成功。

## 2026-10-27盘点修正

素材根已知N:/pixart/RAW，无需另给路径。MetadataProbe第三参数--recursive可递归明确根下机型子目录，不跟reparse/不展开压缩包；扩展名增加CRW/DCR/ERF/MEF等。Inventory-Raw.ps1可加-Recursive，完整输入metadata扫描用N:/pixart/RAW/Real（RAW源均在其中，避免把根内旧导出TIFF当源）。356候选/345可open/11失败是递归元数据结果；不代表全部可渲染。26压缩包未展开、场景覆盖未重审，暂不作整库数量不足结论。旧212清单保留为此前实验范围证据。
