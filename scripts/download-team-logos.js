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

async function loadEspnTeams(sportPath, { includeScoreboardAbbrs = false } = {}) {
  const base = `https://site.api.espn.com/apis/site/v2/sports/football/${sportPath}/teams`;
  const data = await fetchJson(`${base}?limit=1000`);
  const teams = data.sports[0].leagues[0].teams.map((t) => t.team).filter((t) => t.logos?.[0]?.href);
  const byAbbr = new Map();
  for (const t of teams) byAbbr.set(t.abbreviation.toUpperCase(), t.logos[0].href);

  // The /teams list and the scoreboard (what our games store) disagree for some schools — e.g.
  // Air Force is AF in the list but AFA on the scoreboard. The per-team endpoint returns the
  // scoreboard code, so save each logo under that too, or those teams silently get no logo.
  if (includeScoreboardAbbrs) {
    for (let i = 0; i < teams.length; i += CONCURRENCY) {
      await Promise.all(teams.slice(i, i + CONCURRENCY).map(async (t) => {
        // One flaky request must not abort the whole run — that team keeps its list code.
        try {
          const detail = await fetchJson(`${base}/${t.id}`);
          const abbr = detail.team.abbreviation.toUpperCase();
          if (!byAbbr.has(abbr)) byAbbr.set(abbr, t.logos[0].href);
        } catch (err) {
          console.warn(`  ! could not fetch scoreboard code for ${t.abbreviation} (${t.id}): ${err.message}`);
        }
      }));
    }
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
      try {
        await downloadTo(logoUrl, path.join(outDir, `${abbr.toLowerCase()}.png`));
      } catch (err) {
        console.warn(`  ! [${sportKey}] ${abbr}: ${err.message}`);
        return { abbr, ok: false };
      }
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

// ESPN abbreviations are not unique across ~900 schools (e.g. ARK is both Arkansas and Arkansas
// Tech; WASH is both Washington and Washburn). Our games only ever involve FBS/FCS teams, as
// reported on the scoreboard, so those teams' scoreboard codes must always own their file — FBS
// first, then FCS. Pulled one day at a time because ESPN's scoreboard returns nothing for ranges.
async function loadScoreboardTeams(sportPath, groups, from, to) {
  const days = [];
  for (let d = new Date(from); d <= to; d.setUTCDate(d.getUTCDate() + 1)) days.push(d.toISOString().slice(0, 10).replace(/-/g, ''));
  const byAbbr = new Map();
  const ownerId = new Map();
  const collisions = new Set();
  for (const group of groups) {
    for (let i = 0; i < days.length; i += CONCURRENCY) {
      const pages = await Promise.all(days.slice(i, i + CONCURRENCY).map((day) =>
        fetchJson(`https://site.api.espn.com/apis/site/v2/sports/football/${sportPath}/scoreboard?dates=${day}&groups=${group}&limit=300`)
          .catch((err) => { console.warn(`  ! scoreboard ${group}/${day}: ${err.message}`); return { events: [] }; })));
      for (const page of pages) {
        for (const e of page.events ?? []) {
          for (const c of e.competitions[0].competitors) {
            const abbr = c.team.abbreviation?.toUpperCase();
            if (!abbr || !c.team.logo) continue;
            const owner = ownerId.get(abbr);
            if (owner === undefined) {
              ownerId.set(abbr, c.team.id);
              byAbbr.set(abbr, c.team.logo);
            } else if (owner !== c.team.id) {
              collisions.add(`${abbr}: kept team ${owner}, skipped ${c.team.displayName} (${c.team.id})`);
            }
          }
        }
      }
    }
  }
  // Two scoreboard schools sharing one code means one of them shows the other's logo — flag it
  // for a human to check rather than silently picking one.
  for (const c of collisions) console.warn(`  ! scoreboard code collision — ${c}`);
  return byAbbr;
}

async function main() {
  console.log('Fetching ESPN NFL + CFB team lists...');
  const [nflTeams, cfbTeams] = await Promise.all([
    loadEspnTeams('nfl'),
    loadEspnTeams('college-football', { includeScoreboardAbbrs: true }),
  ]);

  // Season window covering both the current/most recent season (Aug through the CFP title game).
  const now = new Date();
  const thisYearStart = Date.UTC(now.getUTCFullYear(), 7, 15);
  const seasonYear = now.getTime() >= thisYearStart ? now.getUTCFullYear() : now.getUTCFullYear() - 1;
  const seasonStart = new Date(Date.UTC(seasonYear, 7, 15));
  const seasonEnd = new Date(Math.min(Date.UTC(seasonYear + 1, 0, 25), now.getTime()));
  console.log(`Fetching FBS/FCS scoreboard teams ${seasonStart.toISOString().slice(0, 10)}..${seasonEnd.toISOString().slice(0, 10)}...`);
  const scoreboardTeams = await loadScoreboardTeams('college-football', [80, 81], seasonStart, seasonEnd);
  console.log(`  ${scoreboardTeams.size} FBS/FCS scoreboard codes`);
  // Scoreboard teams win any code collision; the rest only fill codes nobody on the scoreboard uses.
  const cfbAll = new Map([...cfbTeams, ...scoreboardTeams]);

  await Promise.all([
    downloadSport('nfl', Object.keys(NFL_TEAMS), nflTeams, NFL_ESPN_ABBR_ALIASES),
    downloadSport('cfb', [...cfbAll.keys()], cfbAll),
  ]);
}

if (require.main === module) {
  main().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}

module.exports = { loadEspnTeams, loadScoreboardTeams };
