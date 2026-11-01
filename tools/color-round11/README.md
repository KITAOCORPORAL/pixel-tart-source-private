# 第十一批工具：小规模自然摄影覆盖

本目录独立于第十批冻结流式20MP调用链。本轮没有full20MP engine运行，原脚本/source/build/resources、freeze/receipt/AUDITED_STATUS均保留。任何像素数学改动都不在本批。

## 显式独立准入
`sources-v1.json`位于photo-preparation子目录：NaturalCoverageAdmissionV1，最多6个既有授权清单中的明确sourceSHA/路径。原Canon自然入口仍只原白名单；新外部兄弟入口 `spk_stream_natural_photo_v1` 用NaturalPhotographyStreamV1，同时固化sourceSHA、prepared pixelSHA和proxy尺寸。每项long edge<=1200，标准ProPhotoD50 linear、orientation1。不是用别图冒充旧Canon hash，新增独立版本和独立DLL/hash。

RAW准备链直接调用未改的ProfessionalRawDecoder与ProfessionalRawPipeline.RenderOutputPixel EV0；没有半尺寸native解码、WB/曲线替换，没改product src。准备时完整RAW解码仍是原生产合同，之后冻结ExistingResearchD65ToD50V1桥+每通道Pillow F BOX，将**进入胶片引擎的输入限制<=1200**。输出输入域/位深/白点、source方向/行坐标及已有master端点均写报告。花卉/雪景/户外人物/街景/剪影是开发观察，不是独立holdout、色卡或物理模型。

```powershell
$env:PYTHONPATH='N:/pixart/ColorResearchHome-20261030/deps/python-frozen'
$env:PYTHONDONTWRITEBYTECODE='1'
dotnet build tools/color-round11/photo-preparation/PhotoPrepare.csproj
dotnet tools/color-round11/photo-preparation/bin/Debug/net10.0/PhotoPrepare.dll <明确photo-id> <全新production输出目录>
python tools/color-round11/prepare_photos.py
# prepared目录已经存在时拒绝；在新批配置全新根再复跑，禁止覆盖旧结果。
python tools/color-round11/generate_natural_backend.py <冻结stream-source> <全新clean外部source> <natural-inputs-v1>
# 按home-round11/natural-configure.txt相同固定Clang/CMake/Vulkan设置构建新shared DLL。
python tools/color-round11/run_photos.py --root N:/pixart/ColorResearchHome-20261030 --output <全新round11自然run目录>
python tools/color-round11/review_photos.py
python tools/color-round11/audit_prior_endpoints.py
python tools/color-round11/concrete_findings.py
```

本轮脚本如prepare/review的根/输出是明确固定本机实验配置，已存在会拒绝，不是通用可覆盖任务入口。换根须显式另版、重建准入hash和新DLL；不改老配置冒充原运行。`run_photos.py`参数化root/output，来源仍只清单6个。

## 数值与观察边界
- 每图whole/stream rows17/37实际全图0通道差，含首尾/尾行和全部seam；不是只看汇总hash或肉眼。
- output RGBA16为编码sRGB D65、opaque。input float允许负/>1；**本模块输出不是HDR**。输出0/65535计为端点，单凭端点不能判定是float clamp、曲线floor还是16bitround。
- 生产master已有0/1比例与proxy-footprint支持单独统计。将endpoint indicator经相同BOX传播，只有input proxy全部严格0..1且足迹无master端点时仍出现输出0，才记作“处理后新增端点”；不宣称传感器饱和/恢复，更不反推某个具体node。
- before是原production display，after是scene输入走film/print；两者显示映射不同，relative luma/ROI是同源相对响应，不是真实色差/准确性。禁止用“胶片味”替代已暴露问题。
- 照片前后图及12个1:1同坐标局部仅留N盘本地；Git只统计/hash/文本，无私人照片、RAW或大buffer。

具体复现/输出见REPRODUCE_HOME_ROUND11.md，结论见HOME_ROUND11_REPORT.md。许可仍LOCAL_COMPATIBILITY_TEST_ONLY，对发布照片或上游engine/data另行授权，不能将技术通过当产品接入。


## 2026-11-01 续接收尾（Europe/Helsinki）
新增 `verify_saved_outputs.py` 只读审计既有代理、whole、r17/r37 和被动阶段数据，不导入 native 引擎、不解码 RAW、不生成新像素。每份输入/成片绑定原记录 hash，重算逐通道一致性、端点比例及 72 阶段 finite/min/max/计数。报告结构检查不冒充私人像素检查。

`verify_round11.py` 现也核对六图×12阶段报告的身份、顺序、尺寸和有限性；实际像素由独立 saved-output 审计核验。
续接 22 项独立包校验、3 项 CLI、16 项 saved-data、2 项原照片准入拒绝测试通过（43 项，CLI 内另含双侧 subcase）。公司入口仅语法检查，公司实跑仍 NOT_RUN。新源码清单见 home-round11/resume-20261101/tool-identity.json；原 final-tool-identity.json 为中断前快照，未覆写。
