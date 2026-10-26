// Logos render at most 56px (TeamLogo's default; cards use 50px, the picks matrix 22–32px), so
// 168px covers a 3x iPhone screen. They were 500x500 (~60 KB each, ~110 MB of CFB crests), which
// made every slate's ~28 logo requests slow and failure-prone on cellular.
const MAX_LOGO_PX = 168;

// Each logo as a base64 data URL (Vite's ?inline), so the PNG header can be read without Node APIs.
const logos = import.meta.glob<string>('../../public/Icons/Logos/**/*.png', { query: '?inline', import: 'default' });

// A PNG's width and height are big-endian uint32s at bytes 16–23 (IHDR), inside the first 32
// base64 characters of its data.
function pngSize(dataUrl: string): [number, number] {
  const head = atob(dataUrl.slice(dataUrl.indexOf(',') + 1, dataUrl.indexOf(',') + 33));
  const u32 = (at: number) => ((head.charCodeAt(at) << 24) | (head.charCodeAt(at + 1) << 16) | (head.charCodeAt(at + 2) << 8) | head.charCodeAt(at + 3)) >>> 0;
  return [u32(16), u32(20)];
}

describe('team logo files', () => {
  it(`are at most ${MAX_LOGO_PX}px on each side`, async () => {
    const entries = Object.entries(logos);
    expect(entries.length).toBeGreaterThan(100);

    const sizes = await Promise.all(entries.map(async ([path, load]) => [path, pngSize(await load())] as const));
    const oversized = sizes
      .filter(([, [w, h]]) => w > MAX_LOGO_PX || h > MAX_LOGO_PX)
      .map(([path, [w, h]]) => `${path.split('/Logos/')[1]} ${w}x${h}`);

    expect(oversized.slice(0, 10)).toEqual([]);
  }, 30_000);
});
