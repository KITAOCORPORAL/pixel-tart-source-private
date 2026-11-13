# Unified functional/memory replay

Product source: 2d20b3274f9b84c93d891742a2c5887e46b87b6a, based on functional delivery43781bfe. The replay implementation was retained byte-for-byte from the versioned de0b04ff tool, except that its isolated off-screen windows do not request activation.

This is controlled cache/WPF replay, not normal user-entry or actual TIFF-export acceptance. It reads only the existing hash-bound RGB48 cache copy; a generated text .raf routes to an injected reader, never a native RAW decoder. maximumNewRawDecodes=0 and the2GiB guard are mandatory. Original roots and ledgers are not edited.

Publish with WholeAppDiagnosticBuild=true and SourceRevisionId=the frozen product source. run_guarded.py APP CACHE_COPY NEW_OUTPUT shell-cycle-natural checks explicit detail/zoom, save, close, independent cache reload and export-input pixel hashes. shell-auto-natural-soak checks210s minimized natural idle/restoration. GC samples are explicitly diagnostic.

Native main-project and actual TIFF export remain separate receipts. Package evidence must not substitute this replay for the blocked Windows file picker.
