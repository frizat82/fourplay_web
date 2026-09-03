export interface ChangelogRelease {
  heading: string;
  entries: string[];
}

// Parses the small subset of Markdown CHANGELOG.md actually uses: `## ` release headings and
// `- ` bullets under them. Not a general Markdown parser — the changelog's own format is
// something we control, so there's no need for a full CommonMark dependency just to render it.
export function parseChangelog(raw: string): ChangelogRelease[] {
  const releases: ChangelogRelease[] = [];
  let current: ChangelogRelease | null = null;

  for (const line of raw.split('\n')) {
    const heading = line.match(/^##\s+(.+)/);
    if (heading) {
      current = { heading: heading[1].trim(), entries: [] };
      releases.push(current);
      continue;
    }
    const bullet = line.match(/^-\s+(.+)/);
    if (bullet && current) {
      current.entries.push(bullet[1].trim());
    }
  }

  return releases.filter((r) => r.entries.length > 0);
}
