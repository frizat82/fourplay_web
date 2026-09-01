import { buildAbsoluteUrl, buildLoginUrl } from '../utils/url';

describe('buildAbsoluteUrl', () => {
  it('joins a leading-slash path onto window.location.origin', () => {
    expect(buildAbsoluteUrl('/account/confirmemail')).toBe(`${window.location.origin}/account/confirmemail`);
  });

  it('joins a path without a leading slash the same way', () => {
    expect(buildAbsoluteUrl('account/confirmemail')).toBe(`${window.location.origin}/account/confirmemail`);
  });

  it('returns the origin with a trailing slash for an empty path', () => {
    expect(buildAbsoluteUrl('')).toBe(`${window.location.origin}/`);
  });
});

describe('buildLoginUrl', () => {
  it('builds a login path with the return path URL-encoded as returnUrl', () => {
    expect(buildLoginUrl('/join/tok123')).toBe('/account/login?returnUrl=%2Fjoin%2Ftok123');
  });

  it('encodes query params in the return path too', () => {
    expect(buildLoginUrl('/dashboard?tab=1')).toBe('/account/login?returnUrl=%2Fdashboard%3Ftab%3D1');
  });
});
