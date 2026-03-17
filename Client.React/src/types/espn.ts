export type EspnRecordType = 'total' | 'road' | 'home' | 0 | 1 | 2;
export type HomeAway = 'away' | 'home' | 0 | 1;
export type TypeName =
  | 'status_final'
  | 'status_halftime'
  | 'status_in_progress'
  | 'status_scheduled'
  | 'status_end_period'
  | 0
  | 1
  | 2
  | 3
  | 4;

export interface EspnScores {
  leagues?: EspnLeague[];
  season?: Season;
  week?: Week;
  events?: Event[];
}

export interface Event {
  id: string;
  season: Season;
  week: Week;
  date: string;
  competitions: Competition[];
  weather?: EspnWeather | null;
}

export interface EspnWeather {
  displayValue: string;
  temperature: number;
  highTemperature: number;
  conditionId: string;
}

export interface Competition {
  id: string;
  date: string;
  competitors: Competitor[];
  status: EspnStatus;
  odds?: Odd[];
  situation?: EspnSituation | null;
}

export interface EspnSituation {
  down: number;
  yardLine: number;
  distance: number;
  downDistanceText: string;
  shortDownDistanceText: string;
  possessionText: string;
  isRedZone?: boolean | null;
  homeTimeouts: number;
  awayTimeouts: number;
  possession?: string | null;
}

export interface Competitor {
  id: string;
  homeAway: HomeAway;
  team: EspnTeam;
  score: number | string;
  records?: EspnRecord[];
}

export interface EspnRecord {
  name: string;
  type: EspnRecordType;
  summary: string;
}

export interface EspnTeam {
  abbreviation: string;
  logo?: string;
}

export interface Odd {
  provider: OddsProvider;
  details: string;
  overUnder: number;
}

export interface OddsProvider {
  id: number | string;
  name: string;
  priority: number;
}

export interface EspnStatus {
  clock: number;
  displayClock: string;
  period: number;
  type: StatusType;
}

export interface StatusType {
  id: number | string;
  name: TypeName | string | number;
  state: string;
  completed: boolean;
  description: string;
  detail: string;
  shortDetail: string;
}

export interface EspnLeague {
  id: number | string;
  uid: string;
  name: string;
  abbreviation: string;
  slug: string;
  season: LeagueSeason;
}

export interface LeagueSeason {
  year: number;
  startDate: string;
  endDate: string;
  displayName: number;
  type: SeasonType;
}

export interface SeasonType {
  id: number;
  type: number;
  name: string;
  abbreviation: string;
}

export interface Season {
  type: number;
  year: number;
}

export interface Week {
  number: number;
  teamsOnBye?: EspnTeam[];
}
