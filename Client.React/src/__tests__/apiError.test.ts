import { extractApiErrorMessage } from '../utils/apiError';
import { buildAxiosError } from './testUtils/axiosError';

describe('extractApiErrorMessage', () => {
  it('returns a friendly rate-limit message for a 429, even with an empty body', () => {
    // The real backend's rate limiter (Program.cs AddRateLimiter) returns a bare 429 with no
    // JSON body — there must be no path where this renders "undefined" or a blank toast.
    expect(extractApiErrorMessage(buildAxiosError(429, ''), 'fallback')).toBe(
      'Too many attempts. Please wait a few minutes and try again.',
    );
  });

  it('surfaces the server-provided errors array for a 400', () => {
    expect(
      extractApiErrorMessage(buildAxiosError(400, { isSuccess: false, errors: ['Invalid or expired invitation code.'] }), 'fallback'),
    ).toBe('Invalid or expired invitation code.');
  });

  it('joins multiple server errors with a newline', () => {
    expect(
      extractApiErrorMessage(buildAxiosError(400, { isSuccess: false, errors: ['Error one', 'Error two'] }), 'fallback'),
    ).toBe('Error one\nError two');
  });

  it('falls back for a 400 with no errors array', () => {
    expect(extractApiErrorMessage(buildAxiosError(400, {}), 'fallback')).toBe('fallback');
  });

  it('falls back for a non-axios error', () => {
    expect(extractApiErrorMessage(new Error('network down'), 'fallback')).toBe('fallback');
  });

  it('surfaces a bare-string response body', () => {
    // AuthController.CreateUser's post-creation MarkInvitationAsUsedAsync failure path
    // (AuthController.cs) returns BadRequest("Invalid invitation code or already used/expired.")
    // — a plain string, not a { errors: [...] } body. That specific, actionable message must
    // not be silently dropped in favor of the generic fallback.
    expect(
      extractApiErrorMessage(buildAxiosError(400, 'Invalid invitation code or already used/expired.'), 'fallback'),
    ).toBe('Invalid invitation code or already used/expired.');
  });

  it('falls back for an empty-string response body', () => {
    expect(extractApiErrorMessage(buildAxiosError(400, ''), 'fallback')).toBe('fallback');
  });
});
