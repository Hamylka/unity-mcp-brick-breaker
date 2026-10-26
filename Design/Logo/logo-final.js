// Final Neon Breaker logo (needs logo-lib.js).
// Wordmark: pixel NEON, BREAKER in neon HP bricks; the ball leaves the paddle, smashes the A and becomes the O of NEON.
// Icon: magenta tube frame around a three-row wall, the ball coming in at the same angle as in the wordmark.

/** Wordmark. background = false drops the backdrop and floor and crops to the artwork (for the title screen). */
function logoWordmark(p, background = true) {
  let s = defs(p);
  if (background) s += `<rect width="1280" height="640" fill="url(#${p}bg)"/>` + grid(p, 1280, 640, 480, 640);
  const ne = cells('NEON', 2), cs = 15, nx = (1280 - ne.cols * cs) / 2, ny = 62;
  s += pixels(p, cs, nx, ny, ne.cells.filter(q => q.li !== 2), '#ff4fe0', '#ffe3fb');
  const ox = nx + 16.5 * cs, oy = ny + 3.5 * cs, or = cs * 2.05;

  const br = cells('BREAKER', 1), bw = 27, bh = 19, bx = (1280 - br.cols * bw) / 2, by = 192, a = 18;
  const hx = bx + (a + 2.5) * bw, hy = by + 3.5 * bh;
  // the line O -> bar of the A, extended down to the paddle; it also clips the top right brick of the A
  const k = (hx - ox) / (hy - oy), py = 548, px = hx + k * (py - hy);
  const ty = by + bh * .5, tx = hx + k * (ty - hy);
  s += brickWord(p, br.cells, bx, by, bw, bh, new Set([`${a + 1},3`, `${a + 2},3`, `${a + 3},3`, `${a + 3},0`]), 1.2, 1.6);
  s += paddle(p, f(px - 75), py, 150, 16);
  s += `<circle cx="${f(hx)}" cy="${f(hy)}" r="20" fill="#ffffff" opacity=".35" filter="url(#${p}g2)"/>`;
  s += ring(p, hx, hy, 30, '#ffffff', 2);
  s += shards(p, hx, hy, 12, [HP[1], HP[2], '#ffffff'], 7, 12, -Math.PI / 2 + .3, 1.3, 85);
  s += shards(p, tx, ty, 9, [HP[4], HP[3], '#ffffff'], 21, 11, -Math.PI / 3, 1.0, 70);
  s += ball(p, ox, oy, or, px, py, 15);
  return background ? svg(1280, 640, s) : `<svg xmlns="http://www.w3.org/2000/svg" viewBox="40 0 1200 600">${s}</svg>`;
}

/** The three-row wall, smashed brick, paddle and ball (shared by the full icon and the adaptive foreground). */
function iconPlayfield(p) {
  const bw = 96, bh = 40, gx = 12, x0 = (512 - 3 * bw - 2 * gx) / 2; let b = '', s = '';
  [[98, HP[4]], [148, HP[3]], [198, HP[1]]].forEach(([y, col], row) => {
    for (let i = 0; i < 3; i++) if (!(row === 2 && i === 2)) b += brick(x0 + i * (bw + gx), y, bw, bh, col);
  });
  s += bloom(p, b, .9);
  const hx = x0 + 2 * (bw + gx) + bw / 2, hy = 218;
  s += ring(p, hx, hy, 40, '#ffffff', 4);
  s += shards(p, hx, hy, 12, [HP[1], '#ffffff', HP[2]], 8, 24, -Math.PI / 2 + .2, 1.4, 100);
  s += paddle(p, 104, 420, 140, 22);
  s += ball(p, 334, 280, 32, 174, 420, 28);
  return s;
}
/** Full app icon (rounded square, neon frame). Used for the default icon, legacy Android icons and the store. */
function logoIcon(p, size) { return icon(p, grid(p, 512, 512, 392, 256) + tubeFrame(p) + iconPlayfield(p), size); }

/**
 * Android adaptive icon layers (108 dp canvas, the launcher shows the middle 72 dp through its own mask shape).
 * The frame is left out because the launcher mask draws the outline; the playfield is scaled into the safe zone.
 */
function logoAdaptiveBackground(p) {
  return svg(512, 512, defs(p) + `<rect width="512" height="512" fill="url(#${p}bg)"/>` + grid(p, 512, 512, 372, 256)
    + `<radialGradient id="${p}edge" r="70%"><stop offset=".6" stop-color="${MAGENTA}" stop-opacity="0"/><stop offset="1" stop-color="${MAGENTA}" stop-opacity=".35"/></radialGradient>
       <rect width="512" height="512" fill="url(#${p}edge)"/>`);
}
function logoAdaptiveForeground(p) {
  // playfield content spans roughly x 94..418, y 70..445; center it and fit it into the 66 dp safe circle
  return svg(512, 512, defs(p) + `<g transform="translate(256 256) scale(.68) translate(-256 -262)">${iconPlayfield(p)}</g>`);
}
