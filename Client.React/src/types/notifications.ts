export interface NotificationPreferencesDto {
  notifyMineBloodyDuringGame: boolean;
  notifyMineBloodyAtFinal: boolean;
  notifyMineCoveringDuringGame: boolean;
  notifyMineCoveringAtFinal: boolean;
  notifyOthersBloodyDuringGame: boolean;
  notifyOthersBloodyAtFinal: boolean;
  notifyOthersCoveringDuringGame: boolean;
  notifyOthersCoveringAtFinal: boolean;
  notifyWeekResult: boolean;
}

export interface PushSubscriptionRequestDto {
  endpoint: string;
  p256dh: string;
  auth: string;
  userAgent?: string | null;
  /** LeagueType of the app this was made from: 0 = NFL, 1 = CFB. Routes each sport's pushes to its own app. */
  sport?: 0 | 1 | null;
}

export interface UnsubscribeRequestDto {
  endpoint: string;
}

export interface VapidPublicKeyDto {
  publicKey: string;
}

export interface MessageResponseDto {
  message: string;
}
