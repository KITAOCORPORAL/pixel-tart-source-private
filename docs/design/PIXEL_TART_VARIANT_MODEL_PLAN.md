# Pixel Tart Variant Model Plan

**Status: DESIGN_ONLY**

This round does not add a Variant table, edit-state column, duplicate source
file, or placeholder Variant button. The current product stores one Asset and
its non-destructive workflow state. That model remains the source of truth.

## Proposed model

One imported Asset may own zero or more non-destructive Variants:

- `Original` — the imported source identity.
- `ReferenceMatch` — a reference engine, reference identity, settings, and
  engine version.
- `FilmLook` — a film/look preset and its parameters.
- `ClientVersion` — a named delivery configuration.

Each Variant would store parameters and provenance, never a second copy of the
original RAW or source file. A future record needs a stable `VariantId`, parent
`AssetId`, display name, kind, parameter document, engine version, created and
updated timestamps, and an optional active flag. Rendered previews and exports
remain cache or publishing outputs and are not source assets.

## Required safeguards before implementation

1. Migration must preserve the existing Asset, rating, color label, tags,
   folders, metadata, project relationships, and undo journal.
2. Copy/apply adjustment commands must address parameters only; they must never
   copy rating, tags, folder, filename, metadata, or project relationship.
3. Preview and export must resolve the same source, reference, settings,
   engine version, and ICC intent.
4. Deleting a Variant must not delete the parent Asset or original file.
5. Selection, query, and publishing contracts need explicit Variant semantics
   before any UI action is exposed.

## Follow-up design work

Define schema versioning, conflict behavior, query identity, undo records,
batch selection semantics, and export naming before writing a migration. Until
those contracts and tests exist, `VARIANT_MODEL = DESIGN_ONLY` is the honest
product state.
