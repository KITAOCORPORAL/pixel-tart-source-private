# Source and data security

This repository is private and intentionally contains only product source, build metadata, documentation, synthetic fixtures, and empty configuration templates.

## Prohibited content

Do not commit or upload:

- API keys, tokens, passwords, AppSecret/SessionKey values, connection credentials, cookies, or browser profiles;
- certificates or private keys (`.pfx`, `.p12`, `.pem`, `.key`);
- customer photographs, human photographs, RAW/JPG originals, personal portfolios, or production object-store content;
- production SQLite databases, logs, crash dumps, diagnostic sessions, caches, proxies, thumbnails, generated installers, archives, executables, or PDBs;
- absolute local machine paths, usernames, or LocalAppData contents.

## Before pushing

1. Inspect tracked and untracked files, including ignored files.
2. Scan the complete reachable Git history with a current secret scanner.
3. Review unknown images and large files before they enter Git.
4. Confirm configuration files contain only empty values, placeholders, loopback development URLs, or documented public identifiers.
5. Stop the push if a finding cannot be classified safely.

Generated acceptance evidence belongs in ignored local directories. Security findings and delivery exceptions must be recorded in the public handoff report without exposing sensitive values.
