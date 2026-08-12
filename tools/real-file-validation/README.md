# Pixel Tart Real File Validation

This local-only harness validates the production RAW, batch compression, local split and collage chains with files selected on the current Windows computer.

Safety contract:

- source files are opened read-only and are checked by length, modification time and SHA-256 before and after;
- all outputs must use a dedicated `PixelTart_Validation` directory;
- the tool never uploads, embeds or copies source photographs into the repository;
- JSON reports contain `<USER_PATH>\file.ext` and `<VALIDATION_PATH>\file.ext`, never full local paths;
- reports must be reviewed before they are placed in a public handoff repository.

Examples:

```powershell
.\tools\real-file-validation\Invoke-RealFileValidation.ps1 raw D:\PixelTart_Validation\raw D:\PixelTart_Validation\raw.json C:\path\photo.ARW
.\tools\real-file-validation\Invoke-RealFileValidation.ps1 batch D:\PixelTart_Validation\batch D:\PixelTart_Validation\batch.json C:\path\a.jpg C:\path\b.jpg C:\path\c.jpg
.\tools\real-file-validation\Invoke-RealFileValidation.ps1 local-split D:\PixelTart_Validation\split D:\PixelTart_Validation\split.json C:\path\a.jpg C:\path\a.dng C:\path\b.jpg C:\path\b.dng C:\path\c.jpg C:\path\c.dng
.\tools\real-file-validation\Invoke-RealFileValidation.ps1 collage D:\PixelTart_Validation\collage.jpg D:\PixelTart_Validation\collage.json C:\path\a.jpg C:\path\b.jpg C:\path\c.jpg
```
