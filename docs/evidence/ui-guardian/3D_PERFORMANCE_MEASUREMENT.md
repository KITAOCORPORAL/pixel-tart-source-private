# 3D performance — NOT MEASURED

Product source: `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`.

| Field | Observation |
|---|---|
| OS | Windows 10 Pro 10.0.19045 |
| GPU | NVIDIA GeForce GTX 1650 |
| Driver | 32.0.15.7270 |
| Renderer backend | WPF DrawingContext with CPU projection (source audit) |
| Active model/sample count | NOT MEASURED: 3D panel was not opened by native pointer |
| 3D viewport | NOT MEASURED |
| Idle / orbit / pan / zoom workload | NOT RUN |
| CPU utilization under 3D load | NOT MEASURED |
| GPU utilization | NOT MEASURED |
| VRAM usage | NOT MEASURED |
| Presentation frame time / FPS | NOT MEASURED |
| Resource leak trend | NOT MEASURED |
| Black-frame / input-lock / resize recovery | NOT RUN |

Process 28696 responded after the read-only observer check. At 09:56:43 +08:00 it had working set 961376256 bytes, peak working set 962985984 bytes, private bytes 941281280 and cumulative CPU time 24.796875 seconds. This single Color Studio startup/observer sample is NOT a 3D performance benchmark, CPU utilization percentage or leak result.

LastRenderMilliseconds measures CPU time spent inside OnRender, excluding presentation/composition. It must never be labelled FPS, GPU frame time or VRAM.
