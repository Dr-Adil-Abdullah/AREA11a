# Approved password backup design — NOT IMPLEMENTED in v0.92.0

**2026-11-10, D-113.** Owner chose **A: backup password** (not a recovery-key file). No real password is requested or stored in chat/source/docs. This page is a pending implementation contract, not a cryptographic/runtime certification.

## Current truth / safety boundary

`BackupService` is unchanged in portion14. Existing manual/automatic/pre-restore safety copies are ordinary ZIP/DB-based workflows, NOT encrypted or authenticated packages. Do not advertise/send them as confidential or tamper-evident. Existing files must not be deleted, renamed or silently trusted as authenticated. No installer or new backup artifact was produced.

## Next coherent implementation

- Owner-only in-app setup/change with confirmation using password controls, not an ordinary Settings string. A separate, strong backup password; losing it can make older backups unrecoverable. App login reset must not pretend to recover encryption keys. Rotation does not change an old package's password.
- Automatic backups need the secret without prompting: use Windows-protected local credential storage **outside the database/settings/export/backup/audit**. Scope, ACLs, different Windows-account behavior and startup failure must be tested. On a new PC explicitly request the original backup password. No embedded shared secret, plaintext credential file, password in manifest, or unencrypted fallback. No password/key telemetry/logging. Missing setup/credential access must be a conspicuous failed backup, never a fake successful encrypted backup; the absence of recent backups is a data-loss risk to surface.
- Versioned, bounded, streaming authenticated-encryption container around the existing consistent SQLite snapshot + photos + manifest. AES-GCM with a reviewed chunk/nonce scheme, random salt/nonce, bounded password KDF parameters, and authenticated header/order/length/final record. No home-grown cipher. Validate maximum size/count/path/decompression limits before expensive processing. A local administrator or someone holding the password is outside the confidentiality/tamper boundary.
- SHA-256 of the payload must be bound to authenticated metadata. An unkeyed hash stored next to editable data is NOT anti-forgery: an attacker could recompute both. Authenticate package and metadata, detect truncation/reordering/trailing bytes, then verify checksum before SQLite validation or applying anything to the live shop. Decryption may require private staging; fail/cleanup it without treating unauthenticated content as valid. Deleting staging is not guaranteed secure erasure.
- Preserve consistent snapshot, photos option/meaning, crash-safe creation, atomic publish, retention limited to app automatic copies, backup scheduling and full pre-restore safety copy. Pre-restore safety/rollback must understand encryption too; do not ship a new package writer while rollback still assumes an ordinary ZIP. Snapshot database/photo consistency limits must be documented.
- Wrong password, modified header/ciphertext/tag/manifest, truncation, appended content, invalid archive/database and unsupported version must fail before live replacement. Restore failure must keep original data/safety copy and give useful nonsecret error text. A file merely opened/read without content change cannot reliably be detected and should not be described as tampered.
- Legacy plain ZIP/DB recovery needs an explicit, clearly unverified owner-only compatibility/migration path, not magic authenticity from a newly computed checksum. Retain legacy copies until an encrypted backup AND isolated restore have been verified. No quiet downgrade or automatic destruction of old backups.

## Required evidence before completion claim

Codec round-trip with files beyond one chunk; every-byte-region mutation/wrong-password/truncation/append/reorder rejection; KDF/length/count bounds; no plaintext/password leak in package or persisted settings/log/audit; failure cleanup/atomic-file preservation; genuine new-PC/password restore; Windows credential/ACL/auto-start/manual/auto/safety/rollback matrix; corrupt archive/DB and injected-replace/photo failure with original shop preserved. Synthetic data only, verified backup and truly separate DB/VM; another exe folder is not isolation.

No .NET/Windows execution here. Part15 is the next priority; this plan does not mean encryption has been written or tested.
