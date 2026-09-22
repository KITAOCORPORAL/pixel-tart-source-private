# Studio v2 Source Identity Audit

- Branch: `integration/pixel-tart-developer-preview`
- Start branch head: `f90772712ca351f4441ae384aaaabd2f750ce79b`
- Verified Global Rollout product commit: `9970b4d0506841358f6479e5613c6a9c7a08ce53`
- `1c22ee20f893f3cfcfab43740a9aa015fe6ada0b`: object resolves locally but is not an ancestor of the current branch; it must not be used as ProductSourceSha.
- Studio v2 product commit: recorded after the prototype source commit.
- Evidence commit: recorded separately after evidence capture.
- Docs commit: design-system documents travel with the product commit; the final implementation report travels with the evidence commit.

Every Studio v2 evidence manifest must use a full Git SHA reachable from this branch. `UNFROZEN_WORKTREE` is allowed only for local iteration and must not appear in the delivered evidence set.
