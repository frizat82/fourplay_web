import { parseChangelog } from '../utils/parseChangelog';

describe('parseChangelog', () => {
  it('groups bullet entries under their release heading', () => {
    const raw = `# Changelog

## 2026-09-03

- Changed: NFL pricing
- Removed: Scores Share button
`;

    expect(parseChangelog(raw)).toEqual([
      { heading: '2026-09-03', entries: ['Changed: NFL pricing', 'Removed: Scores Share button'] },
    ]);
  });

  it('returns multiple releases in document order (most recent first, matching the file)', () => {
    const raw = `# Changelog

## 2026-09-03

- Newest entry

## 2026-09-02

- Older entry
`;

    expect(parseChangelog(raw)).toEqual([
      { heading: '2026-09-03', entries: ['Newest entry'] },
      { heading: '2026-09-02', entries: ['Older entry'] },
    ]);
  });

  it('ignores the top-level title and blank lines', () => {
    const raw = `# Changelog

Some intro text that isn't a bullet.

## 2026-09-03

- Only bullets become entries
`;

    expect(parseChangelog(raw)).toEqual([
      { heading: '2026-09-03', entries: ['Only bullets become entries'] },
    ]);
  });

  it('returns an empty array for a document with no release headings', () => {
    expect(parseChangelog('# Changelog\n')).toEqual([]);
  });

  it('returns an empty array for empty input', () => {
    expect(parseChangelog('')).toEqual([]);
  });

  it('skips a release heading with no bullets under it', () => {
    const raw = `## 2026-09-03

## 2026-09-02

- Has an entry
`;

    expect(parseChangelog(raw)).toEqual([{ heading: '2026-09-02', entries: ['Has an entry'] }]);
  });
});
