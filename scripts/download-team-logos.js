#!/usr/bin/env node
/**
 * Downloads one real ESPN team logo PNG per team in generate-helmets.js's NFL_TEAMS/CFB_TEAMS
 * into Client.React/public/Icons/Logos/{nfl,cfb}/ — a one-time local cache so the app never hits
 * ESPN for a logo at request time (frizat-cwj). Sport-scoped subfolders (not a flat abbr-keyed
 * folder like Helmets) because a handful of abbreviations are shared by an NFL team and a CFB
 * team (MIA: Dolphins/Hurricanes, CIN: Bengals/Bearcats, TEN: Titans/Volunteers) — generate-
 * helmets.js keeps NFL_TEAMS/CFB_TEAMS as two separate objects for exactly this reason.
 *
 * Re-run this after a team rebrand/relocation or when a new team is added to generate-helmets.js.
 *
 * Run: node scripts/download-team-logos.js
 */

const fs = require('fs');
const path = require('path');
const { NFL_TEAMS, CFB_TEAMS } = require('./generate-helmets');

const OUT_DIR = path.join(__dirname, '..', 'Client.React', 'public', 'Icons', 'Logos');

// Abbreviations in generate-helmets.js that no longer match ESPN's current abbreviation for that
// team (renames/relocations since it was written) — resolved by this alias instead. Keyed by
// "sport:ABBR" since NFL and CFB are looked up in separate ESPN team lists. (This app's own
// WAS/JAC <-> ESPN's WSH/JAX quirk is also encoded server-side in
// Shared/Helpers/NFLTeamMappingHelpers.cs's NflTeamAbbrMapping — that table exists to translate
// ESPN's abbreviation back to this app's stable one when parsing live data; this one does the
// reverse, one-time, to find the right ESPN logo to download. Not directly shareable across the
// C#/Node boundary, but keep them in sync if either changes.)
const ESPN_ABBR_ALIASES = {
  'nfl:WAS': 'WSH', // Washington Commanders — ESPN uses WSH
  'nfl:JAC': 'JAX', // Jacksonville Jaguars — ESPN uses JAX
  'cfb:NCST': 'NCSU', // NC State — ESPN uses NCSU
  'cfb:TAMU': 'TA&M', // Texas A&M — ESPN uses TA&M
  'cfb:TEN': 'TENN', // Tennessee Volunteers — ESPN uses TENN
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

async function downloadSport(sportKey, abbrs, espnTeams) {
  const outDir = path.join(OUT_DIR, sportKey);
  fs.mkdirSync(outDir, { recursive: true });

  const results = await Promise.all(abbrs.map(async (abbr) => {
    const lookupAbbr = ESPN_ABBR_ALIASES[`${sportKey}:${abbr}`] || abbr;
    const logoUrl = espnTeams.get(lookupAbbr);
    if (!logoUrl) return { abbr, ok: false };
    await downloadTo(logoUrl, path.join(outDir, `${abbr.toLowerCase()}.png`));
    process.stdout.write(`  [${sportKey}] ${abbr.padEnd(6)} ✓\n`);
    return { abbr, ok: true };
  }));

  const downloaded = results.filter((r) => r.ok).length;
  const missing = results.filter((r) => !r.ok).map((r) => r.abbr);

  console.log(`\n✓ ${sportKey}: downloaded ${downloaded}/${abbrs.length} team logos → ${outDir}`);
  if (missing.length > 0) {
    console.log(`✗ ${sportKey}: could not resolve an ESPN logo for: ${missing.join(', ')}`);
    console.log('  Add an ESPN_ABBR_ALIASES entry above if the team has renamed/relocated,');
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
    downloadSport('nfl', Object.keys(NFL_TEAMS), nflTeams),
    downloadSport('cfb', Object.keys(CFB_TEAMS), cfbTeams),
  ]);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
