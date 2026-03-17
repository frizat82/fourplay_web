export type PickType = 'Over' | 'Under' | 'Spread';

export interface NflPickDto {
  id: number;
  leagueId: number;
  userId: string;
  userName: string;
  team: string;
  pick: PickType;
  nflWeek: number;
  season: number;
  dateCreated: string;
}

export interface SpreadResponse {
  team: string;
  spread?: number | null;
  over?: number | null;
  under?: number | null;
}

export interface SpreadCalculationResponse extends SpreadResponse {
  isWinner: boolean;
  isOverWinner: boolean;
  isUnderWinner: boolean;
}

export interface BatchSpreadRequest {
  requests: SpreadRequest[];
}

export interface SpreadRequest {
  team: string;
}

export interface BatchSpreadResponse {
  responses: Record<string, SpreadResponse>;
}

export interface SpreadCalculationRequest extends SpreadRequest {
  pickTeamScore: number;
  otherTeamScore: number;
}

export interface BatchSpreadCalculationRequest {
  calculations: SpreadCalculationRequest[];
}

export interface BatchSpreadCalculationResponse {
  results: Record<string, SpreadCalculationResponse>;
}
