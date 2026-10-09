import indexHtml from '../../index.html?raw';

// index.html is all a phone has to show while the rest of the app downloads on a weak signal.
// Before this, a stalled request at launch left iOS's installed app on a plain white screen with
// no way forward but a force-quit.
describe('index.html boot resilience', () => {
  it('loads no render-blocking stylesheet from another host (Google Fonts stalled launch on a weak signal)', () => {
    expect(indexHtml).not.toMatch(/fonts\.googleapis\.com|fonts\.gstatic\.com/);
    expect(indexHtml).not.toMatch(/<link[^>]+rel="stylesheet"[^>]+href="https?:/);
  });

  // The inline boot colors are checked against global.css's --bg-2 in e2e/bootResilience.spec.ts
  // (Vitest stubs CSS imports, even ?raw, so it can't read global.css here).
  it('paints a background inline, before any stylesheet downloads', () => {
    expect(indexHtml).toMatch(/<head>[\s\S]*?<meta charset="UTF-8" \/>\s*(<!--[\s\S]*?-->\s*)?<style>/);
  });

  it('shows a loading splash inside #root until the app mounts over it', () => {
    expect(indexHtml).toMatch(/<div id="root">\s*<div[^>]*id="boot-splash"/);
  });
});
