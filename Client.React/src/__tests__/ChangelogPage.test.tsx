import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

// The real CHANGELOG.md is bundled at build time via Vite's ?raw import — mock it here so these
// tests don't churn every time someone adds a real entry to the file. vi.mock is hoisted, so the
// mocked content has to be inlined per-test via vi.doMock + dynamic import rather than a shared
// module-level page import, since ChangelogPage computes `releases` once at import time.
const nonEmptyChangelog = `# Changelog

## 2026-09-03

- Changed: NFL pricing
- Removed: Scores Share button

## 2026-09-02

- Fixed: existing-user invite notifications
`;

async function renderWithChangelog(raw: string) {
  vi.resetModules();
  vi.doMock('../../CHANGELOG.md?raw', () => ({ default: raw }));
  const { default: AdminChangelogPage } = await import('../pages/admin/ChangelogPage');
  render(<AdminChangelogPage />);
}

describe('AdminChangelogPage', () => {
  it('shows each release heading with its entries', async () => {
    await renderWithChangelog(nonEmptyChangelog);

    expect(screen.getByText('2026-09-03')).toBeInTheDocument();
    expect(screen.getByText('Changed: NFL pricing')).toBeInTheDocument();
    expect(screen.getByText('Removed: Scores Share button')).toBeInTheDocument();
    expect(screen.getByText('2026-09-02')).toBeInTheDocument();
    expect(screen.getByText('Fixed: existing-user invite notifications')).toBeInTheDocument();
  });

  it('renders releases in document order, most recent first', async () => {
    await renderWithChangelog(nonEmptyChangelog);

    const headings = screen.getAllByRole('heading', { level: 6 }).map((h) => h.textContent);
    expect(headings).toEqual(['2026-09-03', '2026-09-02']);
  });

  it('shows an empty state when the changelog has no parseable releases', async () => {
    await renderWithChangelog('# Changelog\n\nNothing shipped yet.\n');

    expect(screen.getByText(/no entries yet/i)).toBeInTheDocument();
  });
});
