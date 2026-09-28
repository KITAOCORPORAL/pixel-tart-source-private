# LibRaw compatibility probe

This is an opt-in, external-fixture probe for the product's current LibRaw boundary. It does not
copy RAW files into the repository and it does not replace the production package. It performs
20 serial ProfessionalDecode runs with one decoder instance and 20 runs with a new decoder for
each run, recording dimensions, orientation, RGB48 SHA-256, and failures.

Run from a .NET 10 SDK shell:

```powershell
$env:PIXEL_TART_COMPANY_RAW_ROOT = 'D:\AI AGENT\PixelTart-TestAssets\CompanyRAW'
$env:PIXEL_TART_LIBRAW_PROBE_OUTPUT = 'D:\PixelTart-QA\libraw-matrix.json'
dotnet run --project tools/LibRawCompatibilityProbe/LibRawCompatibilityProbe.csproj -c Release
```

`PIXEL_TART_LIBRAW_CANDIDATE_EXE` is optional. When set, it must point to a separately built
candidate process which accepts `--root` and `--output`; otherwise the JSON explicitly records
`candidate.status = NOT_AVAILABLE`. The current checkout has no newer Sdcb.LibRaw Windows
runtime and no native compiler/SDK, so no candidate DLL is fabricated or loaded in production.
