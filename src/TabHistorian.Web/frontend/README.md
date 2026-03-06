# TabHistorian Web Frontend

Next.js SPA served by the ASP.NET backend (`TabHistorian.Web`). Built as a static export and copied to `wwwroot/` at build time.

## Development

```bash
bun install
bun dev
```

Runs on `http://localhost:3000` with hot reload. The API proxy rewrites `/api/*` to the ASP.NET backend at `http://localhost:17000`.

## Build

```bash
bun run build
```

Runs `next build` (static export) then copies the output to `../wwwroot/` via `scripts/copy-to-wwwroot.mjs`.

## Pages

- `/` — Tab Machine: event-sourced tab search and time travel
- `/snapshots` — Full Snapshots: browse with live search, infinite scroll, profile filtering
- `/snapshots/explore` — Explorer: hierarchical drill-down (snapshot > profile > window > tab)
