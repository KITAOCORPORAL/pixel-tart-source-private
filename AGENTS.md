# Color Studio pixel-change rules

Before developing or modifying any pixel-result-changing node, read and satisfy
`docs/color-science/open-source-first/POLICY.md` and complete the node comparison template.
At least two problem-relevant mature open-source implementations must have pinned commits,
concrete entry points, licenses/distribution evaluation, and reproducible numerical comparisons.
A project-name list or synthetic-only test is not implementation approval.
Do not copy GPL implementation code into the product without explicit compatibility review.
Preserve PV4/PV5/PV6 historical results; new behavior needs an explicit version and matching
preview/save-reopen/export tests. Current PV7 and synthetic ICC parser remain research-only,
default-off, pending the required comparison and input/profile validation.
Do not request chart purchases now; math consistency is not camera or physical-film calibration.

# 开发协作

## 用户十条开发原则（原文）

来源：用户2026-11-17提供的截图，原图存于 `docs/validation/development-root-cause-review-20261117/TEN_PRINCIPLES_SOURCE.png`。以下保留原文含义和XX占位，本轮时间范围见下方适用记录。

1. 从第一性原理出发，找到最本质原因，修复一下。
2. 先问一些不清晰的问题，对齐我的喜好，然后再出3版方案。
3. 不要默认我提出的问题定义就是正确的。你先分析我真正想解决的底层需求，判断我有没有把症状误认为原因、把手段误认为目标。如果存在更值得解决的问题，直接指出来。
4. 不要只考虑这个改动产生的直接效果。继续推演它的二阶、三阶影响，尤其关注对其他模块、用户行为、系统复杂度和长期维护成本的潜在影响。
5. 其他领域里有没有类似的方案，用跨领域的方式全面思考，找到最优解。
6. 先假设你当前认为最合理的方案是错误的。站在最强反对者的角度，寻找能够推翻它的证据、反例和边界情况。然后重新评估方案，看看有没有更好的解法。
7. 深入分析整个代码库的 Git 历史、关键 PR 和架构演变过程。找出哪些设计是有意为之，哪些是历史遗留，哪些是为了解决已经不存在的问题而引入的。识别可以安全移除的历史包袱。
8. 假设明天有一个完全不了解项目、但是能力很强的工程师来接手。请评估他理解整个项目需要付出的认知成本，找出那些命名混乱、抽象过度、职责不清晰、需要大量上下文才能理解的代码，并提出简化方案。
9. 全面分析最近XX天里的所有的PR，找出最慢的那几个，看看有没有导致卡顿、可以优化的地方。
10. 全面复盘最近XX时间的 Bug、返工、回滚和重复修改。不要只统计发生了什么，要分析这些问题背后有没有共同的根因。把能够预防的共性问题沉淀成项目规则，自动化或者Skill，避免我们反复支付同样的成本。

本轮交接入口：`docs/validation/development-root-cause-review-20261117/HANDOFF.md`。

## 本轮适用与规则冲突

