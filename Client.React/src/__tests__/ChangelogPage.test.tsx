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

  it('renders each entry as a real <li> element, one per bullet across all releases', async () => {
    // Locks in native list semantics (a plain <ul>/<li>, not a MUI ListItem CSS override) — the
    // override previously used (ListItem { display: 'list-item' }) pulled ListItemText out of
    // the flex layout MUI's own styles assume, which was the fragile point behind long bullets
    // visually overflowing their Paper card.
    await renderWithChangelog(nonEmptyChangelog);

    expect(document.querySelectorAll('li').length).toBe(3); // 2 entries + 1 entry across the 2 releases
  });

  it('wraps a long bullet within its own list item rather than letting it overflow', async () => {
    const longEntry = 'Fixed: ' + 'a very long changelog entry that must wrap onto multiple lines'.repeat(6);
    await renderWithChangelog(`# Changelog\n\n## 2026-09-06\n\n- ${longEntry}\n`);

    const item = screen.getByText(/Fixed: a very long changelog entry/).closest('li');
    expect(item).not.toBeNull();
    // A real <li> has no fixed width/height/overflow constraints of its own — this assertion
    // documents the intent (no clipping container) rather than measuring pixel layout, which
    // jsdom cannot do reliably; the actual visual fix is confirmed by a browser screenshot.
    expect(item).not.toHaveStyle({ overflow: 'hidden' });
    expect(item).not.toHaveStyle({ whiteSpace: 'nowrap' });
  });
});
