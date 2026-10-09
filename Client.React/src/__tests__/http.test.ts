import { http } from '../api/http';

// On a weak phone signal a request can stall on a dead connection for a minute or more. Without
// a client-side timeout the app sits on a blank loading state the whole time (the iOS "white
// screen" report); with one, callers get an error they can turn into a retry prompt.
describe('http client', () => {
  it('times out requests instead of waiting on a stalled connection forever', () => {
    expect(http.defaults.timeout).toBeGreaterThan(0);
    expect(http.defaults.timeout).toBeLessThanOrEqual(20_000);
  });
});
