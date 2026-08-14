# Pixel Tart Asset Library Schema Proposal

Status: feature-branch development proposal
Branch: `feature/asset-library-v1`
Production migration: **not registered**

## 1. Existing identity audit

The existing concepts are intentionally kept, but none is promoted blindly to a global library identity:

| Existing concept | Current responsibility | Identity scope | Asset Library decision |
|---|---|---|---|
| `OrganizePhotoItem` | Temporary input to copy/move grouping plans | One organize operation | Keep as an operation DTO. It is not a persistent asset. |
| `MediaFileRecord` / `MediaFiles` | Matching/index cache for the current source scan | Index snapshot | Reuse metadata ideas only. `SqliteMediaIndexRepository.ReplaceAsync` deletes rows and creates new GUIDs, so `MediaFiles.Id` is not a stable global `AssetId`. |
| `SelectionAsset` | Online-selection project member and proxy state | One selection project | Keep `SelectionAsset.Id` project-scoped. A later controlled change may add optional `SourceAssetId` plus filename/stem snapshots. |
| `TetherAssetRecord` | A file discovered inside one tether session | One tether session | Keep `TetherAssetRecord.Id` session-scoped. Importing it to the library creates/resolves a stable `AssetItem.AssetId`. |

The canonical library identity is therefore `AssetItem.AssetId`, generated once and persisted. Re-indexing the same normalized source path updates metadata without replacing the ID.

## 2. Isolation decision

The first development preview uses an independent metadata database:

```text
%LocalAppData%/KitaoPhotoSelector.AssetLibraryV15Preview/asset-library-v15-preview.db
```

This is deliberate. The active product database is still controlled by the P0 line, and the feature branch must not silently advance or mutate that schema. The proposal can later become a contiguous product migration after P0 is stable and merge order is agreed.

No `Schema 6` migration is registered in `DatabaseMigrator`, and the formal `pixel-tart.db` is not opened by the preview host.

## 3. Proposed entities

### `AssetItems`

- `AssetId` stable GUID primary key
- `SourcePath`, `NormalizedSourcePath`, `DuplicateDiscriminator`
- `DisplayName`, `Extension`, `MediaType`, `FileSize`, optional `ContentHash`
- optional `Width`, `Height`, `Orientation`, `CaptureTime`
- `AddedAt`, `ModifiedAt`, `Rating`, `Comment`
- `IsMissing`, `IsArchived`
- `ImportMode` (`Reference` or `ManagedCopy`)
- optional `ManagedCopyPath`

Only metadata is stored in SQLite. JPG, RAW, PSD and video bytes are never stored as BLOBs.

### Virtual folders

- `AssetFolders`
- `AssetFolderMemberships(AssetId, FolderId)`

Membership is many-to-many. Adding an asset to several folders creates no image copy and does not move the source file. `ParentFolderId` supplies a virtual hierarchy. Folder auto-tags are configuration; removing an asset from a folder does not remove tags automatically.

### Tags

- `TagGroups`
- `AssetTags`
- `AssetTagMemberships(AssetId, TagId)`

Tag merge migrates all memberships to the target tag, archives the source tag, and writes an undo journal entry that restores the prior membership sets after restart.

### Smart folders

- `SmartFolders`
- `SmartFolderRules`

A smart folder stores rules only, never membership rows. The first evaluator supports AND/OR groups, per-rule negation, exact/contains/not-equals comparisons, numeric comparisons and filename regex. Invalid regex returns a readable query error and does not terminate the process.

System queries `Uncategorized` and `Untagged` use `NOT EXISTS` membership predicates and therefore remain correct as folders/tags change.

## 4. Source-safety contract

Reference import is the default:

- no move
- no rename
- no write to source bytes or EXIF
- no source deletion
- removing a library record, folder membership or tag membership cannot delete the source file

Managed Copy is explicit and requires a destination root. The copy gets a collision-safe library filename. The original remains untouched. Permanent source deletion is deliberately absent from V1.

## 5. Query and performance contract

- database-only search over filename, comment, tag and folder names
- keyset paging by (`AddedAt`, `AssetId`), default 100 and hard maximum 500 rows
- indexes on display name, added time, capture time, rating, tag membership and folder membership
- metadata-only import; no full-image decode during indexing
- thumbnail decoding belongs to the UI cache/queue, not SQLite
- preview loads at most one page of model rows; additional pages are explicit
- cancellation is checked during import, decode and query enumeration
- non-regex Smart Folder rules compile to SQLite predicates; regex remains a bounded fallback
- visual-analysis cache variants include decoded-proxy fingerprint, palette size and palette sort

A deterministic 100,000-record metadata-only test traverses keyset pages without storing media bytes. This is a correctness smoke test, not a production-hardware latency claim. SQL plan caching and a 10,000 generated-JPEG end-to-end benchmark remain deferred.

## 6. Undo scope

V1 local undo tokens cover:

- folder membership add/remove
- tag membership add/remove
- rating/comment update
- tag merge

`AssetLibraryUndoJournal` retains the newest 100 operations. Forward mutation and journal write share one SQLite transaction; inverse mutation and `UndoneAt` marking also share one transaction. Folder membership undo records only rows introduced by the operation, including auto-tags. Restart success paths are tested; explicit SQLite fault-injection remains deferred.

## 7. Future controlled migration

After P0 merge stabilization:

1. back up the product database;
2. add the Asset Library tables as one contiguous migration;
3. import eligible `MediaFiles` rows by normalized path while generating stable `AssetId` values;
4. do not rewrite `SelectionAsset.Id` or `TetherAssetRecord.Id`;
5. add optional mapping fields/tables only where cross-workflow references are needed;
6. verify foreign keys and indexes;
7. retain the backup for rollback.

Rollback before feature use can drop only the new Asset Library tables. After user-created metadata exists, rollback must export folder/tag/smart-folder metadata before removing tables.

## 8. First-round implementation and deferrals

Implemented on this branch:

- stable `AssetItem`
- Reference and explicit Managed Copy contracts
- folder/tag many-to-many repositories
- tag groups, tag merge and undo
- grouped Smart Folder evaluator with SQLite compilation for non-regex rules
- metadata import, search, filters and bounded paging
- uncategorized/no-tag queries
- isolated three-column Darkroom-style WPF development preview
- deterministic temp-database/file tests

Deferred intentionally:

- production database migration and merge to the P0 branch
- persistent thumbnail priority queue and multi-size disk cache
- 100,000-record profiling run on production-equivalent hardware
- drag/drop folder tree editing and insertion-line feedback
- full drag/drop folder tree editing and insertion-line feedback
- complete Tag Manager editing/merge UI and complete visual Smart Builder UI
- SQLite fault-injection coverage for persistent undo
- generated 10,000-JPEG end-to-end UI benchmark
- AI, semantic search, cloud sync, browser extension and plugin system

The preview contains no production cloud provider and no Eagle branding or copied assets.
