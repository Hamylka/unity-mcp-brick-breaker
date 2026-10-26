// Shared SVG helpers for the Neon Breaker logo pages (pixel font, palette, neon bricks, ball, glow).

// ---------- 5x7 pixel font, copied from Assets/Scripts/UI/PixelFont.cs ----------
const FONT_SRC = `
A .###. #...# #...# ##### #...# #...# #...#
B ####. #...# #...# ####. #...# #...# ####.
E ##### #.... #.... ####. #.... #.... #####
K #...# #..#. #.#.. ##... #.#.. #..#. #...#
N #...# ##..# #.#.# #..## #...# #...# #...#
O .###. #...# #...# #...# #...# #...# .###.
R ####. #...# #...# ####. #.#.. #..#. #...#`;
const FONT = {};
FONT_SRC.trim().split('\n').forEach(l => { const p = l.trim().split(' '); FONT[p[0]] = p.slice(1); });

/** Filled cells of a word: {c, r, li (letter index), lc (column inside the letter)}. */
function cells(text, gap = 1) {
  const out = []; let x = 0;
  [...text].forEach((ch, li) => {
    FONT[ch].forEach((row, r) => [...row].forEach((v, c) => { if (v === '#') out.push({ c: x + c, r, li, lc: c }); }));
    x += 5 + gap;
  });
  return { cells: out, cols: x - gap };
}

