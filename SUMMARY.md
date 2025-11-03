# BitcrackRandomiser & Pool Overview

## High-Level Flow
- Each GPU registers with the backend (`/api/clients/register`) using either auto-issued credentials or the `backend_client_ids/backend_client_tokens` stored in `settings.txt`.
- The backend (`RangeService`) assigns a prefix chunk (`RangeAssignment`) and returns start/end prefixes plus workload suffixes; the client builds `--keyspace start:end` and launches BitCrack or VanitySearch accordingly.
- While scanning, the miner parses stdout (`JobStatus`) to derive speed and progress. It reports periodic heartbeats and progress updates to `/api/ranges/report` so the dashboard reflects worker status.
- When the external app prints a proof address or private key, the miner collates the proof keys for submission back to the pool. On range completion the client posts either proof hashes to the public API or marks the backend range complete.
- Private keys found while attached to the backend flow are only sent to `/api/events/key-found`. The backend stores them (`KeyFindEvent`) and optionally notifies admins via Telegram; the miner never prints the key to stdout.

## Proof-of-Work & Range Validation
- Pools issue three (default six) proof addresses per range. The client verifies each and concatenates the resulting private keys, hashing them to produce a proof token (`SHA256`).
- Proof keys are returned when flagging the range to `btcpuzzle.info` or when the backend coordinates range completion, guaranteeing that the GPU actually traversed the assigned space.

## Backend Components
- `RangeService` keeps track of unassigned prefixes, supports sequential or random selection, and enforces chunk sizes/weights per puzzle.
- `Program.cs` wires the REST endpoints: range claim/report, stats overview, key-found events, and admin endpoints protected by `X-Admin-Key`.
- `NotificationService` integrates Telegram alerts; credentials come from `PoolOptions.TelegramBotToken` and `TelegramChatId`.
- A SQLite database (`PoolDbContext`) holds puzzles, clients, range assignments, and key-find events. Admin tools allow puzzle management and viewing mined keys.

## Client Internals
- `SettingsService` loads `settings.txt`, parsing backend configuration, worker name, GPU count, and optional API/Telegram hooks.
- `Randomiser.Scan` orchestrates worker loops: claim range, launch external app (`Process.Start`), tail output, manage proof keys, and restart on completion.
- `BackendPoolClient` handles registration, range claiming, progress reporting, and key-found submission to the backend APIs. It logs credential pairs so miners can persist them.
- Output parsing and telemetry updates ensure the backend dashboard displays online workers, speeds, and range progress in near real-time.
