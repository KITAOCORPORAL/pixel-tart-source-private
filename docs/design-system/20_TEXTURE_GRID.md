# Texture Grid

Purpose: visual material choice without third-party texture assets. Each option has an internal ID, Chinese display name and concise description. Layout is two columns in Pro Film mode.

Options: 无, 细纤维, 纸面颗粒, 柔雾, 扫描细纹. Selection uses a fine accent edge and quiet surface fill; hover uses the shared surface-hover token. Cards are compact selectors, not large buttons.

Do use Pixel Tart procedural previews/effects, deterministic seeds and a visible “none” choice. Don't use WOMB bitmaps, animal patterns, tiled sine-wave assets or internal IDs as foreground language.

Responsive/accessibility: items wrap/scroll without horizontal clipping; display name remains primary and description supports meaning beyond appearance. Implementation: `PixelTartFilmTextureOption`, `PixelTartFilmTextures`, `PixelTartFilmPipeline.Surface`.
