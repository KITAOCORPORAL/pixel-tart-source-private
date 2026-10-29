# 光镜灯箱与胶片后端联动开发大纲
当前：2026-10-29 America/Los_Angeles；家庭产品f70ecf7d1179b6f42c721a787a4a2d7a7acc8109，总体诊断版，非完整LightTable复刻。

## 产品边界和导航
复用现有暗室比较/所选照片入口，不建第二图库、不嵌WinUI。R5.4第9章仅覆盖简易/专业节点；外壳/即时胶片/变体/3D/自由画布/策划仍按R5.3。灯箱是ViewingTableState v1独立查看摆放，拼贴是作品编辑，两者共享target/variant但不混布局。

## 实施顺序和当前结果
1. 暗室收尾：已有DevelopedStageProjection改系统执行视图，来源/配方hash、只读查看和切换失效保护；同固定输入旧包A/B预览与三格式像素一致。四色轮完整单格139×186 DIP、两行382，窄窗需滚动；专业图仍在照片与胶片条之间。已实现，物理验收未做。
2. 灯箱首批：选中照片/固定变体→中性灯箱摆放/缩放/90°旋转→带真实代理分辨率的局部放大→共享比较/返回暗室→保存→独立进程重开，已实现并自动测。展示灯光只改台面背景。不引入SHARP、专有EDR、音频、遥测或上传。
3. 后端适配首批：FilmBackendBindingV1和任务中心桥，固定源/域/profile/hash/engine/resource/参数/seed/输出位深/alpha/取消错误。已有创意胶片真实计算后生成新版本→灯箱→暗室→保存重开已测。首批只原生8位不透明JPEG/PNG/BMP和非复杂图；不降RAW/16位。不是实测负片/反转片，不与宝丽来混类。
4. SpektraLab产品接入：BLOCKED_BY_ACCEPTANCE，未实现真实engine加载。研究ce292f2a固定c1447c6；构建/渲染可运行但BT失败、任意grain_seed不支持、GPL及数据权利未批准。本产品只记录禁用入口与合同，独立研究另线；不合研究分支，不修改其解码/引擎。

## 固定参考和许可
LightTable-Windows cb8f4c89d845634688627f1c01225cb5d851248f；原LightTable 8e1868df40e69e6b1a08e207d7ab05cf463532ab；SpektraLab c1447c6ab618fa4b1cb11fd2fe12826855007059。文件级hash/许可与依赖读回见../validation/home-lighttable-film-20261029/REFERENCE_FILES.json及LICENSE_REVIEW.md。Root MIT不替模型/EDR/音频授权，进程边界不自动解决GPL。没有新增这些第三方依赖到产品。

## 下一批有界任务
- 优化RAW观片解码和进程工作集，补虚拟化/渐进显示、失败重试；100张当前93成功7失败、首次约49s、采样工作集约3.66GiB，不宣称已达流畅目标。
- 局部原图细节流式接口、任意角度拖柄/框选/平移台面及精美玻璃镜片/幻灯动画。当前仅代理view，非原片细节。
- 带可持久恢复输出的任务提交、跨崩溃恢复与外部引擎进程资源释放；当前无半成品变体，崩溃前未提交需显式重试。
- 普通胶片RAW/16位/宽域输入和三格式完整独立输出域门禁；已有8位适配不是未来Spektra色彩合同。
- Spektra许可批准、seed能力和BT/自然保留集/跨oracle门禁后，显式新engine版本接入候选变体；不能默认覆盖PV4/5/6或宝丽来。研究负责build/color/backend contract，产品负责adapter/variants/project/error recovery，写入范围不重叠。
- 完整鼠标/真实DPI/ACM/HDR与Grok美术验收，独立于自动/离屏/OS启动。

本轮身份、实测、完整包和所有限制见../validation/home-lighttable-film-20261029/FINAL_REPORT.md，不把历史路线图待实现状态盖过当前结果。
