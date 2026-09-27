# DOCX Interoperability Research

Status: **PARTIAL — OOXML package analysis and Pixel Tart contract; WPS round-trip not verified**

## Package map

| Part | Role | Pixel Tart phase |
|---|---|---|
| `[Content_Types].xml` | MIME/type declarations | Import/export core |
| `_rels/.rels` | package relationships | Import/export core |
| `word/document.xml` | body, paragraphs, runs, tables, sections | Phase 6 |
| `word/styles.xml` | named styles and defaults | Phase 6 |
| `word/numbering.xml` | bullets and numbering definitions | Phase 6 |
| `word/settings.xml` | document settings and compatibility flags | Phase 6 |
| `word/theme/theme1.xml` | theme colors/fonts | Phase 6 |
| `word/_rels/document.xml.rels` | images, hyperlinks, headers, footers | Phase 6 |
| `word/media/*` | embedded images | Phase 6 |
| `word/header*.xml`, `word/footer*.xml` | header/footer content | Phase 6/P2 |
| `word/footnotes.xml`, `word/endnotes.xml` | notes | DEFER |
| `word/comments*.xml`, `word/people.xml` | comments/mentions | ADAPT later |

## Canonical model boundary

Pixel Tart should keep a product-native planning document as the source of truth. DOCX is an interchange format. The first writer exporter should support paragraphs, headings, lists, tables, images, page breaks, and basic headers/footers. Unknown OOXML should survive in an import report or preservation envelope where practical; it must not be silently discarded.

## Compatibility tiers

| Tier | Supported | Treatment |
|---|---|---|
| 1 | text, heading, list, table, image, page break, basic styles | faithful round-trip target |
| 2 | headers/footers, links, comments, simple shapes | approximate with report |
| 3 | tracked changes, fields, equations, footnotes, complex sectioning | explicit approximation or unsupported |
| 4 | macros/VBA, embedded applications, proprietary extensions | ignore safely and report |

## Required import report

`Imported`, `Supported`, `Approximate`, `Unsupported`, warnings, source file hash, exporter version, and object-level locations. A failed or partial conversion must be visible to the user.

## WPS-specific gap

No WPS-created DOCX was opened or round-tripped in this run. The compatibility claims above are OOXML design guidance, not WPS behavior evidence.
