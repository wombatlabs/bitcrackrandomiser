# BitcrackRandomiser Custom Pool Mode

The client can now connect to the lightweight pool backend under `Backend/BitcrackPoolBackend`. This mode replaces the original btcpuzzle.info API calls with the self-hosted coordinator.

## 1. Configure `settings.txt`

Add (or update) the following keys in `BitcrackRandomiser/settings.txt`:

```
backend_enabled=true
backend_base_url=http://<server>:5000
btc_address=bc1example...  # address shown on the dashboard / used for payouts
backend_target_address=                  # optional fallback (backend supplies puzzle targets)

# Optional: reuse existing backend credentials (comma separated per GPU)
backend_client_ids=
backend_client_tokens=
```

Leave `backend_client_ids` / `backend_client_tokens` empty to let the client auto-register. On first launch the app will print the issued identifiers in the logs; copy them into `settings.txt` if you want the same worker identity on subsequent runs. When the backend is seeded with puzzle definitions (e.g., 71‑79 in `PoolOptions.SeedPuzzles`) it returns each puzzle’s target address, so the fallback value can stay blank.

If you run multiple GPUs, the client automatically registers individual workers (appending `_0`, `_1`, … to the worker name) unless you provide matching credential lists.

> **Target address** is still required if the backend does not provide one. With seeded puzzles you only need this fallback when testing locally.

## 2. Launch the Miner

Run the miner as usual:

```bash
cd BitcrackRandomiser
dotnet run --project BitcrackRandomiser.csproj
```

When the client starts it will:

- Register each GPU with the backend (or reuse stored credentials).
- Claim a range via `POST /api/ranges/claim`.
- Report an initial heartbeat so the dashboard shows the worker online.
- Scan the range using the backend-provided keyspace (`--keyspace start:end`).
- Notify the backend once a range completes so the next chunk can be assigned automatically.
- When a private key is discovered it is sent to `/api/events/key-found`; the miner keeps the key out of the console output.

## 3. Dashboard Feedback

With the frontend (`Frontend/`) pointing to the same backend you will see:

- Worker name, application type, GPU count and last-seen heartbeat.
- Current range (prefix start → end) and completion state (0% on assignment, 100% when finished).
- Aggregate progress for the puzzle.

Speed reporting currently depends on miner telemetry; if your miner prints hash-rate information to stdout you can extend `JobStatus` parsing to forward that number to the backend (the API accepts `speedKeysPerSecond`).

## 4. Handling Secrets

- Keep the backend token private. Anybody holding the pair `<client_id, client_token>` can submit progress.
- If you regenerate tokens, update the corresponding entries in `settings.txt` before restarting the miner.
- Telegram notifications can be enabled on the backend (`PoolOptions.TelegramBotToken` / `TelegramChatId`).

## 5. Troubleshooting

- “No backend ranges available” → the coordinator finished the configured prefix window or is paused. Expand the range in `Backend/BitcrackPoolBackend/appsettings.json`.
- “backend_target_address is required” → set `backend_target_address` before enabling the backend.
- Registration errors normally mean the backend is unreachable; confirm `backend_base_url` and network access.

For more details on the REST endpoints review `Backend/README.md`.