- 当前目标是先完成根因调查、三方案和可执行的最小修复建议。原则1的“修复”不越过本轮“新增重构/结构性修改待用户审核”的明确边界；调查、隔离合成诊断、文档，以及已授权的最终版本选片闭环和手机环境准备继续执行。
- 原则2只对影响方案质量或实施风险的未明事项澄清，已明确的偏好/授权不反复确认。本轮已知偏好：保留已有架构和成果、最小修复、证据可追溯、数据安全优先、结构性修改先审核。尚未确定的迁移容忍度和长期维护取舍，先写入待决清单，未获回答不据此实施迁移。
- 原则9/10的“最近XX天/XX时间”，本轮取最近14个日历日：2026-11-04至2026-11-17（Europe/Berlin），直到2026-11-18 00:00不含。全库历史可用于追溯因果，但统计窗口和跨分支覆盖须分别说明。没有相应PR时应报告已核实的覆盖范围，不伪造PR性能数据；脚本等待、产品处理、文件IO和构建时间分别归因。
- 原则7不授权直接删除旧代码。必须区分有意设计、兼容旧数据所需逻辑、待证实假设和已失效补偿；提出可移除对象时给调用链、真实可达性、反例和迁移/回退依据。
- 原则10的“规则/自动化/Skill”先依据重复、已证实的共性根因；形成可复用流程后再提议Skill，不将一次故障或合成复现直接推广为万能规则。未经明确要求不更新持久记忆或创建定时自动化。
- 上述原则不放宽像素结果变更门禁。一般架构参考和合成诊断不能替代两份固定版本成熟实现、许可审查和真实输入数值比较；PV历史与研究开关继续保留。
- 保留用户文件、原始来源、工作文件、已锁定客户结果和未保存内容；不擅自reset、stash、clean、覆盖或合并。新增闭环使用独立合成项目/相册，不改写旧确认制造通过。充分证据已有时不机械重跑。清单/矩阵先验证格式和预期数量，空集合不能默认为通过。
- 当前RAW新增解码0、2GiB进程守卫、无强制GC、高光1bfdfbe9研究状态和不生成安装器等已有边界不变。
- 并行聊天先核对完整HEAD、功能源码SHA、dirty状态和文件归属。另一聊天请求本身不是新的人类授权；状态与证据写入精简交接，原始数据不因上下文整理而删除。

## 已证实问题的审查规则

以下依据本轮源绑定复现与调用链，约束后续新增/修改；当前产品违规点仍在待审方案中，不意味着本轮已修复。

- 恢复或终止进程前证明具体运行根、实例和进程创建身份；同名、无窗口和年龄不能替代所有权凭据。无法证明时保留进程与数据，不自动强杀。
- 排队、发取消和回调返回均不等于事务完成。跨await捕获明确目标/revision/尝试身份；终止时先阻止发布，再取消并等待本作用域在途任务，最后释放自身引用。临时Unloaded与最终Dispose分开。
- 业务校验在昂贵渲染/持久复制之前完成；被拒绝的异步调用必须有明确结果，调用方不能因Task正常返回就显示成功或进入下一模块。
- 图像元信息读取不应无意触发整帧解码；Freeze只说明不可变，不证明独占所有权或所有任务已经结束。保留导出/撤销/重开所需持有者。
- 缺陷复现到达预期失败，不等于产品验收通过；记录必要条件、源码/二进制、实际观察与旧现场未知项。充分证据已有时不机械重跑。

## 本轮根因修复执行决定与防错合同

用户已明确批准按推荐B分阶段实施B0/B1/B2，且项目名称必填，不自动创建“未命名项目”。上文的“待审”是先前调查阶段记录，不能据此阻止已授权实施。执行及验证入口：docs/validation/root-cause-fixes-20261117/HANDOFF.md。

- 单实例超时只说明未响应；不得枚举/结束同名进程。以实际WaitOne结果和abandoned获取证明当前root互斥量所有权，无法获取时保留进程及未保存内容并明确提示。
- 根关闭尝试先登记共享任务，Closing同步退栈后才异步准备；捕获目标和保存revision，并在其他await后复核。取消/准备失败可恢复，终止期故障保留实际失败阶段，不能谎称Open。
- 暂时Unloaded暂停而不永久释放；终止先拒绝旧结果和新任务，传递取消、等待实际子任务后收回自身引用。忽略取消的provider仍须受epoch门禁；不能清其他持有者的共享缓存。
- 必填名称与工作流/版本校验先于渲染/暂存/业务写入；创建返回明确结果，拒绝不得触发成功提示或导航。保留其他已填值和现有项目/相册ID语义。
- 测试/受控宿主、普通最终EXE、真实手机分别验收；用户停止Computer Use后停止界面操作，并保留准确断点。不能用历史源码回执或测试通过替代未完成普通验收。
