# Final evidence validation diagnostics

First verify_evidence.py run failed while decoding UTF8 Chinese summary with Windows default GBK. No product/test result changed; validator now explicitly uses UTF8-sig.

First cached-diff check after raw TRX/log inclusion flagged raw CRLF as trailing whitespace. Original evidence bytes must not be rewritten. Those exact raw evidence types are now binary in this evidence folder's attributes, consistent with preserving original logs. Authored source blank EOF was separately removed; full authored diff rechecked.

Initial research C++ API link failed, initial .NET execFile environment restore failed, and cancellation positional argument compile failed. Corresponding logs retained; final successful build was separately run. No failed product assertion was removed and no threshold loosened.

An initial manifest regeneration accidentally used a stale REPL array capture and produced an empty files list. Manual count check caught it; regenerated with a locally-scoped inventory, added a nonempty minimum80 coverage assertion, then reran hashes. Earlier304-check pass was not sufficient and is superseded.
