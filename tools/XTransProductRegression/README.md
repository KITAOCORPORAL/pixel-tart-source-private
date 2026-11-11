# Frozen X-Trans product regression tools

The frozen product source is a24fac65ae91f3fdef7ff16a58dd462a5b7cbe77. These Python tools do not change pixels or rebuild the product. Candidate binaries, RAW inputs, and photo crops remain in the local artifact root.

- verify_evidence.py ARTIFACT_ROOT NEW_OUTPUT: read-only verification of frozen binaries/manifests, old ledgers, stored finished inputs, 30 PNG crops, ten crop comparisons, and existing TRX results. It explicitly requires the current pre-decode checkpoint (0 consumed and empty product-path evidence). It does not execute tests, open RAW, or validate a completed product run.
- readback_p6.py ARTIFACT_ROOT NEW_OUTPUT_JSON RESEARCH_FULL_COMPARISON_JSON: verifies the two saved candidate P6 streams byte by byte, source hashes for both old baselines, and the maximum-difference coordinate. Existing full-image metrics retain their original authority; this does not run a decoder. The output file must be new.
- launch_app.py ARTIFACT_ROOT STEP [--attempt NAME]: verifies the frozen plan and opens one isolated app; all product actions remain on the real UI. It rejects existing attempt directories, an earlier potentially live process, consumed steps, and the batch STOP record. It records process memory at a target 50 ms interval. Its lifetime includes idle and dialog time; command timings come from ProductPathEnd, and candidate worker limits come from the frozen product. It does not import or export automatically.

Python standard library is sufficient. The launcher requires a usable Windows desktop and Windows process APIs. The local contact sheet was created separately with Pillow; it is a review aid at 100 percent and is not an acceptance screenshot or a TIFF export.

See ../../docs/research/xtrans-stable-product-regression-20261110/REPORT.md and RUNBOOK.md for exact checkpoint, authority boundaries, and the remaining eight real paths. Existing reservations and failures must always be preserved.

The desktop-02 batch is now STOPPED_RESOURCE_GUARD (one consumed, five numerically unused, zero executable). Do not clear STOP or resume remaining paths. collect_stopped_attempt.py ARTIFACT_ROOT NEW_OUTPUT_DIR reads the stopped scene, verifies frozen identities and the single completed import, compares full worker/cache/research RGB48 bytes, and seals the resource evidence. It performs no RAW decode, test run, app launch, or original-file mutation. See the desktop-02-20261111 report.

Version2 successor plans are supported only after explicit authorization: AuthorizedForHeavyDecode=true, source lock authorizedHeavyDecodes=6, and AUTHORIZATION.json status GRANTED_BY_USER with the same task. Pending drafts are rejected before app launch. The original Version1 stopped batch remains stopped. No plan flag is itself human authorization.
