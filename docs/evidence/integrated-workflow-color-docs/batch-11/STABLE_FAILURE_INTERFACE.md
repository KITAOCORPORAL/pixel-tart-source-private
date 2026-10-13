# Stable backend failures for later UI integration

StudioProcessingFailure fields: Code, Stage, UserMessage, PreviousVersionPreserved, NativeCode/NativeName(optional), RequiredBytes/AvailableBytes(optional). UserMessage omits input paths and credentials. LastProcessingFailure is exposed on the existing ReferenceColorWorkspaceViewModel without XAML/layout changes.

Codes: RAW_NATIVE_FILE_UNSUPPORTED, DecodeFailed, STUDIO_BUDGET_REJECTED, STUDIO_DOMAIN_OR_CONTRACT_MISMATCH, STUDIO_SOURCE_CHANGED, STUDIO_SOURCE_UNAVAILABLE_OR_CHANGED, STUDIO_CANCELLED, STUDIO_UNSUPPORTED_OPERATION, STUDIO_PROCESSING_FAILED.

Legacy decoder RawDecodeException retains ErrorCode and addsStage/NativeCode/NativeName. Professional decoder and FrozenRawMaster retain their exact historical exception classes (InvalidDataException/IOException); structured result is attached as Data[StudioFailure] and resolved by From. Caller handling and historical tests are not silently changed. Budget exception derives existing InvalidOperationException and supplies required/available ledger bytes.

No page layout/control design is included. UI documentation can consume these fields and current-state notifications later. Existing source/revision/version checks remain; cancelled/stale jobs do not install a newer edit version. Failure states are backend diagnostics, not permission to hide unsupported data or retry with preview/low-bit fallback.
