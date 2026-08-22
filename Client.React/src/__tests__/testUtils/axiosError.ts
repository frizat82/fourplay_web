import { AxiosError, AxiosHeaders } from 'axios';

export function buildAxiosError(status: number, data?: unknown): AxiosError {
  return new AxiosError('Request failed', String(status), undefined, undefined, {
    status,
    statusText: '',
    headers: new AxiosHeaders(),
    config: { headers: new AxiosHeaders() },
    data,
  });
}
