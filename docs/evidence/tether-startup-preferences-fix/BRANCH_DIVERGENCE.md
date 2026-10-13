# Separate lines — no merge performed

Closure line baseline eae12944c66e9e519fceb0842344f8b0c9038b7b and test line5bde6bf8af824e3b2f325e1ff046228ccb0354e0 are divergent. Verified merge base9e0fc52b0fd3598bae36ad1ccb5db2b1520ad0d8. Closure commits include924d486 spatial/candidate backend, c5c0ffd evidence and eae1294 native failure. Other line includes batch12 public provenance/validation and dc18b14 test wait/pipe fixes, then5bde6bf test/native evidence.

This branch contains ONLY startup preference lifecycle source/tests plus own evidence on the closure baseline. No cherry-pick/reset/automatic merge, no borrowing test-line Release. Existing batch12 closure worktree had three generated ui-guardian modifications at entry; kept untouched. Other dirty/ahead worktrees retained.

After this fix is committed/pushed, separately compare the two lines, choose an integration branch and rerun build/tests/release identity. That integration is not performed in this task. New release cb55960 is specifically the closure+startup fix line; it is NOT the test line's sealed release. Future integration requires new source/release evidence rather than overwriting either checkout.
