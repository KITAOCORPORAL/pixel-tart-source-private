# Match V4 Home 16GB Performance

RTX 5060 Ti reports 16,829,644,800 dedicated bytes and uses a 1024 tile plan. The final 102MP GFX100S run completed full-resolution GPU parity with three stable repeats.

| Fixture | MP | GPU classification ms | GPU compute ms | Process working set |
|---|---:|---:|---:|---:|
| Canon EOS R6 CR3 | 20.17 | 187.9 | 20.9 | 4.75 GiB |
| Fuji X-T5 RAF #1 | 40.19 | 292.3 | 53.4 | 7.28 GiB |
| Fuji X-T5 RAF #2 | 40.19 | 314.4 | 46.2 | 7.53 GiB |
| Fuji GFX100S RAF | 102.07 | 764.2 | 39.7 | 19.52 GiB |

Classification time is CPU decision-key preparation and is reported separately from GPU shader compute. Peak dedicated VRAM telemetry is UNKNOWN; process working set is not VRAM telemetry. Real 4GB and 8GB hardware runs, controlled real OOM, and device-loss telemetry remain NOT RUN. The 4/8/16GB policy changes tile and parallelism descriptors, not color results or parity thresholds.

Status: PARTIAL for broad memory acceptance; numerical full-resolution gate is closed for the four listed fixtures.
