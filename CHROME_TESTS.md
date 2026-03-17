# Chrome DevTools MCP Regression Testing

This document describes how we use the Chrome DevTools MCP server to run live browser regression
tests against the running dev stack, and records findings and fixes from each session.

---

## Setup

### Prerequisites
- Backend running: `dotnet run --project Server/FourPlayWebApp.Server.csproj --launch-profile FourPlayWebApp.Server`
- Frontend running: `cd Client.React && npm run dev` (port 5173)
- Chrome DevTools MCP available in the Claude Code session

### Important: Vite Proxy Restart
The Vite dev server must be started **after** the backend is running. If Vite starts before the
backend, its proxy enters a broken state where all `/api/*` requests return the SPA fallback
(GET→200 HTML, POST→404). Symptoms:
- `POST /api/auth/login` returns `404` through Vite proxy but `401` directly on port 7209
- `GET /api/auth/me` returns `200 text/html` instead of proxying to backend

Fix: kill and restart the Vite process.
```bash
pkill -f "node.*vite" && cd Client.React && npm run dev &
```

---

## How to Run

Use the Chrome DevTools MCP tools in sequence:

1. `list_pages` — confirm the browser has a page open
2. `navigate_page` — go to the URL under test
3. `take_snapshot` — inspect the a11y tree (preferred over screenshots for assertions)
4. `fill` + `click` — interact with form elements using `uid` from the snapshot
5. `wait_for` — wait for expected text/elements to appear
6. `take_screenshot` — visual confirmation when needed

---

## Test Flows Covered

### 1. Home Page (`/`)
| Check | Result |
|-------|--------|
| Hero section renders ("Elevate Your Fantasy Game") | ✅ Pass |
| Logo image visible | ✅ Pass |
| "Log In to Start" CTA button present | ✅ Pass |
| "Create Account" CTA button present | ✅ Pass |
| Top-right Login / Register nav buttons | ✅ Pass |
| "Why Choose FourPlay?" section below fold | ✅ Pass |

### 2. Login Page (`/account/login`)
| Check | Result |
|-------|--------|
| Page title "Log in" | ✅ Pass |
| Email + Password fields present | ✅ Pass |
| Remember me checkbox present | ✅ Pass |
| "Forgot your password?" button present | ✅ Pass |
| "Need an account? Register" button present | ✅ Pass |
| Submit with bad credentials shows error toast | ✅ Pass (after fix) |
| Submit with empty fields shows validation error | ✅ Pass |

### 3. Register Page (`/account/register`)
| Check | Result |
|-------|--------|
| Invitation Code, Username, Email, Password, Confirm Password fields | ✅ Pass |
| Invite-only notice visible | ✅ Pass |

### 4. Forgot Password Page (`/account/forgotpassword`)
| Check | Result |
|-------|--------|
| Email field + "Reset password" button | ✅ Pass |

### 5. Protected Route Redirects (unauthenticated)
| Route | Redirects To | Result |
|-------|-------------|--------|
| `/picks` | `/account/login?returnUrl=%2Fpicks` | ✅ Pass |
| `/scores` | `/account/login?returnUrl=%2Fscores` | ✅ Pass |
| `/leaderboard` | `/account/login?returnUrl=%2Fleaderboard` | ✅ Pass |

---

## Bugs Found & Fixed

### Bug 1: Vite Proxy POST 404 (Session 2026-03-09)
**Symptom:** `POST /api/auth/login` through `http://localhost:5173` returned 404 with empty body.
All GET requests to `/api/*` returned `200 text/html` (SPA fallback) instead of proxying.

**Root Cause:** Vite dev server was started before the backend was running (process from Mar 4,
backend only started Mar 9). The proxy entered a broken state.

**Fix:** Killed stale Vite process and restarted it after backend was confirmed running.

---

### Bug 2: No Error Feedback on Failed Login (Session 2026-03-09)
**Symptom:** Submitting the login form with bad credentials showed no error — form just reset to
its initial state silently.

**Root Cause:** The backend returns a bare `401 Unauthorized` (no body) when the user is not
found. Axios throws on non-2xx responses. The `login()` function in `auth.tsx` did not catch the
throw, so it propagated to react-hook-form's `handleSubmit`, which swallows uncaught errors
without showing any UI feedback.

**Fix:** Added a `try/catch` in `auth.tsx` `login()` that returns a `SignInResultDto` with
`succeeded: false, message: 'Invalid credentials'` on any HTTP error. The existing `onSubmit`
in `LoginPage.tsx` already called `toast.push(result.message, 'error')` on `!result.succeeded`,
so no changes were needed to the page component.

**File:** `Client.React/src/services/auth.tsx`

---

## Playwright E2E Tests

Playwright tests are in `Client.React/e2e/` and mirror the Chrome MCP flows above as repeatable
automated assertions. See `Client.React/playwright.config.ts` for configuration.

### Setup
```bash
cd Client.React
npm install
npx playwright install chromium --with-deps
```

### Run
```bash
# With dev server already running on port 5173:
npx playwright test --reporter=list

# Or via npm script:
npm run test:e2e
```

### Test Files
| File | Covers |
|------|--------|
| `e2e/home.spec.ts` | Home page hero, nav links, CTA buttons |
| `e2e/auth.spec.ts` | Login form, error feedback, register, forgot password |
| `e2e/navigation.spec.ts` | Unauthenticated redirects for all protected routes |
| `e2e/game-time-locking.spec.ts` | `/picks` redirect guard |
