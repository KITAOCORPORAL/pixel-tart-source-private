# 3D Color Space Renderer Architecture

The renderer boundary must consume `ColorSpaceVisualizationModel` and keep color science in Core. Core remains platform-neutral. A Windows renderer may use WPF and DX12, while future renderers implement the same contract.

Current state: Core model and projection remain platform neutral. Windows now has a bounded WPF `ColorSpace3DViewport`; it owns only drawing and camera input and does not introduce WPF into Core. GPU acceleration and native performance evidence remain open.