// ---------- palette (matches the game) ----------
const HP = ['#ffffff', '#7af5ff', '#2fb4ff', '#3a62ff', '#9a3cff']; // 1 HP .. 5 HP
const MAGENTA = '#ff3fd8', CYAN = '#22e6ff', GRID = '#8a3cff';
const hex = h => [1, 3, 5].map(i => parseInt(h.substr(i, 2), 16));
function lerpPal(pal, t) {
  t = Math.max(0, Math.min(1, t));
  const k0 = t * (pal.length - 1), i = Math.min(pal.length - 2, Math.floor(k0)), k = k0 - i;
  const a = hex(pal[i]), b = hex(pal[i + 1]);
  return '#' + a.map((v, j) => Math.round(v + (b[j] - v) * k).toString(16).padStart(2, '0')).join('');
}
function rng(seed) { return () => { seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const f = n => +n.toFixed(1);
let uid = 0;

// ---------- shared SVG pieces ----------
function defs(p) {
  return `<defs>
  <filter id="${p}g1" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="3"/></filter>
  <filter id="${p}g2" x="-80%" y="-80%" width="260%" height="260%"><feGaussianBlur stdDeviation="9"/></filter>
  <filter id="${p}g3" x="-150%" y="-150%" width="400%" height="400%"><feGaussianBlur stdDeviation="22"/></filter>
  <radialGradient id="${p}bg" cx="50%" cy="42%" r="75%"><stop offset="0" stop-color="#1d0e40"/><stop offset=".5" stop-color="#0b0719"/><stop offset="1" stop-color="#030208"/></radialGradient>
  <radialGradient id="${p}ball" cx="42%" cy="40%" r="62%"><stop offset="0" stop-color="#ffffff"/><stop offset=".3" stop-color="#fff0cc"/><stop offset=".62" stop-color="#ff9a2e"/><stop offset="1" stop-color="#ff3d7a"/></radialGradient>
  <linearGradient id="${p}fade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".05"/><stop offset="1" stop-color="#fff"/></linearGradient>
  </defs>`;
}
/** Bloom stack: wide haze, mid glow, tight glow, crisp shape. */
function bloom(p, inner, k = 1, haze = true) {
  return `${haze ? `<g filter="url(#${p}g3)" opacity="${.5 * k}">${inner}</g>` : ''}<g filter="url(#${p}g2)" opacity="${.75 * k}">${inner}</g><g filter="url(#${p}g1)" opacity="${k}">${inner}</g>${inner}`;
}
const svg = (w, h, body, size) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}"${size ? ` width="${size}" height="${size}"` : ''}>${body}</svg>`;

/** Flat synthwave floor with a horizon line. */
function grid(p, w, h, hy, vx, col = GRID) {
  let s = '';
  for (let i = -24; i <= 24; i++) s += `<line x1="${f(vx + i * w * .016)}" y1="${hy}" x2="${f(vx + i * w * .15)}" y2="${h}"/>`;
  for (let k = 1; k <= 14; k++) { const y = hy + (h - hy) * Math.pow(k / 14, 2.3); s += `<line x1="0" y1="${f(y)}" x2="${w}" y2="${f(y)}"/>`; }
  const id = p + 'fm' + (uid++);
  return `<mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="${hy}" width="${w}" height="${h - hy}"><rect x="0" y="${hy}" width="${w}" height="${h - hy}" fill="url(#${p}fade)"/></mask>
  <g mask="url(#${id})">${bloom(p, `<g stroke="${col}" stroke-width="2" fill="none">${s}</g>`, .7, false)}</g>
  ${bloom(p, `<line x1="0" y1="${hy}" x2="${w}" y2="${hy}" stroke="${MAGENTA}" stroke-width="2.5"/>`)}`;
}

/** Rounded neon brick: dark glassy body, glowing outline, small glint. */
function brick(x, y, w, h, col) {
  const sw = Math.max(1.5, h * .14), r = h * .3;
  return `<rect x="${f(x + sw / 2)}" y="${f(y + sw / 2)}" width="${f(w - sw)}" height="${f(h - sw)}" rx="${f(r)}" fill="${col}" fill-opacity=".13" stroke="${col}" stroke-width="${f(sw)}"/>`
       + `<line x1="${f(x + r + sw)}" y1="${f(y + sw * 1.9)}" x2="${f(x + w * .42)}" y2="${f(y + sw * 1.9)}" stroke="#fff" stroke-opacity=".55" stroke-width="${f(sw * .55)}" stroke-linecap="round"/>`;
}
/** A word made of bricks, colored by row like brick HP (back rows violet, front rows white). */
function brickWord(p, list, x0, y0, bw, bh, skip = new Set(), gapX = 1.5, gapY = 1.5) {
  let s = '';
  list.forEach(q => { if (!skip.has(q.c + ',' + q.r)) s += brick(x0 + q.c * bw + gapX, y0 + q.r * bh + gapY, bw - 2 * gapX, bh - 2 * gapY, lerpPal(HP, 1 - q.r / 6)); });
  return bloom(p, s, .9);
}
/** Solid glowing pixels with a hot core. */
function pixels(p, cs, x0, y0, list, col, core) {
  let a = '', b = '';
  list.forEach(({ c, r }) => {
    const x = x0 + c * cs, y = y0 + r * cs, g = cs * .09;
    a += `<rect x="${f(x + g)}" y="${f(y + g)}" width="${f(cs - 2 * g)}" height="${f(cs - 2 * g)}" rx="${f(cs * .14)}"/>`;
    b += `<rect x="${f(x + cs * .28)}" y="${f(y + cs * .28)}" width="${f(cs * .44)}" height="${f(cs * .44)}" rx="${f(cs * .08)}"/>`;
  });
  return bloom(p, `<g fill="${col}">${a}</g>`) + `<g fill="${core}" opacity=".85">${b}</g>`;
}
/** Neon tube path through the centers of a word's pixels (diagonals round the corners). */
function tubePath(cs, x0, y0, list) {
  const set = new Set(list.map(q => q.c + ',' + q.r)), has = (c, r) => set.has(c + ',' + r);
  const P = (c, r) => `${f(x0 + c * cs + cs / 2)},${f(y0 + r * cs + cs / 2)}`;
  let d = '';
  list.forEach(({ c, r }) => {
    if (has(c + 1, r)) d += `M${P(c, r)}L${P(c + 1, r)}`;
    if (has(c, r + 1)) d += `M${P(c, r)}L${P(c, r + 1)}`;
    if (has(c + 1, r + 1) && !has(c + 1, r) && !has(c, r + 1)) d += `M${P(c, r)}L${P(c + 1, r + 1)}`;
    if (has(c - 1, r + 1) && !has(c - 1, r) && !has(c, r + 1)) d += `M${P(c, r)}L${P(c - 1, r + 1)}`;
  });
  return d;
}
function tube(p, d, w, col) {
  const st = `fill="none" stroke-linecap="round" stroke-linejoin="round"`;
  return bloom(p, `<path d="${d}" ${st} stroke="${col}" stroke-width="${f(w)}"/>`) + `<path d="${d}" ${st} stroke="#fff" stroke-opacity=".9" stroke-width="${f(w * .3)}"/>`;
}
/** The game ball: white-hot core, orange shell, halo, two-layer trail toward (tx, ty) of half-width tw. */
function ball(p, x, y, r, tx, ty, tw = r * 1.05) {
  let s = '';
  if (tx !== undefined) {
    const id = p + 'tr' + (uid++), dx = x - tx, dy = y - ty, L = Math.hypot(dx, dy), nx = -dy / L, ny = dx / L;
    const poly = w => `${f(x + nx * w)},${f(y + ny * w)} ${f(tx)},${f(ty)} ${f(x - nx * w)},${f(y - ny * w)}`;
    const g = `gradientUnits="userSpaceOnUse" x1="${f(x)}" y1="${f(y)}" x2="${f(tx)}" y2="${f(ty)}"`;
    s += `<defs><linearGradient id="${id}" ${g}><stop offset="0" stop-color="#ff9a2e"/><stop offset=".45" stop-color="#ff3fb8" stop-opacity=".75"/><stop offset="1" stop-color="${MAGENTA}" stop-opacity="0"/></linearGradient>
      <linearGradient id="${id}c" ${g}><stop offset="0" stop-color="#fff"/><stop offset=".55" stop-color="#ffd9a0" stop-opacity="0"/></linearGradient></defs>
      <polygon points="${poly(tw * 1.1)}" fill="url(#${id})" filter="url(#${p}g2)" opacity=".8"/>
      <polygon points="${poly(tw)}" fill="url(#${id})"/>
      <polygon points="${poly(tw * .33)}" fill="url(#${id}c)"/>`;
  }
  return s + `<circle cx="${f(x)}" cy="${f(y)}" r="${f(Math.min(r * 3.4, r + 40))}" fill="#ff7a1f" opacity=".45" filter="url(#${p}g3)"/>
    <circle cx="${f(x)}" cy="${f(y)}" r="${f(r * 1.6)}" fill="#ffae4a" opacity=".75" filter="url(#${p}g2)"/>
    <circle cx="${f(x)}" cy="${f(y)}" r="${f(r)}" fill="url(#${p}ball)"/>
    <circle cx="${f(x - r * .25)}" cy="${f(y - r * .28)}" r="${f(r * .32)}" fill="#fff" opacity=".9"/>`;
}
/** Brick shards flying away from an impact. */
function shards(p, x, y, n, colors, seed, size, ang = -Math.PI / 2, spread = 1.4, dist = 100) {
  const R = rng(seed); let s = '';
  for (let i = 0; i < n; i++) {
    const a = ang + (R() - .5) * spread * 2, d = dist * (.25 + R() * .9), sz = size * (.4 + R() * .9);
    const cx = x + Math.cos(a) * d, cy = y + Math.sin(a) * d;
    s += `<rect x="${f(cx - sz / 2)}" y="${f(cy - sz / 4)}" width="${f(sz)}" height="${f(sz / 2)}" fill="${colors[i % colors.length]}" opacity="${f(.55 + R() * .45)}" transform="rotate(${f(R() * 180)} ${f(cx)} ${f(cy)})"/>`;
  }
  return bloom(p, s, .8);
}
const ring = (p, x, y, r, col, w = 3) => bloom(p, `<circle cx="${f(x)}" cy="${f(y)}" r="${f(r)}" fill="none" stroke="${col}" stroke-width="${w}" opacity=".7"/>`, .8);
function paddle(p, x, y, w, h) {
  return `<ellipse cx="${f(x + w / 2)}" cy="${f(y + h * 1.4)}" rx="${f(w * .8)}" ry="${f(h * 1.4)}" fill="${CYAN}" opacity=".35" filter="url(#${p}g3)"/>`
       + bloom(p, `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${f(h / 2)}" fill="${CYAN}" fill-opacity=".18" stroke="${CYAN}" stroke-width="${f(h * .22)}"/>`)
       + `<line x1="${f(x + h * .7)}" y1="${f(y + h * .32)}" x2="${f(x + w * .5)}" y2="${f(y + h * .32)}" stroke="#fff" stroke-opacity=".7" stroke-width="${f(h * .14)}" stroke-linecap="round"/>`;
}
/** Rounded-square app icon frame (content is clipped to the rounded corners). */
function icon(p, body, size) {
  return svg(512, 512, `${defs(p)}<clipPath id="${p}clip"><rect width="512" height="512" rx="112"/></clipPath>
    <g clip-path="url(#${p}clip)"><rect width="512" height="512" fill="url(#${p}bg)"/>${body}</g>
    <rect x="1" y="1" width="510" height="510" rx="111" fill="none" stroke="#ffffff" stroke-opacity=".08" stroke-width="2"/>`, size);
}
/** Magenta neon tube frame inside an icon. */
const tubeFrame = (p, inset = 46, r = 74, w = 11) => {
  const a = inset, b = 512 - inset;
  return tube(p, `M${a + r},${a}H${b - r}A${r},${r} 0 0 1 ${b},${a + r}V${b - r}A${r},${r} 0 0 1 ${b - r},${b}H${a + r}A${r},${r} 0 0 1 ${a},${b - r}V${a + r}A${r},${r} 0 0 1 ${a + r},${a}Z`, w, MAGENTA);
};
