import changelogRaw from '../../CHANGELOG.md?raw';
import { parseChangelog } from '../utils/parseChangelog';

// Canary against the REAL CHANGELOG.md, not a mock — parseChangelog only understands `## `
// headings and single-line `- ` bullets (see CHANGELOG.md's own format note). Anything outside
// that subset silently renders as literal syntax or gets dropped, rather than erroring — this
// test is what actually catches drift, since parseChangelog.test.ts only exercises synthetic
// strings and ChangelogPage.test.tsx mocks the file entirely.
describe('CHANGELOG.md format', () => {
  const releases = parseChangelog(changelogRaw);

  it('has at least one parsed release', () => {
    expect(releases.length).toBeGreaterThan(0);
  });

  it('has no bold, italic, or link syntax in any entry', () => {
    for (const release of releases) {
      for (const entry of release.entries) {
        expect(entry).not.toMatch(/\*\*|\[.*\]\(/);
      }
    }
  });

  it('has no indented continuation lines that would silently drop from a bullet', () => {
    // A line indented under a `- ` bullet that isn't itself a new bullet/heading is a multi-line
    // entry the parser doesn't support — it just never becomes part of any entry, so the only way
    // to catch it is checking the raw text directly.
    const orphanedContinuation = changelogRaw
      .split('\n')
      .some((line) => /^\s+\S/.test(line) && !/^\s*-\s+/.test(line));
    expect(orphanedContinuation).toBe(false);
  });
});
