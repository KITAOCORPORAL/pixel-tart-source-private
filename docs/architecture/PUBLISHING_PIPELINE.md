# Publishing pipeline

UI file/folder selection → `PublishingExportViewModel` options/layers/preset → `PublishingTaskCoordinator` → existing Task Center engine/handler → `PublishingExportService` → `WpfPublishingRenderer` background worker → verified temporary output → collision-free final path.

Preview invokes the same renderer with an ephemeral path, loads an immutable bitmap, and deletes that temporary file. Generation increments a revision so an in-flight stale render is not shown; changes trigger a later render. Options and layers are validated before output. Source hashes before/after rendering enforce non-mutation. Failed files return user-facing reasons; partial completion is recorded rather than silently retried. Cancellation is delegated to Task Center. The request store is in-memory, so interrupted queued publishing tasks cannot resume after a restart without resubmission; mark this as a known recovery limitation.

The project default preset store is consumed when entering publishing: a preset saved while a project is active becomes that project's default and is selected on re-entry. There is no full Planning UI or image editor in this pipeline.
