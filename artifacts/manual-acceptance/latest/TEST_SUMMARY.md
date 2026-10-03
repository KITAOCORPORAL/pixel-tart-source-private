# Automated regression

SourceHead: `8457e9022ac8278df5401c6c1842f70a39c98129`

| Run | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| core-frozen-source.trx | 1533 | 0 | 4 |
| wpf-frozen-source.trx | 1429 | 0 | 11 |
| dpi-final.trx | 91 | 0 | 0 |

## Relevant subsets

Derived from full TRX results using selectors in test-results.json. Subsets overlap.

| Subject | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| Asset Library | 543 | 0 | 2 |
| Color Studio | 88 | 0 | 1 |
| Reference Match | 203 | 0 | 8 |
| 3D | 39 | 0 | 0 |
| Free Canvas | 24 | 0 | 0 |
| Guardian | 10 | 0 | 0 |

## Limits

DPI artifact checks still read de4c91a historical evidence. They are not current Release visual proof.
WPF/STA fixture tests are not user operation or user approval. Release runtime and 16 After screenshots remain NOT_RUN.
Intermediate failed/aborted TRX files are retained in tests/. No test was skipped to conceal these failures.
Baseline before changes: Core 1530/0/4; WPF 1421/0/11; DPI 91/0.
All final failures must be zero; acceptance remains NOT_APPROVED.
