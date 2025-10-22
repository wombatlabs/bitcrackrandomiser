# Bitcrack Pool Frontend

Static dashboard that consumes the backend `/api/stats/overview` endpoint and visualises worker activity, range progress, and search completion.

## Running Locally

Serve the directory with any static web server (the backend also serves it automatically, including the admin interface).

Example using Python:

```bash
cd Frontend
python3 -m http.server 8080
```

Visit `http://localhost:8080` and ensure the backend API is reachable at the same origin or adjust `API_ENDPOINT` inside `app.js`. When the backend runs via `dotnet run` it automatically exposes these files so the full site (dashboard + `admin.html`) can sit behind `https://btcmultipuzzle.com`.

## Admin Console

- `admin.html` + `admin.js` use `X-Admin-Key` and the `/api/admin/puzzles` endpoints to manage puzzle bounds, chunk sizes, and weights.
- Enter the admin key once and it is stored in `localStorage` for convenience.

## Customising

- Update styles in `styles.css` for branding.
- Tweak refresh cadence or columns inside `app.js`.
- The dashboard treats workers as offline if no heartbeat arrives within two minutes (configurable via `OFFLINE_THRESHOLD_MS`).
