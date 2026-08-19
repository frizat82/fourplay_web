import axios from 'axios';

interface ErrorsBody {
  errors?: unknown;
}

export function extractApiErrorMessage(error: unknown, fallback: string): string {
  if (!axios.isAxiosError(error)) return fallback;

  if (error.response?.status === 429) {
    return 'Too many attempts. Please wait a few minutes and try again.';
  }

  const data = error.response?.data;

  if (typeof data === 'string' && data.trim().length > 0) {
    return data;
  }

  const errors = (data as ErrorsBody | undefined)?.errors;
  if (Array.isArray(errors) && errors.length > 0 && errors.every((e) => typeof e === 'string')) {
    return errors.join('\n');
  }

  return fallback;
}
