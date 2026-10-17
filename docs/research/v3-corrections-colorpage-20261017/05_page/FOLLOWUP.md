# Final UI/geometry follow-up

Full early WPF found FilmstripKeepsCompactHeightAndCollapseReturnsSpaceToMainImage failure:375px at1180×720 <55%. New-page launch button had added one WrapPanel row. Original failure retained; move launch to compact title strip (no assertion change), exact failing method + page/localization8/8 pass. Old page is retained, new page raster tools explicitly disabled when active professional/V3 independent input cannot consume those steps, avoiding adjustable-but-ignored tool controls.

Defect component2 persists CenterX/Y,Angle,Scale; masks transform full normalized coordinates before fixed-form evaluation. Component1 unchanged; no random movement caused by editing unrelated values. UI geometry explicit, defaults center. Source second-photo selector never scans directories. Risk audit observations and remaining physical validation are not replaced by UI state reads.

## 续接修复

新页顶部撤销/重做改为实际路由 V3 版本历史或普通调整历史；V3 禁用效果复用先渲染后提交事务，失败不消耗 redo。第二源缺失导致历史切换失败时，恢复之前配方、版本、预览、控件与缓存，不弹出历史项。实际控件绑定测试覆盖这些行为。焦点框颜色改为准确文字，不再冒称计算蒙版。取消用 Decode/PixelRender/DevelopmentAnimation 同步信号，另测 EncodeStrip/OutputValidation；不再用 IsInstantGenerating 代替阶段证据。新增缺源/恢复重做与双源资源释放测试。
