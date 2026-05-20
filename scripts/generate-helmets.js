#!/usr/bin/env node
/**
 * Generates one SVG team badge per team into Client.React/public/Icons/Helmets/
 * Shield shape with primary color fill, secondary color accent band, team abbreviation text.
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
  NYJ:  { primary: '#125740', secondary: '#ffffff' },
  PHI:  { primary: '#004c54', secondary: '#a5acaf' },
  PIT:  { primary: '#101820', secondary: '#ffb612' },
  SEA:  { primary: '#002244', secondary: '#69be28' },
  SF:   { primary: '#aa0000', secondary: '#b3995d' },
  TB:   { primary: '#d50a0a', secondary: '#34302b' },
  TEN:  { primary: '#0c2340', secondary: '#4b92db' },
  WAS:  { primary: '#5a1414', secondary: '#ffb612' },
  // CFB — Power programs + regular Top 25 appearances
  ALA:  { primary: '#9e1b32', secondary: '#828a8f' },
  ARK:  { primary: '#9d2235', secondary: '#ffffff' },
  ASU:  { primary: '#8c1d40', secondary: '#ffc627' },
  AUB:  { primary: '#0c2340', secondary: '#e87722' },
  BYU:  { primary: '#002e5d', secondary: '#ffffff' },
  CAL:  { primary: '#003262', secondary: '#fdb515' },
  CLEM: { primary: '#f56600', secondary: '#522d80' },
  COLO: { primary: '#cfb87c', secondary: '#000000' },
  DUKE: { primary: '#003087', secondary: '#ffffff' },
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
  SC:   { primary: '#73000a', secondary: '#ffffff' },
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
  // CFB — additional Top 25 programs
  APP:  { primary: '#000000', secondary: '#ffb612' },
  ARMY: { primary: '#000000', secondary: '#d4af37' },
  BOIS: { primary: '#0033a0', secondary: '#d64309' },
  CIN:  { primary: '#e00122', secondary: '#000000' },
  GASO: { primary: '#011e41', secondary: '#c1a144' },
  HAW:  { primary: '#024731', secondary: '#c8a96e' },
  IOWA: { primary: '#000000', secondary: '#ffcd00' },
  KU:   { primary: '#0051a5', secondary: '#e8000d' },
  LIB:  { primary: '#c8102e', secondary: '#002868' },
  MINN: { primary: '#7a0019', secondary: '#ffb71b' },
  MIZ:  { primary: '#000000', secondary: '#f1b82d' },
  MSU:  { primary: '#18453b', secondary: '#ffffff' },
  NAVY: { primary: '#00205b', secondary: '#c5b783' },
  NIU:  { primary: '#cc0000', secondary: '#000000' },
  OHIO: { primary: '#00694e', secondary: '#ffffff' },
  OKST: { primary: '#ff6600', secondary: '#000000' },
  PITT: { primary: '#003594', secondary: '#ffb81c' },
  TCU:  { primary: '#4d1979', secondary: '#ffffff' },
  UNLV: { primary: '#b10202', secondary: '#8d734a' },
  UVA:  { primary: '#232d4b', secondary: '#e57200' },
  WAKE: { primary: '#9e7e38', secondary: '#000000' },
  WVU:  { primary: '#002855', secondary: '#eaaa00' },
  WSU:  { primary: '#981e32', secondary: '#5e6a71' },
  // Bowl-game regulars
  ARST: { primary: '#cc0000', secondary: '#000000' },
  CCU:  { primary: '#007041', secondary: '#a39161' },
  FRES: { primary: '#cc0033', secondary: '#ffffff' },
  UTSA: { primary: '#f15a22', secondary: '#002147' },
  WMU:  { primary: '#6c4023', secondary: '#ffc62f' },
};

// ── Pick text color that contrasts against the primary background ─────────────
function textColor(hex) {
  const r = parseInt(hex.slice(1,3), 16);
  const g = parseInt(hex.slice(3,5), 16);
  const b = parseInt(hex.slice(5,7), 16);
  // Perceived luminance
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return lum > 0.45 ? '#111111' : '#ffffff';
}

// ── Badge SVG — clean shield, no text (abbr shown below in TeamHelmet component)
function makeBadge(abbr, primary, secondary) {
  const secR = parseInt(secondary.slice(1,3)||'ff', 16);
  const secG = parseInt(secondary.slice(3,5)||'ff', 16);
  const secB = parseInt(secondary.slice(5,7)||'ff', 16);
  const secLum = (0.299 * secR + 0.587 * secG + 0.114 * secB) / 255;
  const priR = parseInt(primary.slice(1,3), 16);
  const priG = parseInt(primary.slice(3,5), 16);
  const priB = parseInt(primary.slice(5,7), 16);
  const priLum = (0.299 * priR + 0.587 * priG + 0.114 * priB) / 255;
  const band = Math.abs(secLum - priLum) < 0.15
    ? (priLum > 0.5 ? '#222222' : '#dddddd')
    : secondary;

  return `<svg viewBox="0 0 100 106" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <clipPath id="sc">
      <path d="M 50,4 L 6,18 L 6,56 C 6,78 26,98 50,104 C 74,98 94,78 94,56 L 94,18 Z"/>
    </clipPath>
  </defs>

  <!-- Shield fill -->
  <path d="M 50,4 L 6,18 L 6,56 C 6,78 26,98 50,104 C 74,98 94,78 94,56 L 94,18 Z"
    fill="${primary}"/>

  <!-- Secondary accent band -->
  <rect x="6" y="42" width="88" height="20" fill="${band}" clip-path="url(#sc)"/>

  <!-- Gloss -->
  <ellipse cx="50" cy="28" rx="26" ry="12" fill="rgba(255,255,255,0.15)"/>

  <!-- Outline -->
  <path d="M 50,4 L 6,18 L 6,56 C 6,78 26,98 50,104 C 74,98 94,78 94,56 L 94,18 Z"
    fill="none" stroke="rgba(0,0,0,0.3)" stroke-width="3"/>
</svg>`;
}

// ── Generate files ──────────────────────────────────────────────────────────
let count = 0;
for (const [abbr, { primary, secondary }] of Object.entries(TEAMS)) {
  const svg = makeBadge(abbr, primary, secondary);
  const file = path.join(OUT_DIR, `${abbr.toLowerCase()}.svg`);
  fs.writeFileSync(file, svg, 'utf8');
  count++;
}
console.log(`✓ Generated ${count} team badge SVGs → ${OUT_DIR}`);
