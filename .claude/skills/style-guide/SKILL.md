---
name: style-guide
description: Color palette semantics, MUI button/status-indicator conventions, and the recurring contrast bugs to avoid. Invoke before touching any color, button variant, or status indicator (dot, chip, badge).
---

# Style Guide

`Client.React/src/app/theme.ts` is the single source of truth for every hex value. This
skill documents the *semantics* (which color means what) and the *conventions* (how to
apply a color to a component) so those decisions don't get re-litigated component by
component. If you're about to pick a color or add an opacity/mode branch to "fix" one,
read this first.

## Color semantics

| Palette key | Meaning | Where it's used |
|---|---|---|
| `success` | picked / positive / winning | "Picked" pick button, winning matrix cell, positive money |
| `info` | available / neutral action | unpicked-but-pickable button (e.g. "Pick" on `GameCard`) |
| `error` | negative / losing | losing matrix cell, negative money, "Loser" chip |
| `warning` | caution / one-off accent only | a single Rules-page callout row — **not** pick-state buttons |
| `secondary` | brand accent (orange) | CTAs, hero buttons, nav highlight |
| `primary` | structural navy (light) / blue (dark) | app bar, default text emphasis |

## Scores page pick badges: blue (`info`) means "my pick," full stop

On `ScoresPage.tsx`, the blue/`info` badge is reserved **exclusively** for the viewer's own
pick — never any other league member's pick, under any game state. `didUserPick()` sets this
unconditionally for the viewer's own team/pick, which is correct and shouldn't change. The bug
(2026-09-19, fixed in PR #409): `badgeColor()` — the *fallback* used for other users' picks —
returned `'info'` (blue) whenever `!isDecided(game)`, i.e. "game not decided yet, we don't know
if this pick won or lost." That's a reasonable-sounding default in isolation, but
`revealPicksForStartedGames` (`sportAdapter.ts`) reveals other users' picks once a game's real
kickoff time has passed, even before ESPN's live status has caught up — so there's a real window
where another user's pick is visible but the game still isn't "decided" by status, and it
rendered in the same blue meant only for your own pick. A user correctly called this out: "blue
circle is for only my picks - so I should never see 5 [when I only have 4]."

**Fix pattern — narrow the type, don't just patch the condition.** `badgeColor()`'s return type
was changed from `'success' | 'error' | 'info' | 'default'` to `'success' | 'error' | 'default'`
— dropping `'info'` entirely. This makes "only `didUserPick()` can ever produce blue" a
compile-time invariant instead of a runtime condition someone could reintroduce by tweaking a
branch. Prefer this shape of fix — shrink the return type / union so the wrong value is
unrepresentable — over adding another `if` that special-cases the specific trigger you just
found; the next trigger condition you haven't thought of yet will slip past a condition-only fix
but not a type-only one.

**`warning` (amber) is explicitly not used for pick-state buttons.** It was tried for the
"available to pick" state and reported unreadable in both light and dark mode as a small
filled button — `info` (blue) replaced it. Don't reintroduce amber there.

## The mode-contrast trap (read this before adding an opacity branch)

Every color in `theme.ts` is already tuned to be legible in **both** light and dark mode
at full opacity — `success`/`warning` use a darker shade in light mode specifically so
text/borders don't wash out on white, while dark mode keeps the punchier saturated tone.

This repo has twice shipped a bug where a component tried to "fix" contrast locally with
a mode-dependent `opacity` value (e.g. `opacity: mode === 'dark' ? 0.7 : 0.15` on a status
dot) — it fixed one mode and broke the other, then got reported as a regression once the
first mode's fix went out. **Don't add per-component opacity/mode branches.** If a color
reads poorly somewhere:
1. First check whether `theme.ts` already has a light/dark variant for that palette key
   (`success`/`warning` do; most others don't yet).
2. If it doesn't and needs one, add it there — one definition, both modes covered — not a
   local opacity hack.
3. Status dots, chips, and filled buttons should render at **full opacity** in both modes.

## MUI button conventions

- **Filled (`variant="contained"`) for both states of a binary pick control**, distinguished
  by color (`info` = available, `success` = picked) — not by outlined-vs-filled. Two
  differently-colored filled buttons read as "pick one of these"; an outlined button next
  to a filled one was tried and reported as looking like a lesser/broken version of the
  filled one, not a deliberate second state.
- **MUI's `disabled` prop flattens `variant="contained"` to uniform gray regardless of
  `color`.** Since locked/disabled is the majority real-world state once a pick window
  closes, this silently erases whatever color distinction you just built. Override it
  explicitly:
  ```tsx
  const lockedFillSx = (color: 'success' | 'info') => ({
    '&.Mui-disabled': {
      color: `${color}.contrastText`,
      backgroundColor: `${color}.main`,
      opacity: 0.6,
    },
  });
  <Button color={color} variant="contained" disabled sx={[baseSx, lockedFillSx(color)]} />
  ```
  See `GameCard.tsx`'s `lockedFillSx` for the live implementation.

## Page background gradient (`global.css` `body`) — four bugs, one subsystem

The site background — two decorative radial highlights plus a `linear-gradient(180deg, var(--bg-1), var(--bg-2))`
— has caused the "background looks inconsistent" report **four separate times**
(2026-09-02, 09-03, 09-04, 09-15), each a genuinely different bug in the same area. Read this before
touching `global.css`'s `body` rule, `theme.ts`'s `MuiPaper`/`MuiCard` overrides or `palette.background`,
or the `--bg-1`/`--bg-2`/`background.paper` color trio, so the next report doesn't require re-deriving
all four from scratch:

1. **Dark-mode elevation overlay** (theme.ts `MuiPaper` styleOverrides). MUI bakes a translucent
   white gradient onto elevated (non-`outlined`) dark-mode `Paper`/`Card` as a stand-in for a drop
   shadow — an outer elevated `Card` wrapping inner `Paper variant="outlined"` sections rendered
   visibly lighter than them. Fixed via `backgroundImage: isDark ? 'none' : undefined` on
   `MuiPaper`'s root override (cascades to `Card` too, since `Card` extends `Paper` — don't add a
   separate `MuiCard` override for this).
2. **`--bg-2` / `background.paper` color collision.** These are two independently-maintained color
   systems (`global.css` CSS custom properties vs. `theme.ts`'s MUI palette) with no shared source
   of truth. They drifted into the exact same hex value once, so cards near the bottom of the page
   gradient (where it settles at `--bg-2`) became invisible against the page. **Never let `--bg-2`
   (dark or light) equal `theme.ts`'s `background.paper` for that mode** — keep a visible gap
   between them, verified by rendering a page long enough to actually reach the gradient's bottom.
3. **Background retiling every viewport height** (the deep one — root cause of most reports,
   including making bug 2 look worse than it was). `html, body { height: 100% }` pins `body`'s own
   box to exactly `100vh` even though real page content scrolls far past that. With no
   `background-repeat` set (CSS default: `repeat`), every layer — both radial highlights included —
   retiled every exact `100vh` down the page: a real seam at every tile boundary, invisible on any
   page shorter than one screen (which is why a quick check on a short page always looked "fixed").
   Worse on mobile, where a shorter viewport means more tile boundaries fit in a normal scroll, and
   the two asymmetric radial highlights (anchored top-left vs. top-right) re-draw at each one and
   visibly drift apart. Fixed by giving `body` a `background-color: var(--bg-2)` base layer (so
   there's never a gap, seam-free by construction since it matches the gradient's own end color)
   plus `background-image` holding the gradients with `background-repeat: no-repeat` and
   `background-size: 100% 100vh`, so they render exactly once at the top as originally intended.
4. **MUI `CssBaseline`-vs-`global.css` collision** (2026-09-15, frizat-2ey). `main.tsx` renders
   `<CssBaseline/>`, which unconditionally sets `body`'s `background-color` from
   `theme.ts`'s `palette.background.default` — a THIRD color system, independent of both `--bg-2`
   and `background.paper`, that nothing had ever pinned to `--bg-2`. `background.default` had
   drifted to `#f9fafb`/`#0f1729` while `--bg-2` was `#f1f4f8`/`#121a2f` — CssBaseline silently
   overrode `global.css`'s own `body { background-color: var(--bg-2) }` rule for that one
   property only (leaving `background-image` — the gradient itself — untouched, which is why the
   gradient still looked right at the very top of the page but the flat fill below it didn't
   match). Visible in light mode (`#f9fafb` vs `#f1f4f8` is a perceptible near-white-vs-gray-blue
   gap) but not dark (`#0f1729` vs `#121a2f` is too small an absolute delta among very dark tones
   to see) — that asymmetry is why this bug always reads as "light is broken, dark is fine" even
   though both modes are technically mismatched. Fixed by hardcoding `background.default` in
   `theme.ts` to the literal same hex as `--bg-2` for both modes, with a comment cross-referencing
   `global.css` (the two systems still can't reference each other directly — same constraint as
   bug 2 — so the only guard against re-drift is the comment plus the e2e test below). **This is
   the bug to suspect first** if a background report says "the gradient/top looks right, it's the
   flat area below it that's wrong" and bug 3's fix (background-color base fill) is already in
   place — bug 3 broke the *tiling*, this one breaks the *color* of that same base fill.

**Before shipping any future fix in this area**: render (screenshot or pixel-sample) a page long
enough to scroll past one full viewport, in both themes — a short page or a single-viewport
screenshot cannot catch bugs 1–3. `python3`/`PIL` pixel-sampling a vertical strip outside any
card, looking for a sudden channel jump, is the fastest way to confirm a seam is really gone.
For bug 4's class specifically (a flat-fill color mismatch, not a tiling/seam issue), the
`e2e/pageBackground.spec.ts` Playwright test asserts `getComputedStyle(document.body).backgroundColor`
resolves to the same value as `--bg-2` in both themes — run it (or add to it) before touching
`theme.ts`'s `palette.background` or `global.css`'s `--bg-2`, so a future edit that re-diverges
the two fails CI instead of shipping.

## Layout: numeric columns need explicit centering

A fixed-width column showing numbers of varying digit width (e.g. a spread like `-3.5` vs
`+16.5`) needs `textAlign: 'center'` set explicitly — the `.fixed-width` CSS class
(`global.css`) only sets `width`, not alignment, so left-aligned numbers visually drift
between rows. This was shipped once, reported as "spread alignment is totally off," and
fixed by adding `sx={{ textAlign: 'center' }}` alongside `className="fixed-width"`. Any
new fixed-width numeric column needs the same treatment.

## Before you touch a color

1. Check this skill for the semantic meaning you actually want (picked vs available vs
   negative vs caution) — don't reach for whichever color looks closest.
2. If the color needs to differ from what's already in `theme.ts`, fix it in `theme.ts`
   for both modes, not with a local opacity/sx override.
3. If it's a `disabled` state on a filled button, use the `lockedFillSx` pattern above —
   don't let it fall back to MUI's default gray.
4. Test both light and dark mode before calling it done — a fix verified in only one mode
   is how this cycle started in the first place.
