# Stage 4 decode-to-display audit and bounded repair

|Boundary|Numerical domain / profile / responsibility|Actual finding|
|---|---|---|
|RAW old product|LibRawDecoder.cs, Sdcb0.21.1.7/native0.21.1; camera/auto WB+matrix, sRGB output16, defaults include highlight/gamma|Already WB/encoded, not PV4 camera-linear. Preserve Sony FileUnsupported; exact Sony original missing at home.|
|PV4|ProfessionalRawDecoder.cs black/cblack/max/user_mul once, AHD Bayer, ProPhoto primaries gamma1/1; working LibRawLinearProPhotoD65 convention|NOT standard ICC ProPhoto D50. uint16 clipping prior to managed float. Camera profile metadata not physical calibration.|
|PV5/PV6|InstantFilmJpegInput/LegacyRawInstantInput, rendered sRGB identity, frozen source recipe|Do not apply RAW WB a second time.|
|Adjust/HQ|ProfessionalRawPipeline.cs / ColorStudioRenderPipeline.cs|Existing PV456 math, shoulder/gamut and HQ linear-light averaging unchanged;72 golden retained.|
|Display bitmap|RawDisplayBitmapAdapter RGB24, ColorStudioBitmapRenderer, ColorStudioImageViewport.DrawImage including detail tiles|Direct sRGB bitmap draw. No explicit monitor-profile conversion, no destination-profile cache or profile-change transform. OS/WPF compositor behavior not proved by source audit.|
|Monitor profile|Previously no code uses current window monitor ICC|Home read-only GetICMProfileW discovers two associations; model/profile names differ. This is association only, no calibration claim.|
|Export|ProfessionalRawExport / ProfessionalWicRowEncoder / RawMatchTiff16ProductPipeline and StudioQuickExport|Full frozen source, sRGB row transform, ICC embedded and reread. Display diagnostic must NEVER feed transformed monitor pixels into export.|
|ICC ingress|StudioQuickExport.Load: embedded profile WIC ColorConvertedBitmap→sRGB; detached pixels then EXIF once|Missing profile currently assumes sRGB without calibrated-input proof. Local WIC RGB48 transform refused 0x88982F80; remains OPEN, no hidden8bit substitute.|

Minimal product repair is diagnostic, not new colour math: WindowsMonitorProfileProbe read-only observes the window's display association, profile path/hash and invalid/missing state; viewport refreshes diagnostic on load/location/activation, detaches on unload. Tooltip explicitly says monitor-profile conversion NOT applied by ColorStudio and OS behavior unverified. Absent/invalid profile does not pretend calibrated fallback. Original/Matched/Tiles and export buffers untouched. No old project migration, no candidate DLL or new display transform. Cross-monitor actual foreground move NOT_RUN.

Two focused tests run real association-file validation with the current Windows sRGB bytes, corrupt/missing files, no-window state and unchanged bitmap references. NoWindow does not invoke a desktop/window activation. Pixel changes remain blocked pending open-source-first gate, clean implementation/distribution and physical/native validations. We do not claim this diagnostic closes actual monitor compensation.

Home monitor mismatch caveat: EDID says ASUS PA279CRV while DISPLAY1 association names PA279CV. We do not infer that it is the wrong profile solely from a filename; operator verification is OPEN. Two actual profile header hashes were read, not copied or embedded in product/evidence.
