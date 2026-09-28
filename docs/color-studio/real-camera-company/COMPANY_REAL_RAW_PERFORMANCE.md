# Company real RAW performance — descriptive, not certification

Source `ba58558682d5dcfa570017f338ed98ce9b3a9248`; Windows x64, .NET SDK 10.0.401. Timings in milliseconds, one first-run sample per fixture; no controlled cold/warm or device-contention benchmark. `Product preview` includes ViewModel load + activation; `Export` includes full-resolution decode, Match and TIFF write/read validation. `Decode` and `Match proxy` are *separate preflight* measurements and must not be added to product timings. Process peak working set is cumulative at that point in a sequential runner, **not per-fixture RAM**. Source file MiB uses bytes / 1,048,576.

| Camera / fixture | MP | RAW MiB | Professional decode | 1600 proxy Match preflight | Product preview | Product export | Product preview + export | Process peak MiB (cumulative) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Sony ILCE-7M4 / company-01 | UNKNOWN | 23.43 | FAIL | NOT RUN | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| X-T5 / company-02 | 40.19 | 83.29 | 4,757 | 1,795 | 12,506 | 39,650 | 52,156 | 11,910 |
| X-T5 / company-03 | 40.19 | 83.34 | 4,780 | 1,862 | 12,493 | 40,745 | 53,238 | 11,943 |
| GFX100S / company-04 | 102.07 | 122.70 | 4,711 | 1,971 | 12,176 | 96,162 | 108,338 | 18,454 |
| EOS R6 / company-05 | 20.17 | 7.89 | 933 | 1,843 | 3,754 | 18,919 | 22,673 | 5,206 |

GFX medium-format route completed on one real 102 MP sample, but ~96 seconds export and cumulative 18.5 GiB peak process set demand dedicated bounded performance and UI responsiveness work. No performance gate is closed on these figures. Live WPF interaction/cancellation/visual color evaluation were not measured by this headless product ViewModel run.
