# Fujifilm repeated ProfessionalDecode determinism

External runner: `CompanyProductProbe`, source product baseline `3dab1b4f75dd696ba64ecfb431a28ce2fd933673`. Each case performed ten sequential ProfessionalDecode calls with one `LibRawDecoder` instance and ten calls with a new `LibRawDecoder` per call. Each SHA256 covers the complete RGB48 `ushort` sample buffer.

| Fixture | Same managed decoder | New decoder each run | Controlled pair changed pixels / max U16 delta |
|---|---:|---:|---:|
| X-T5 company-02 | 10 distinct SHA256 | 10 distinct SHA256 | 6,705 / 5,767 |
| X-T5 company-03 | 10 distinct SHA256 | 10 distinct SHA256 | 6,199 / 5,930 |

Classification: **NON_DETERMINISTIC**, observed for both instance strategies. The controlled pairs differ in sparse pixels; representative coordinates include `(4128,1497,channel=2)` for company-02 and `(7456,1001,channel=2)` for company-03. Canon repeated decode was byte-identical; GFX was byte-identical in the controlled pair. This makes managed dictionary reuse an insufficient explanation. Native decoder lifecycle/threading/RAF processing remains a hypothesis requiring a minimized LibRaw reproduction.

No production decoder setting was changed. Do not certify X-T5 RAF parity until a stable master or deterministic decoder contract is established and all four camera fixtures are rerun.
