#!/usr/bin/env node
/**
 * Downloads one real ESPN team logo PNG per NFL team, and EVERY CFB team ESPN has (FBS/FCS/D2 —
 * roughly 760), into Client.React/public/Icons/Logos/{nfl,cfb}/ — a one-time local cache so the
 * app never hits ESPN for a logo at request time (frizat-cwj). Sport-scoped subfolders (not a
 * flat abbr-keyed folder like Helmets) because a handful of abbreviations are shared by an NFL
 * team and a CFB team (MIA: Dolphins/Hurricanes, CIN: Bengals/Bearcats, TEN: Titans/Volunteers).
 *
 * CFB intentionally does NOT filter down to generate-helmets.js's curated CFB_TEAMS (Top 25 +
 * bowl regulars, used for the synthetic-badge color generator) — real logos need no hand-picked
 * color, so there's no reason to limit them to that smaller list. A CFB pick'em league can face
 * any FBS/FCS opponent, not just ranked ones (reported live: UTEP had no logo under the old
 * curated-list-only download). The ~760-team universe does occasionally reuse an abbreviation
 * between two small/lower-division schools — that team's logo silently wins the shared file, the
 * loser falls back to the text badge (TeamLogo.tsx's existing onError path); not worth resolving
 * for schools this obscure.
 *
 * Re-run this after a team rebrand/relocation, a new team joining FBS/FCS, or when a new NFL team
 * is added to generate-helmets.js.
 *
 * Run: node scripts/download-team-logos.js
 */

const fs = require('fs');
const path = require('path');
const { NFL_TEAMS } = require('./generate-helmets');

const OUT_DIR = path.join(__dirname, '..', 'Client.React', 'public', 'Icons', 'Logos');

// Abbreviations in generate-helmets.js's NFL_TEAMS that no longer match ESPN's current
// abbreviation for that team (renames/relocations since it was written) — resolved by this alias
// instead. Only needed for NFL: CFB downloads every abbreviation ESPN itself reports, so there's
// nothing to reconcile there. (This app's own WAS/JAC <-> ESPN's WSH/JAX quirk is also encoded
// server-side in Shared/Helpers/NFLTeamMappingHelpers.cs's NflTeamAbbrMapping — that table exists
// to translate ESPN's abbreviation back to this app's stable one when parsing live data; this one
// does the reverse, one-time, to find the right ESPN logo to download. Not directly shareable
// across the C#/Node boundary, but keep them in sync if either changes.)
const NFL_ESPN_ABBR_ALIASES = {
  WAS: 'WSH', // Washington Commanders — ESPN uses WSH
  JAC: 'JAX', // Jacksonville Jaguars — ESPN uses JAX
};

async function fetchJson(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url} → HTTP ${res.status}`);
  return res.json();
}

async function loadEspnTeams(sportPath) {
  const data = await fetchJson(`https://site.api.espn.com/apis/site/v2/sports/football/${sportPath}/teams?limit=1000`);
  const teams = data.sports[0].leagues[0].teams.map((t) => t.team);
  const byAbbr = new Map();
  for (const t of teams) {
    const logo = t.logos?.[0]?.href;
    if (logo) byAbbr.set(t.abbreviation.toUpperCase(), logo);
  }
  return byAbbr;
}

async function downloadTo(url, filePath) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url} → HTTP ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  fs.writeFileSync(filePath, buf);
}

// CFB's ~760 teams downloaded all-at-once (one Promise.all over every abbr) blew past ESPN's
// CDN's concurrent-connection tolerance and started timing out — a bounded-concurrency batch
// avoids that while still being far faster than one-at-a-time.
const CONCURRENCY = 20;

async function downloadSport(sportKey, abbrs, espnTeams, aliases = {}) {
  const outDir = path.join(OUT_DIR, sportKey);
  fs.mkdirSync(outDir, { recursive: true });

  const results = [];
  for (let i = 0; i < abbrs.length; i += CONCURRENCY) {
    const batch = abbrs.slice(i, i + CONCURRENCY);
    const batchResults = await Promise.all(batch.map(async (abbr) => {
      const lookupAbbr = aliases[abbr] || abbr;
      const logoUrl = espnTeams.get(lookupAbbr);
      if (!logoUrl) return { abbr, ok: false };
      await downloadTo(logoUrl, path.join(outDir, `${abbr.toLowerCase()}.png`));
      process.stdout.write(`  [${sportKey}] ${abbr.padEnd(6)} ✓\n`);
      return { abbr, ok: true };
    }));
    results.push(...batchResults);
  }

  const downloaded = results.filter((r) => r.ok).length;
  const missing = results.filter((r) => !r.ok).map((r) => r.abbr);

  console.log(`\n✓ ${sportKey}: downloaded ${downloaded}/${abbrs.length} team logos → ${outDir}`);
  if (missing.length > 0) {
    console.log(`✗ ${sportKey}: could not resolve an ESPN logo for: ${missing.join(', ')}`);
    console.log('  Add an alias entry above if the team has renamed/relocated,');
    console.log('  or leave it — TeamLogo.tsx falls back to the text/helmet badge for these.');
  }
}

async function main() {
  console.log('Fetching ESPN NFL + CFB team lists...');
  const [nflTeams, cfbTeams] = await Promise.all([
    loadEspnTeams('nfl'),
    loadEspnTeams('college-football'),
  ]);

  await Promise.all([
    downloadSport('nfl', Object.keys(NFL_TEAMS), nflTeams, NFL_ESPN_ABBR_ALIASES),
    downloadSport('cfb', [...cfbTeams.keys()], cfbTeams),
  ]);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
