# Calendar Workflow State Migration — RC2

## Scope

RC2 records the fact that a booking's shoot was completed separately from its current workflow stage. The calendar still exposes only four primary visual states: `Free`, `Scheduled`, `PostProduction`, and `Delivered`. No new table is introduced.

## Existing-data mapping

- `Cancelled` and archived bookings remain excluded from day aggregation and resolve to `Free` when no other active booking exists.
- `Delivered` remains `Delivered` and has no inferred completion timestamp.
- `Completed` and `Shooting` are treated as historical shoot-complete states. On migration, `ShotCompletedAtUtc` is backfilled from `UpdatedAtUtc` only when the value is absent.
- Existing scheduled and draft/tentative statuses remain scheduled (or free for draft when excluded by the resolver).

## New field

`ShootBookings.ShotCompletedAtUtc TEXT NULL` stores the UTC instant at which the application records “标记拍摄完成”. It is metadata only; no image, document, or customer content is stored.

## Migration

Schema version 5 adds the nullable column with `ALTER TABLE`, then backfills legacy `Completed`/`Shooting` rows from their existing `UpdatedAtUtc`. The migration runs in the existing migrator transaction and is idempotent through the schema version ledger.

## Rollback

The normal database backup is created before migration. If migration or integrity validation fails, the transaction is rolled back and the application enters its existing read-only recovery path. Restoring the pre-migration backup removes the column without touching source files.

## Compatibility

The field is nullable, so old records and imported records remain readable. Repository reads treat a missing/NULL value as unknown. The app continues to use the existing `Status` enum for detailed workflow stages; only the new workflow service writes the completion timestamp.

## Tests

Coverage includes migration/backfill, write and reload of `ShotCompletedAtUtc`, restart persistence, four-state day aggregation, multi-booking priority, closed-day independence, delivered undo protection, and idempotent workflow operations. Tests use isolated temporary databases.
