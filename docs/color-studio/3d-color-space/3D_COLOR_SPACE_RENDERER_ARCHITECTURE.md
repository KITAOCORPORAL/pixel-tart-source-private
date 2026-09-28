# 3D Color Space Renderer Architecture

The renderer boundary must consume `ColorSpaceVisualizationModel` and keep color science in Core. Core remains platform-neutral. A Windows renderer may use WPF and DX12, while future renderers implement the same contract.

Current state: data model exists; renderer boundary, GPU point renderer, and WPF view are not implemented.
