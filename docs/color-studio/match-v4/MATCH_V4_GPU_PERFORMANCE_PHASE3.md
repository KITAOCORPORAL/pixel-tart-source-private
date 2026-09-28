# Match V4 GPU performance Phase 3

The GTX 1650 synthetic 256x192 smoke run recorded CPU pixel execution 20.43 ms, whole GPU upload/compute/readback 2.01/35.32/0.67 ms, and 64-edge tiled GPU upload/compute/readback 8.09/5.58/3.73 ms. These values are diagnostic only and are not 24/45/60/102MP benchmarks.

24MP, 45MP, 60MP, 102MP V4 GPU benchmarks, real GFX100S V4 GPU processing, and live peak VRAM are **NOT RUN**. The external Core runner's GFX100S 102.07 MP total of 168,875.9 ms is retained as non-GPU context only.

The adapter reports upload, compute, readback, and tile-count fields so a later hardware run can record them without changing the contract. A compile result is not a performance result.
