#!/usr/bin/env node
/**
 * Generates one SVG helmet file per team into Client.React/public/Icons/Helmets/
 * Run: node scripts/generate-helmets.js
 */

const fs = require('fs');
const path = require('path');

const OUT_DIR = path.join(__dirname, '..', 'Client.React', 'public', 'Icons', 'Helmets');
fs.mkdirSync(OUT_DIR, { recursive: true });

// ── Team colors ──────────────────────────────────────────────────────────────
const TEAMS = {
  // NFL
  ARI:  { primary: '#97233f', secondary: '#ffb612' },
  ATL:  { primary: '#a71930', secondary: '#000000' },
  BAL:  { primary: '#241773', secondary: '#9e7c0c' },
  BUF:  { primary: '#00338d', secondary: '#c60c30' },
  CAR:  { primary: '#0085ca', secondary: '#101820' },
  CHI:  { primary: '#0b162a', secondary: '#c83803' },
  CIN:  { primary: '#fb4f14', secondary: '#000000' },
  CLE:  { primary: '#ff3c00', secondary: '#311d00' },
  DAL:  { primary: '#041e42', secondary: '#869397' },
  DEN:  { primary: '#fb4f14', secondary: '#002244' },
  DET:  { primary: '#0076b6', secondary: '#b0b7bc' },
  GB:   { primary: '#203731', secondary: '#ffb612' },
  HOU:  { primary: '#03202f', secondary: '#a71930' },
  IND:  { primary: '#002c5f', secondary: '#a5acaf' },
  JAC:  { primary: '#006778', secondary: '#9f792c' },
  KC:   { primary: '#e31837', secondary: '#ffb81c' },
  LAC:  { primary: '#0080c6', secondary: '#ffc20e' },
  LAR:  { primary: '#003594', secondary: '#ffd100' },
  LV:   { primary: '#000000', secondary: '#a5acaf' },
  MIA:  { primary: '#008e97', secondary: '#fc4c02' },
  MIN:  { primary: '#4f2683', secondary: '#ffc62f' },
  NE:   { primary: '#002244', secondary: '#c60c30' },
  NO:   { primary: '#101820', secondary: '#d3bc8d' },
  NYG:  { primary: '#0b2265', secondary: '#a71930' },
  NYJ:  { primary: '#125740', secondary: '#000000' },
  PHI:  { primary: '#004c54', secondary: '#a5acaf' },
  PIT:  { primary: '#101820', secondary: '#ffb612' },
  SEA:  { primary: '#002244', secondary: '#69be28' },
  SF:   { primary: '#aa0000', secondary: '#b3995d' },
  TB:   { primary: '#d50a0a', secondary: '#34302b' },
  TEN:  { primary: '#0c2340', secondary: '#4b92db' },
  WAS:  { primary: '#5a1414', secondary: '#ffb612' },
  // CFB
  ALA:  { primary: '#9e1b32', secondary: '#828a8f' },
  ARK:  { primary: '#9d2235', secondary: '#ffffff' },
  ASU:  { primary: '#8c1d40', secondary: '#ffc627' },
  AUB:  { primary: '#0c2340', secondary: '#e87722' },
  BYU:  { primary: '#002e5d', secondary: '#ffffff' },
  CAL:  { primary: '#003262', secondary: '#fdb515' },
  CLEM: { primary: '#f56600', secondary: '#522d80' },
  COLO: { primary: '#cfb87c', secondary: '#000000' },
  DUKE: { primary: '#003087', secondary: '#000000' },
  FLA:  { primary: '#0021a5', secondary: '#fa4616' },
  FSU:  { primary: '#782f40', secondary: '#ceb888' },
  GT:   { primary: '#b3a369', secondary: '#003057' },
  IU:   { primary: '#990000', secondary: '#dfccb2' },
  JMU:  { primary: '#450084', secondary: '#cbb677' },
  KSU:  { primary: '#512888', secondary: '#d1a827' },
  LSU:  { primary: '#461d7c', secondary: '#fdd023' },
  MICH: { primary: '#00274c', secondary: '#ffcb05' },
  MIA:  { primary: '#f47321', secondary: '#005030' },
  MISS: { primary: '#ce1126', secondary: '#14213d' },
  MSST: { primary: '#660000', secondary: '#ffffff' },
  ND:   { primary: '#0c2340', secondary: '#c99700' },
  NEB:  { primary: '#e41c38', secondary: '#f5f1e7' },
  NCST: { primary: '#cc0000', secondary: '#6f7073' },
  ORE:  { primary: '#154733', secondary: '#fee123' },
  OSU:  { primary: '#bb0000', secondary: '#666666' },
  OU:   { primary: '#841617', secondary: '#f5c518' },
  PSU:  { primary: '#041e42', secondary: '#ffffff' },
  SC:   { primary: '#73000a', secondary: '#000000' },
  SMU:  { primary: '#0033a0', secondary: '#c8102e' },
  STAN: { primary: '#8c1515', secondary: '#b6b1a9' },
  TAMU: { primary: '#500000', secondary: '#ffffff' },
  TEN:  { primary: '#f77f00', secondary: '#ffffff' },
  TEX:  { primary: '#bf5700', secondary: '#ffffff' },
  TTU:  { primary: '#cc0000', secondary: '#000000' },
  TULN: { primary: '#006747', secondary: '#418fde' },
  UCLA: { primary: '#2d68c4', secondary: '#f2a900' },
  UGA:  { primary: '#ba0c2f', secondary: '#000000' },
  UNC:  { primary: '#4b9cd3', secondary: '#13294b' },
  USC:  { primary: '#990000', secondary: '#ffc72c' },
  UTAH: { primary: '#cc0001', secondary: '#808080' },
  VT:   { primary: '#861f41', secondary: '#cf4420' },
  WASH: { primary: '#33006f', secondary: '#e8d3a2' },
  WIS:  { primary: '#c5050c', secondary: '#f7f7f7' },
};

