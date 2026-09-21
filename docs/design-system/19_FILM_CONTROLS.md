# Film Controls

Purpose: optional Pixel Tart-owned film response after reference color. Anatomy: enable toggle, named profile, profile strength, grain, halation and Pro-only grain size/Bloom/vignette/surface/texture/seed action.

Metrics: parameter label to slider 8 DIP; parameter blocks 18–24 DIP; section groups 28–36 DIP. Values use `AccentValueBrush`; Studio sliders use a 4 DIP track and 19 DIP quiet thumb.

States: disabled identity, enabled neutral identity, profile-only, individual effect and combined. Do keep profiles generic and Chinese. Don't imitate commercial stock names, expose a no-op option, or imply film pixels are stored in LUTs.

Responsive/accessibility: controls scroll vertically, retain focus visuals and keep label/value pairs readable at 200% layout scale. Implementation: `PixelTartFilmSettings`, `PixelTartFilmPipeline`, `ReferenceColorWorkspaceView.xaml`.
