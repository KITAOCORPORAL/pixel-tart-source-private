# Duplicate Finder v1

Exact groups use reliable content fingerprints; visually similar groups use cached perceptual signatures and an adjustable strictness threshold. Names and paths do not establish identity. The library exposes a duplicate system collection and a horizontal image-first comparison with dimensions, size, rating, path, usage and a non-binding “建议保留” suggestion. It never auto-deletes. Similarity is advisory: a related burst or alternate crop must remain under photographer control.

Import defaults to skipping an exact existing item; independent import is possible in repository policy. The dedicated guard exposes skip/view/import-anyway decisions, but the library import flow does not yet present all three options in a prompt. Similar images are not blocked on import.

Before trashing, reference usage is checked across inspiration collections, Free Canvas and project links. A referenced asset is blocked until references are changed. Trash only changes library metadata; it does not remove the disk source. Replacement captures every source and participant before any writes, then compensates across the whole group on an ordinary write exception; a second-source failure is covered by a rollback test. A process/power failure between stores is **not crash-atomic**: there is no durable cross-store journal. Planning references are counted in the model but have no persisted participant; they cannot be claimed protected. These gaps must close before an unconditional reference-safety release claim.

Evidence: `artifacts/creative-intelligence-v1/duplicate-finder/` uses real App/MainWindow synthetic files. Screenshots are not evidence of physical-machine behavior or crash-atomic replacement.
