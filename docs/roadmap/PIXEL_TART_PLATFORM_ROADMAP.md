# 光镜当前产品路线图

## 2026-10-29 当前家庭增量
产品f70ecf7d1179b6f42c721a787a4a2d7a7acc8109，home/r54-design-fixes-20261029。先完成R5.4暗室收尾，再接入基于现有target/variant的WPF灯箱查看状态和胶片任务边界；最小闭环已自动验证。详见LIGHTTABLE_SPEKTRALAB_INTEGRATION.md和../validation/home-lighttable-film-20261029/FINAL_REPORT.md。研究Spektra未通过产品门禁，不纳默认产品。不是R5.4全量/真实DPI/发行验收。

## 历史平台规划
# Pixel Tart platform roadmap

1. Windows Match V4 + GPU: complete high-precision GPU stages, tile/memory acceptance and product wiring.
2. Color Studio Professional: expose v3/v4 Beta engine choice and shared preview/export state; **3D Color Space / Color Map is REQUIRED and DEFERRED_TO_COLOR_STUDIO_PRO_PHASE** (see `docs/color-studio/3d-color-space/3D_COLOR_SPACE_PRODUCT_SPEC.md`).
3. Photography Workflow 1.0.
4. Planning + Asset Library.
5. Tethering + Client Workflow.
6. Windows 1.0 / RC.
7. macOS Desktop: native UX, Metal, ColorSync, Finder integration, Apple Silicon and RAW/ICC/TIFF/EXIF reevaluation.
8. iPad / Tablet: touch-first, Apple Pencil, browsing, rating, Color Studio and desktop sync.

The current Windows phase establishes the compute boundary and guarded product wiring. The
platform-neutral Match V4 transform and future 3D color data model are shared contracts; Windows
uses DX12 while future macOS/iPadOS renderers use Metal. macOS/iPad UI and Metal implementation
are not part of this change.
