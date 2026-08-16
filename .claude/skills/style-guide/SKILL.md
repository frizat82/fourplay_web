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