// ── SVG template — designed at 200×164, scales perfectly when used as <img> ──
function makeHelmet(primary, secondary) {
  const cage = '#8fa3b8';
  return `<svg viewBox="0 0 200 164" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <!-- Clip dome to left 60% so face opening is transparent on the right -->
    <clipPath id="c">
      <rect x="0" y="0" width="124" height="164"/>
    </clipPath>
    <filter id="sh" x="-15%" y="-10%" width="135%" height="130%">
      <feDropShadow dx="0" dy="3" stdDeviation="3" flood-opacity="0.25"/>
    </filter>
  </defs>

  <!-- Main dome: large circle, face (right) side is clipped open -->
  <circle cx="70" cy="80" r="66" fill="${primary}" clip-path="url(#c)" filter="url(#sh)"/>

  <!-- Top stripe -->
  <path d="M 14,22 Q 68,6 122,22"
        fill="none" stroke="${secondary}" stroke-width="13"
        stroke-linecap="round" clip-path="url(#c)" opacity="0.9"/>

  <!-- Shine highlight -->
  <ellipse cx="42" cy="34" rx="24" ry="9"
           fill="rgba(255,255,255,0.22)" transform="rotate(-22 42 34)"
           clip-path="url(#c)"/>

  <!-- Earhole -->
  <circle cx="20" cy="100" r="11" fill="rgba(0,0,0,0.28)"/>
  <circle cx="20" cy="100" r="7"  fill="${secondary}" opacity="0.75"/>

  <!-- Chin strap -->
  <path d="M 24,136 Q 68,152 112,136"
        fill="none" stroke="${secondary}" stroke-width="8"
        stroke-linecap="round" clip-path="url(#c)" opacity="0.8"/>

  <!-- Face-opening edge shadow -->
  <path d="M 122,16 C 148,32 154,56 154,80 C 154,106 146,128 122,148"
        fill="none" stroke="rgba(0,0,0,0.18)" stroke-width="10"/>

  <!-- ═══ Facemask cage ═══ -->
  <!-- Attachment clips (rectangular brackets where cage meets helmet) -->
  <rect x="116" y="24"  width="18" height="13" rx="4" fill="${cage}"/>
  <rect x="116" y="126" width="18" height="13" rx="4" fill="${cage}"/>

  <!-- Vertical bar along face edge -->
  <line x1="133" y1="28"  x2="133" y2="137"
        stroke="${cage}" stroke-width="6" stroke-linecap="round"/>

  <!-- Top diagonal bar (clip → cage top-right) -->
  <line x1="133" y1="30"  x2="172" y2="46"
        stroke="${cage}" stroke-width="8" stroke-linecap="round"/>

  <!-- Horizontal bar 1 -->
  <line x1="133" y1="66"  x2="178" y2="66"
        stroke="${cage}" stroke-width="7" stroke-linecap="round"/>

  <!-- Horizontal bar 2 -->
  <line x1="133" y1="94"  x2="178" y2="94"
        stroke="${cage}" stroke-width="7" stroke-linecap="round"/>

  <!-- Bottom diagonal bar (cage bottom-right → clip) -->
  <line x1="133" y1="135" x2="172" y2="118"
        stroke="${cage}" stroke-width="8" stroke-linecap="round"/>

  <!-- Right vertical connector (closes the cage) -->
  <line x1="178" y1="46"  x2="178" y2="118"
        stroke="${cage}" stroke-width="8" stroke-linecap="round"/>
</svg>`;
}

// ── Generate files ──────────────────────────────────────────────────────────
let count = 0;
for (const [abbr, { primary, secondary }] of Object.entries(TEAMS)) {
  const svg = makeHelmet(primary, secondary);
  const file = path.join(OUT_DIR, `${abbr.toLowerCase()}.svg`);
  fs.writeFileSync(file, svg, 'utf8');
  count++;
}
console.log(`✓ Generated ${count} helmet SVGs → ${OUT_DIR}`);
