# Bitcrack Pool Backend

Minimal ASP.NET Core 6 service that manages multi-puzzle BTC range assignments, hands out randomised chunks to workers, and exposes live statistics (plus an admin API) for the frontend.

## Features

- Register workers (BitCrack, VanitySearch, or custom) and issue client tokens.
- Assign deterministic HEX ranges from a configurable pool backed by SQLite.
- Track worker telemetry: speed, progress, GPU count, status.
- Expose `/api/stats/overview` for the dashboard along with worker and range summaries.
- Simple range lifecycle: claim → report → auto-assign next chunk on completion.

## Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- SQLite is used as the embedded store (`pool.db` in the project folder).

> ℹ️ If `dotnet restore` fails because of sandbox restrictions, set a local CLI home before running commands:  
> `DOTNET_CLI_HOME=.dotnet dotnet restore`

## Configuration

`appsettings.json` holds global defaults and optional seed entries.

```jsonc
"PoolOptions": {
  "Puzzle": "71",
  "RangeStartHex": "926FB80",
  "RangeEndHex": "926FBFF",
  "RangeChunkSize": 4,
  "WorkloadStartSuffix": "000000000",
  "WorkloadEndSuffix": "FFFFFFFFF",
  "WorkerOfflineAfter": "00:02:00",
  "TelegramBotToken": "",
  "TelegramChatId": "",
  "AdminApiKey": "",
  "SeedPuzzles": [
    {
      "Code": "71",
      "DisplayName": "Puzzle 71 (hex length 18)",
      "TargetAddress": "",
      "MinPrefixHex": "400000000000000000",
      "MaxPrefixHex": "7FFFFFFFFFFFFFFFFF",
      "PrefixLength": 18,
      "ChunkSize": 4,
      "Enabled": true,
      "Randomized": true,
      "Weight": 1,
      "Notes": "Fill target address for puzzle 71."
    },
    {
      "Code": "72",
      "DisplayName": "Puzzle 72 (hex length 18)",
      "TargetAddress": "",
      "MinPrefixHex": "800000000000000000",
      "MaxPrefixHex": "FFFFFFFFFFFFFFFFFF",
      "PrefixLength": 18,
      "ChunkSize": 4,
      "Enabled": true,
      "Randomized": true,
      "Weight": 1
    }
    // ... add more puzzles such as 73-79 ...
  ]
}
```

- These defaults (or any `SeedPuzzles`) seed the database on first launch; afterwards you can edit everything via the admin UI.
- The workload suffixes are appended to build full 64/76 bit key ranges for external miners.
- `WorkerOfflineAfter` controls the inactivity window shown on the dashboard.
- Add `TelegramBotToken`/`TelegramChatId` to receive key-found alerts.
- `AdminApiKey` gates the `/api/admin/*` endpoints (also used by `Frontend/admin.html`).
- `SeedPuzzles` is optional but convenient for loading puzzles 71‑79 (fill in each target address before shipping, or update via the admin UI later).

Adjust the values to match the puzzle you plan to target.

### Example: Seeding puzzles 71–79

The provided `SeedPuzzles` template covers nine contiguous puzzles (hex lengths 18–20). To finish the setup:

1. Look up the official target address for each puzzle (see [btcpuzzle.info/puzzle](https://btcpuzzle.info/puzzle)).
2. Copy those addresses into the corresponding `TargetAddress` fields in `SeedPuzzles` (or leave blank and add them later through the admin UI).
3. Adjust weights if you want to prioritise certain puzzles.
4. Start the backend—on first launch the definitions are inserted into SQLite.
5. Visit `/admin.html`, enter your `X-Admin-Key`, and confirm the nine entries exist. You can tweak ranges, toggle randomisation, or disable specific puzzles without restarting.

## Running the API

```bash
cd Backend/BitcrackPoolBackend
dotnet restore
dotnet run
```

The service listens on `http://localhost:5000` by default.

## REST Endpoints

### Register a worker

```bash
curl -X POST http://localhost:5000/api/clients/register \
  -H "Content-Type: application/json" \
  -d '{
        "user": "alice",
        "workerName": "rig-01",
        "applicationType": "bitcrack",
        "cardsConnected": 4,
        "gpuInfo": "4x RTX 4090",
        "clientVersion": "1.0.0"
      }'
```

Response:

```json
{
  "clientId": "9f91d6c8-...",
  "clientToken": "07c7c89c8abc4ff6b31c4e5cf92c3b21",
  "assignedRange": {
    "rangeId": "efc7...",
    "puzzle": "71",
    "prefixStart": "926FB80",
    "prefixEnd": "926FB83",
    "rangeStart": "926FB800000000000",
    "rangeEnd": "926FB83FFFFFFFFFF",
    "chunkSize": 4
  }
}
```

### Claim next range

```bash
curl -X POST http://localhost:5000/api/ranges/claim \
  -H "Content-Type: application/json" \
  -H "X-Client-Token: 07c7c8..." \
  -d '{ "clientId": "9f91d6c8-..." }'
```

- Returns the current assignment if one is already active.
- Returns `404` if the pool is exhausted.

### Report progress / completion

```bash
curl -X POST http://localhost:5000/api/ranges/report \
  -H "Content-Type: application/json" \
  -H "X-Client-Token: 07c7c8..." \
  -d '{
        "clientId": "9f91d6c8-...",
        "rangeId": "efc7...",
        "progressPercent": 100,
        "speedKeysPerSecond": 5.2e9,
        "cardsConnected": 4,
        "markComplete": true
      }'
```

- Marks the range as finished and immediately issues the next chunk when available.
- Update partial progress by sending `progressPercent` < `100`.

### Stats for the dashboard

```bash
curl http://localhost:5000/api/stats/overview
```

Contains overall progress (summed across puzzles), worker summaries, and active range snapshots for the frontend.

### Report key found (no leak to miner console)

## Next Steps

- Extend the API to broadcast key-found alerts (HTTP webhook, Telegram bot, etc.).
- Add authentication for administrative endpoints (range resets, manual assignments).
- Harden range recycling logic (timeouts, retries, abandoned work).
The backend stores the key, marks the assignment complete, and sends an optional Telegram alert. Keys never surface in the miner console when running in custom pool mode.

### Admin endpoints

All admin calls require `X-Admin-Key` header matching `PoolOptions.AdminApiKey`.

* List puzzles: `GET /api/admin/puzzles`
* Upsert puzzle: `PUT /api/admin/puzzles/{code}` (body: `PuzzleDefinitionDto`)
* Delete puzzle: `DELETE /api/admin/puzzles/{code}`

The static admin UI (`Frontend/admin.html`) consumes these endpoints and ships with the backend (served automatically alongside the dashboard).


### Building:
dotnet publish Backend/BitcrackPoolBackend/BitcrackPoolBackend.csproj \
    -c Release -o backend-publish