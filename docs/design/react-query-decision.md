# Design: React Query — Adopt (scoped), don't remove

**Bead:** frizat-mon.1 · **Epic:** frizat-mon · **Date:** 2026-07-27 · **Author:** Claude (Fable 5)

## Decision

**Adopt `@tanstack/react-query` for the two polling pages (PicksPage, ScoresPage).**
LeaderboardPage and session.tsx stay hand-rolled (one-shot loads; no benefit).
Do not remove the dependency — it is already installed and provider-wired in `main.tsx`.

## Why not remove it

The minimal hand-rolled fix for the poll bugs (~15 lines/page: a `firstLoad` ref +
preserving `userPicks`) leaves three problems:

1. **Three copies of the same machinery.** PicksPage and ScoresPage each hand-roll
   loading state, `setInterval` polling, page-visibility pause, and current-vs-historical
   week juggling. ScoresPage additionally hand-rolls SSE-triggered reloads and a data
   fingerprint hack (`fp(prev) === fp(result)`, ScoresPage.tsx:84-85) added to fight
   re-render churn — evidence the hand-rolled path has already cost debugging time.
2. **The pending-picks wipe is a symptom of mixed state.** `applyLoaded` owns both
   server data and local selection state, so every refresh resets `userPicks`
   (PicksPage.tsx:67). Any hand-rolled fix must remember to thread pending state
   through every reload path, forever.
3. **The dep is already paid for.** Bundle cost is sunk; `QueryClient` is instantiated;
   removal is its own PR with zero user-facing benefit.

## What RQ buys, mapped to current bugs

| Current bug / hack | RQ mechanism |
|---|---|
| Poll flashes full-page spinner (both pages) | `isLoading` (first load only) vs `isFetching` (background) — render spinner only on `isLoading` |
| Poll wipes pending picks (PicksPage.tsx:67) | Server data lives in query cache; pending `userPicks` stays in `useState` — refetch never touches it |
| SSE event → spinner flash (ScoresPage.tsx:139) | `es.onmessage = () => queryClient.invalidateQueries({ queryKey })` — background refetch, no loading state change |
| Page-visibility pause effect (both pages) | `refetchIntervalInBackground: false` (default) + `refetchOnWindowFocus` |
| Fingerprint identity hack (ScoresPage.tsx:84-85) | RQ structural sharing keeps referential identity when data is unchanged — delete the hack |
| Week navigation refetches everything, blanks page | `queryKey: [sport, 'scores', leagueId, season, week, isPostSeason]` + `placeholderData: keepPreviousData` — cached weeks render instantly |
| Adaptive poll interval (active games 1× vs 4×) | `refetchInterval: (query) => query.state.data?.hasActiveGames ? base : base * 4` |

## Migration plan

### Step 1 — PicksPage (bead frizat-mon.5, do first)

- Replace `reload`/`loadHistoricalWeek`/`loading`/`games`/etc. state with:
  ```ts
  const weekState = isCurrentWeek ? null : { season, week, isPostSeason };
  const { data, isLoading } = useQuery({
    queryKey: [adapter.sport, 'picks', currentLeague, user?.userId, weekState],
    queryFn: () => weekState
      ? adapter.loadHistoricalGames(currentLeague!, user!.userId, weekState)
      : adapter.loadCurrentGames(currentLeague!, user!.userId),
    enabled: leaguesLoaded && !!currentLeague && !!user?.userId,
    refetchInterval: weekState ? false : adapter.pollIntervalMs,
  });
  ```
- `existingPicks` derives from `data.userPicks` via `useMemo` — not `useState`.
- Pending `userPicks` remains `useState<Set<string>>` — untouched by refetch.
- On submit success: `queryClient.invalidateQueries({ queryKey: [adapter.sport, 'picks'] })`
  replaces manual `reload()`.
- Conflict rule: `useMemo` drops pending keys that now exist in `existingPicks` or whose
  game became locked (toast once via effect).
- Jersey cache: separate `useQuery` keyed `[sport, 'jerseys', season, week]`,
  `staleTime: Infinity`.

### Step 2 — ScoresPage

- Same queryKey shape with `'scores'`; move SSE effect to
  `invalidateQueries` instead of `reload()`; delete fingerprint hack and
  visibility effect; keep `refetchInterval` callback for the active-games 1×/4× logic.
- The "historical week == frozen current week" special case (ScoresPage.tsx:102-111)
  survives as-is inside the queryFn selection.

### Step 3 — (optional, later) LeaderboardPage / session

Only if a concrete need appears (e.g. caching leaderboards across league switches).
Not part of this epic.

## Test impact

- **Vitest:** wrap rendered pages in a `QueryClientProvider` test helper
  (`src/test/`, `new QueryClient({ defaultOptions: { queries: { retry: false } } })`).
  Existing assertions about rendered games/picks stay valid; assertions that spy on
  reload timing change to waiting on rendered output (`findBy*`), which is what most
  already do.
- **e2e (mock + demo):** unaffected — RQ still issues the same HTTP requests through
  axios/fetch; `page.route` interception in `e2e/helpers/routes.ts` works unchanged.
- **New red-first tests for mon.5** (write before migrating):
  1. pending picks survive a background refetch,
  2. background refetch does not unmount the grid (no spinner),
  3. pending pick on a game that became locked is dropped with a toast.

## Risks

- **Query key discipline:** current-week vs historical must be part of the key or
  navigation will show stale week data. Mitigated by the `weekState` pattern above.
- **Double-fetch on mount in StrictMode dev:** expected RQ behavior, harmless.
- **`enabled` guards:** the `!currentLeague → <NoLeague/>` early return must come
  before using `data`, same as today.
