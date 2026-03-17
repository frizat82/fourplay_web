import { http } from './http';
import type { EmailRequest, InvitationDto } from '../types/admin';

export async function getAllInvitations() {
  const { data } = await http.get<InvitationDto[]>('/api/invitations/all');
  return data;
}

export async function getInvitationsByUser(userId: string) {
  const { data } = await http.get<InvitationDto[]>(`/api/invitations/user/${encodeURIComponent(userId)}`);
  return data;
}

export async function createInvitation(email: string, invitedByUserId: string) {
  const { data } = await http.post<InvitationDto>('/api/invitations', undefined, {
    params: { email, invitedByUserId },
  });
  return data;
}

export async function deleteInvitation(id: number) {
  await http.delete(`/api/invitations/${id}`);
}

export async function sendEmail(request: EmailRequest) {
  await http.post('/api/invitations/send', request);
}
