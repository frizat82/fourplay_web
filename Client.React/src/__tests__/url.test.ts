import { buildAbsoluteUrl } from '../utils/url';

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
