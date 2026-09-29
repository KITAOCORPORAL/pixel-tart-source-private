# Pixel Tart Color Pipeline v1 proposal

The current code supports the following canonical direction:

```text
decode -> input/working-space boundary -> Reference Match -> Preset/LUT
       -> Film color process -> Film spatial effects -> output transform -> Publishing
```

The order remains a proposal until a single `ColorPipelineSnapshot` contract is accepted. Any stage that cannot preserve the high-precision working representation must declare that boundary explicitly.
