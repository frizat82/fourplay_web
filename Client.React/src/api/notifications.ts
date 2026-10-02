import { http } from './http';
import type {
  MessageResponseDto,
  NotificationPreferencesDto,
  PushSubscriptionRequestDto,
  UnsubscribeRequestDto,
  VapidPublicKeyDto,
} from '../types/notifications';

export async function getNotificationPreferences() {
  const { data } = await http.get<NotificationPreferencesDto>('/api/notifications/preferences');
  return data;
}

export async function putNotificationPreferences(payload: NotificationPreferencesDto) {
  const { data } = await http.put<NotificationPreferencesDto>('/api/notifications/preferences', payload);
  return data;
}

export async function getVapidPublicKey() {
  const { data } = await http.get<VapidPublicKeyDto>('/api/notifications/vapid-public-key');
  return data;
}

export async function subscribeToPush(payload: PushSubscriptionRequestDto) {
  await http.post('/api/notifications/subscribe', payload);
}

export async function unsubscribeFromPush(payload: UnsubscribeRequestDto) {
  await http.delete('/api/notifications/subscribe', { data: payload });
}

export async function sendTestPush() {
  const { data } = await http.post<MessageResponseDto>('/api/notifications/test-push');
  return data;
}
