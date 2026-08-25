import { decodeBase64Url, isValidBase64Url } from '../utils/base64';

function encodeBase64Url(input: string): string {
  return btoa(input).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

describe('decodeBase64Url', () => {
  it('round-trips a value encoded with the URL-safe alphabet and no padding', () => {
    const original = 'user-id-001:invite-code-abc123';
    expect(decodeBase64Url(encodeBase64Url(original))).toBe(original);
  });

  it('handles inputs that need padding restored', () => {
    // 'a' -> base64 'YQ==' -> url-safe 'YQ' (stripped padding, length 2, needs 2 chars of padding back)
    expect(decodeBase64Url('YQ')).toBe('a');
  });
});

describe('isValidBase64Url', () => {
  it('returns true for validly-encoded input', () => {
    expect(isValidBase64Url(encodeBase64Url('valid-token'))).toBe(true);
  });

  it('rejects garbage input that is not valid base64', () => {
    expect(isValidBase64Url('!!!not-base64!!!')).toBe(false);
  });
});
