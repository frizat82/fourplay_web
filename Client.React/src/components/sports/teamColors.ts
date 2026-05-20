export interface TeamColors {
  primary: string;
  secondary: string;
  text: string; // text color on helmet (white or dark)
}

const DEFAULT: TeamColors = { primary: '#6b7280', secondary: '#9ca3af', text: '#ffffff' };

const TEAMS: Record<string, TeamColors> = {
  // ── NFL ─────────────────────────────────────────────────────────────────
  ARI:  { primary: '#97233f', secondary: '#ffb612', text: '#ffffff' },
  ATL:  { primary: '#a71930', secondary: '#000000', text: '#ffffff' },
  BAL:  { primary: '#241773', secondary: '#9e7c0c', text: '#ffffff' },
  BUF:  { primary: '#00338d', secondary: '#c60c30', text: '#ffffff' },
  CAR:  { primary: '#0085ca', secondary: '#101820', text: '#ffffff' },
  CHI:  { primary: '#0b162a', secondary: '#c83803', text: '#ffffff' },
  CIN:  { primary: '#fb4f14', secondary: '#000000', text: '#ffffff' },
  CLE:  { primary: '#ff3c00', secondary: '#311d00', text: '#ffffff' },
  DAL:  { primary: '#041e42', secondary: '#869397', text: '#ffffff' },
  DEN:  { primary: '#fb4f14', secondary: '#002244', text: '#ffffff' },
  DET:  { primary: '#0076b6', secondary: '#b0b7bc', text: '#ffffff' },
  GB:   { primary: '#203731', secondary: '#ffb612', text: '#ffffff' },
  HOU:  { primary: '#03202f', secondary: '#a71930', text: '#ffffff' },
  IND:  { primary: '#002c5f', secondary: '#a5acaf', text: '#ffffff' },
  JAC:  { primary: '#006778', secondary: '#9f792c', text: '#ffffff' },
  KC:   { primary: '#e31837', secondary: '#ffb81c', text: '#ffffff' },
  LAC:  { primary: '#0080c6', secondary: '#ffc20e', text: '#ffffff' },
  LAR:  { primary: '#003594', secondary: '#ffd100', text: '#ffffff' },
  LV:   { primary: '#000000', secondary: '#a5acaf', text: '#ffffff' },
  MIA:  { primary: '#008e97', secondary: '#fc4c02', text: '#ffffff' },
  MIN:  { primary: '#4f2683', secondary: '#ffc62f', text: '#ffffff' },
  NE:   { primary: '#002244', secondary: '#c60c30', text: '#ffffff' },
  NO:   { primary: '#101820', secondary: '#d3bc8d', text: '#d3bc8d' },
  NYG:  { primary: '#0b2265', secondary: '#a71930', text: '#ffffff' },
  NYJ:  { primary: '#125740', secondary: '#000000', text: '#ffffff' },
  PHI:  { primary: '#004c54', secondary: '#a5acaf', text: '#ffffff' },
  PIT:  { primary: '#101820', secondary: '#ffb612', text: '#ffb612' },
  SEA:  { primary: '#002244', secondary: '#69be28', text: '#ffffff' },
  SF:   { primary: '#aa0000', secondary: '#b3995d', text: '#ffffff' },
  TB:   { primary: '#d50a0a', secondary: '#34302b', text: '#ffffff' },
  TEN:  { primary: '#0c2340', secondary: '#4b92db', text: '#ffffff' },
  WAS:  { primary: '#5a1414', secondary: '#ffb612', text: '#ffffff' },

  // ── CFB — Power programs ─────────────────────────────────────────────────
  ALA:  { primary: '#9e1b32', secondary: '#828a8f', text: '#ffffff' }, // Alabama
  ARIZ: { primary: '#ab0520', secondary: '#0c234b', text: '#ffffff' }, // Arizona
  ARK:  { primary: '#9d2235', secondary: '#ffffff', text: '#ffffff' }, // Arkansas
  ASU:  { primary: '#8c1d40', secondary: '#ffc627', text: '#ffffff' }, // Arizona State
  AUB:  { primary: '#0c2340', secondary: '#e87722', text: '#ffffff' }, // Auburn
  BAMA: { primary: '#9e1b32', secondary: '#828a8f', text: '#ffffff' }, // Alabama alt
  BCU:  { primary: '#8b0000', secondary: '#f0c000', text: '#ffffff' }, // Bethune-Cookman
  BYU:  { primary: '#002e5d', secondary: '#ffffff', text: '#ffffff' }, // BYU
  CAL:  { primary: '#003262', secondary: '#fdb515', text: '#ffffff' }, // California
  CLEM: { primary: '#f56600', secondary: '#522d80', text: '#ffffff' }, // Clemson
  COLO: { primary: '#cfb87c', secondary: '#000000', text: '#000000' }, // Colorado
  DUKE: { primary: '#003087', secondary: '#000000', text: '#ffffff' }, // Duke
  FSU:  { primary: '#782f40', secondary: '#ceb888', text: '#ffffff' }, // Florida State
  FLA:  { primary: '#0021a5', secondary: '#fa4616', text: '#ffffff' }, // Florida
  GT:   { primary: '#b3a369', secondary: '#003057', text: '#000000' }, // Georgia Tech
  IU:   { primary: '#990000', secondary: '#dfccb2', text: '#ffffff' }, // Indiana
  JMU:  { primary: '#450084', secondary: '#cbb677', text: '#ffffff' }, // James Madison
  KSU:  { primary: '#512888', secondary: '#d1a827', text: '#ffffff' }, // Kansas State
  LSU:  { primary: '#461d7c', secondary: '#fdd023', text: '#fdd023' }, // LSU
  MIAF: { primary: '#f47321', secondary: '#005030', text: '#ffffff' }, // Miami FL (CFB)
  MICH: { primary: '#00274c', secondary: '#ffcb05', text: '#ffcb05' }, // Michigan
  MISS: { primary: '#ce1126', secondary: '#14213d', text: '#ffffff' }, // Ole Miss
  MSU:  { primary: '#18453b', secondary: '#ffffff', text: '#ffffff' }, // Michigan State / Miss State
  MSST: { primary: '#660000', secondary: '#ffffff', text: '#ffffff' }, // Mississippi State
  ND:   { primary: '#0c2340', secondary: '#c99700', text: '#ffffff' }, // Notre Dame
  // NE: same as NFL New England (using NEB for Nebraska instead)
  NEB:  { primary: '#e41c38', secondary: '#f5f1e7', text: '#ffffff' }, // Nebraska
  NCST: { primary: '#cc0000', secondary: '#6f7073', text: '#ffffff' }, // NC State
  OHIO: { primary: '#bb0000', secondary: '#636669', text: '#ffffff' }, // Ohio
  ORE:  { primary: '#154733', secondary: '#fee123', text: '#ffffff' }, // Oregon
  OSU:  { primary: '#bb0000', secondary: '#666666', text: '#ffffff' }, // Ohio State
  OU:   { primary: '#841617', secondary: '#f5c518', text: '#ffffff' }, // Oklahoma
  PEN:  { primary: '#011f5b', secondary: '#990000', text: '#ffffff' }, // Penn State
  PSU:  { primary: '#041e42', secondary: '#ffffff', text: '#ffffff' }, // Penn State alt
  SMU:  { primary: '#0033a0', secondary: '#c8102e', text: '#ffffff' }, // SMU
  SC:   { primary: '#73000a', secondary: '#000000', text: '#ffffff' }, // South Carolina
  STAN: { primary: '#8c1515', secondary: '#b6b1a9', text: '#ffffff' }, // Stanford
  TAMU: { primary: '#500000', secondary: '#ffffff', text: '#ffffff' }, // Texas A&M
  TENN: { primary: '#f77f00', secondary: '#ffffff', text: '#ffffff' }, // Tennessee (CFB)
  TEX:  { primary: '#bf5700', secondary: '#ffffff', text: '#ffffff' }, // Texas
  TTU:  { primary: '#cc0000', secondary: '#000000', text: '#ffffff' }, // Texas Tech
  TULN: { primary: '#006747', secondary: '#418fde', text: '#ffffff' }, // Tulane
  UCLA: { primary: '#2d68c4', secondary: '#f2a900', text: '#ffffff' }, // UCLA
  UGA:  { primary: '#ba0c2f', secondary: '#000000', text: '#ffffff' }, // Georgia
  UNC:  { primary: '#4b9cd3', secondary: '#13294b', text: '#ffffff' }, // UNC
  UNLV: { primary: '#b10202', secondary: '#8d734a', text: '#ffffff' }, // UNLV
  USC:  { primary: '#990000', secondary: '#ffc72c', text: '#ffffff' }, // USC
  UTAH: { primary: '#cc0001', secondary: '#808080', text: '#ffffff' }, // Utah
  UVA:  { primary: '#232d4b', secondary: '#e57200', text: '#ffffff' }, // Virginia
  VT:   { primary: '#861f41', secondary: '#cf4420', text: '#ffffff' }, // Virginia Tech
  WAKE: { primary: '#9e7e38', secondary: '#000000', text: '#ffffff' }, // Wake Forest
  WASH: { primary: '#33006f', secondary: '#e8d3a2', text: '#ffffff' }, // Washington
  WSU:  { primary: '#981e32', secondary: '#5e6a71', text: '#ffffff' }, // Washington State
  WIS:  { primary: '#c5050c', secondary: '#f7f7f7', text: '#ffffff' }, // Wisconsin
};

export function getTeamColors(abbr: string): TeamColors {
  return TEAMS[abbr.toUpperCase()] ?? DEFAULT;
}
