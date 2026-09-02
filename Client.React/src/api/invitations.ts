import { http } from './http';
import type { InvitationDto } from '../types/admin';
import type { LeagueInviteResultDto } from './league';

export async function getAllInvitations() {
  const { data } = await http.get<InvitationDto[]>('/api/invitations/all');
  return data;
}

export async function getInvitationsByUser(userId: string) {
  const { data } = await http.get<InvitationDto[]>(`/api/invitations/user/${encodeURIComponent(userId)}`);
  return data;
}

// When leagueId targets an already-registered user, the backend routes them through the same
// existing-user pending-invite flow as League Portal's "Invite Player" (see
// InvitationController.Create) instead of sending a registration email — outcome tells the
// caller which one happened.
export async function createInvitation(email: string, invitedByUserId: string, leagueId?: number | null) {
  const { data } = await http.post<LeagueInviteResultDto>('/api/invitations', undefined, {
    params: {
      email,
      invitedByUserId,
      baseUrl: window.location.origin,
      ...(leagueId != null ? { leagueId } : {}),
    },
  });
  return data;
}

export async function validateInvitation(code: string) {
  const { data } = await http.get<InvitationDto | null>(`/api/invitations/validate/${encodeURIComponent(code)}`);
  return data;
}

export async function deleteInvitation(id: number) {
  await http.delete(`/api/invitations/${id}`);
}

export async function resendInvitation(id: number) {
  await http.post(`/api/invitations/${id}/resend`, undefined, {
    params: { baseUrl: window.location.origin },
  });
}
