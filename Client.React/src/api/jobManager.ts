import { http } from './http';
import type { JobStatusResponse } from '../types/admin';

export async function getAllJobsStatus() {
  const { data } = await http.get<JobStatusResponse[]>('/api/jobmanager/get-jobs');
  return data;
}

export async function runSpreads() {
  await http.post('/api/jobmanager/run-spreads');
}

export async function runUserManager() {
  await http.post('/api/jobmanager/run-users');
}

export async function runScores() {
  await http.post('/api/jobmanager/run-scores');
}

export async function runMissing() {
  await http.post('/api/jobmanager/run-missing');
}

export async function deleteJob(jobName: string) {
  const { data } = await http.delete<boolean>(`/api/jobmanager/delete-job/${encodeURIComponent(jobName)}`);
  return data;
}
