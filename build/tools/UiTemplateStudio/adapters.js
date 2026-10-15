/* UiTemplateStudio · 家族适配器
 * 只操作预览 iframe 的 window/document，不依赖工作台状态。
 * 家族：hy（--bg-window / --text / --accent 一套令牌，含 3ds Max）与 ue5（--bg-title / --fg / --primary 一套令牌）。
 */
(function (g) {
  'use strict';
  const A = {};

  /* ───────── 模块类别 ───────── */
  A.CATS = [
    { id: 'titlebar', label: '标题栏', sel: '.titlebar' },
    { id: 'menubar', label: '菜单栏', sel: '#menubar,.menubar,.macbar' },
    { id: 'ribbon', label: '功能区', sel: '#ribbon,.ribbon' },
    { id: 'toolbar', label: '工具栏', sel: '.toolbar,.tshelf,.tool-rail,.mode-rail' },
    { id: 'dockL', label: '左侧停靠区', sel: '#dockL' },
    { id: 'dockR', label: '右侧停靠区', sel: '#dockR,#dockR2,[data-dock="right2"]' },
    { id: 'dockB', label: '底部停靠区', sel: '#dockB' },
    { id: 'panel', label: '面板', sel: '.pane[data-title]' },
    { id: 'viewport', label: '视口', sel: '#vparea,#vp,.viewport' },
    { id: 'npanel', label: 'N 面板', sel: '#npanel' },
    { id: 'cmdline', label: '命令行', sel: '#cl' },
    { id: 'statusbar', label: '状态栏', sel: '.statusbar,.status' },
  ];
  A.catLabel = id => (A.CATS.find(c => c.id === id) || { label: id }).label;
  const EXCLUDE = '#info-drawer,.review,.hy-review-bar,.uits-host,.pos-seg';

  /* ───────── 颜色工具 ───────── */
  const clamp = (x, a, b) => (x < a ? a : x > b ? b : x);
  function parseColor(str) {
    str = String(str || '').trim();
    let m = /^#([0-9a-f]{3,8})$/i.exec(str);
    if (m) {
      let h = m[1];
      if (h.length === 3 || h.length === 4) h = [...h].map(c => c + c).join('');
      if (h.length === 6) h += 'ff';
      if (h.length !== 8) return null;
      return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: parseInt(h.slice(6, 8), 16) / 255 };
    }
    m = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:\s*[,/]\s*([\d.]+)(%?))?\s*\)$/i.exec(str);
    if (m) {
      let a = m[4] === undefined ? 1 : parseFloat(m[4]);
      if (m[5]) a /= 100;
      return { r: +m[1], g: +m[2], b: +m[3], a };
    }
    return null;
  }
  const hex2 = n => ('0' + Math.round(clamp(n, 0, 255)).toString(16)).slice(-2);
  function fmt(c) {
    return c.a >= 0.999 ? '#' + hex2(c.r) + hex2(c.g) + hex2(c.b) : `rgba(${Math.round(clamp(c.r, 0, 255))},${Math.round(clamp(c.g, 0, 255))},${Math.round(clamp(c.b, 0, 255))},${+clamp(c.a, 0, 1).toFixed(3)})`;
  }
  const lum = c => (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255;
  function mixc(a, b, t) {
    return { r: clamp(a.r + (b.r - a.r) * t, 0, 255), g: clamp(a.g + (b.g - a.g) * t, 0, 255), b: clamp(a.b + (b.b - a.b) * t, 0, 255), a: 1 };
  }
  function rgb2hsl(c) {
    const r = c.r / 255, g = c.g / 255, b = c.b / 255, mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2;
    let h = 0, s = 0;
    if (mx !== mn) {
      const d = mx - mn;
      s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
      h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
      h *= 60;
    }
    return [h, s, l];
  }
  function hsl2rgb(h, s, l, a) {
    h = ((h % 360) + 360) % 360 / 360;
    const f = (p, q, t) => { if (t < 0) t += 1; if (t > 1) t -= 1; return t < 1 / 6 ? p + (q - p) * 6 * t : t < 1 / 2 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p; };
    let r, g, b;
    if (s === 0) r = g = b = l;
    else { const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q; r = f(p, q, h + 1 / 3); g = f(p, q, h); b = f(p, q, h - 1 / 3); }
    return { r: r * 255, g: g * 255, b: b * 255, a: a === undefined ? 1 : a };
  }
  const hueDist = (a, b) => { const d = Math.abs(a - b) % 360; return d > 180 ? 360 - d : d; };
  A.color = { parse: parseColor, fmt, lum, mix: mixc };

  /* ───────── 整体颜色：种子色 → 角色 → 各家族令牌 ───────── */
  A.DEFAULT_SEEDS = {
    dark: { mode: 'dark', bg: '#1B1B1B', panel: '#222222', text: '#E6E6E6', accent: '#0A84FF', canvas: '#212830' },
    light: { mode: 'light', bg: '#F5F5F7', panel: '#FFFFFF', text: '#1D1D1F', accent: '#0071E3', canvas: '#EEF1F5' },
  };
  A.PRESETS = [
    { id: 'hy-dark', name: 'hy 深色', seeds: A.DEFAULT_SEEDS.dark },
    { id: 'hy-light', name: 'hy 浅色', seeds: A.DEFAULT_SEEDS.light },
    { id: 'blender', name: 'Blender 深', seeds: { mode: 'dark', bg: '#181818', panel: '#303030', text: '#E6E6E6', accent: '#4772B3', canvas: '#3D3D3D' } },
    { id: 'vscode', name: 'VS Code 深', seeds: { mode: 'dark', bg: '#1F1F1F', panel: '#181818', text: '#CCCCCC', accent: '#0078D4', canvas: '#1E1E1E' } },
    { id: 'obsidian', name: '紫调深色', seeds: { mode: 'dark', bg: '#1E1B26', panel: '#262230', text: '#DAD7E4', accent: '#8A5CF5', canvas: '#211E2B' } },
    { id: 'paper', name: '纸白', seeds: { mode: 'light', bg: '#FAFAF7', panel: '#FFFFFF', text: '#2B2B2B', accent: '#2B6CB0', canvas: '#F1F0EA' } },
  ];

  A.derive = function (s) {
    const dark = s.mode === 'dark';
    const bg = parseColor(s.bg), panel = parseColor(s.panel), text = parseColor(s.text), acc = parseColor(s.accent), cv = parseColor(s.canvas);
    const black = { r: 0, g: 0, b: 0, a: 1 }, white = { r: 255, g: 255, b: 255, a: 1 };
    const al = (c, a) => ({ r: c.r, g: c.g, b: c.b, a });
    const P = {};
    P.window = bg; P.panel = panel; P.raised = mixc(panel, text, 0.07); P.header = bg; P.ctl = mixc(panel, text, 0.12);
    P.input = dark ? mixc(bg, black, 0.3) : mixc(bg, white, 0.65);
    P.canvas = cv; P.text = text; P.text2 = mixc(bg, text, 0.68); P.text3 = mixc(bg, text, 0.45);
    P.border = mixc(bg, text, 0.12); P.borderStrong = mixc(bg, text, 0.22); P.sashHover = mixc(bg, text, 0.3);
    P.borderDark = dark ? mixc(bg, black, 0.5) : mixc(bg, black, 0.25);
    P.hover = al(text, 0.06); P.active = al(text, 0.11);
    P.hoverSolid = mixc(panel, text, 0.18); P.hoverSolid2 = mixc(panel, text, 0.35);
    P.grid = al(text, 0.06); P.sbThumb = al(text, 0.3); P.sbThumbHover = al(text, 0.5);
    Object.assign(P, A.deriveAccent(acc, dark));
    P.selInactive = mixc(panel, acc, 0.35); P.selParent = mixc(panel, acc, 0.12);
    P.danger = parseColor(dark ? '#FF453A' : '#D70015');
    P.warning = parseColor(dark ? '#FFB340' : '#C77700');
    P.success = parseColor(dark ? '#30D158' : '#1E8E3E');
    const css = {};
    Object.keys(P).forEach(k => { css[k] = fmt(P[k]); });
    return { c: P, css, dark, bg, text, acc, accHSL: rgb2hsl(acc) };
  };
  A.deriveAccent = function (acc, dark) {
    const black = { r: 0, g: 0, b: 0, a: 1 }, white = { r: 255, g: 255, b: 255, a: 1 };
    return {
      accent: acc,
      accentHover: dark ? mixc(acc, white, 0.18) : mixc(acc, black, 0.12),
      accentPress: dark ? mixc(acc, black, 0.2) : mixc(acc, black, 0.25),
      accentSoft: { r: acc.r, g: acc.g, b: acc.b, a: 0.22 },
      onAccent: lum(acc) > 0.6 ? black : white,
    };
  };

  const TOKENS = {
    hy: {
      '--bg-window': 'window', '--bg-panel': 'panel', '--bg-raised': 'raised', '--bg-input': 'input', '--bg-canvas': 'canvas', '--bg-header': 'header',
      '--bg-ctl': 'ctl', '--bg-hover': 'hover', '--bg-active': 'active', '--border': 'border', '--border-strong': 'borderStrong', '--sash': 'border',
      '--sash-hover': 'sashHover', '--text': 'text', '--text-2': 'text2', '--text-3': 'text3', '--accent': 'accent', '--accent-hover': 'accentHover',
      '--accent-press': 'accentPress', '--accent-soft': 'accentSoft', '--text-on-accent': 'onAccent', '--danger': 'danger', '--warning': 'warning',
      '--success': 'success', '--cad-bg': 'canvas', '--vp-bg': 'canvas', '--cad-grid': 'grid', '--vp-grid': 'grid', '--cad-line': 'text', '--cad-text': 'text2',
      '--sb-thumb': 'sbThumb', '--sb-thumb-hover': 'sbThumbHover',
    },
    ue5: {
      '--bg-title': 'window', '--bg-input': 'input', '--bg-recessed': 'canvas', '--bg-panel': 'panel', '--bg-header': 'raised', '--bg-dropdown': 'ctl',
      '--bg-hover': 'hoverSolid', '--bg-hover2': 'hoverSolid2', '--fg': 'text', '--fg-hover': 'text', '--fg-header': 'text', '--primary': 'accent',
      '--primary-hover': 'accentHover', '--primary-press': 'accentPress', '--select-inactive': 'selInactive', '--select-parent': 'selParent',
      '--warning': 'warning', '--error': 'danger', '--success': 'success', '--border': 'borderDark', '--border-soft': 'border', '--border-mid': 'borderStrong',
    },
  };
  A.TOKENS = TOKENS;
  const SURFACE_TOKENS = { hy: ['--bg-window', '--bg-panel', '--bg-header', '--bg-raised'], ue5: ['--bg-title', '--bg-panel', '--bg-header'] };
  const ACCENT_TOKENS = {
    hy: { '--accent': 'accent', '--accent-hover': 'accentHover', '--accent-press': 'accentPress', '--accent-soft': 'accentSoft', '--text-on-accent': 'onAccent' },
    ue5: { '--primary': 'accent', '--primary-hover': 'accentHover', '--primary-press': 'accentPress' },
  };
  const ALIAS = {
    'hy>ue5': { '--bg-window': 'var(--bg-title)', '--bg-raised': 'var(--bg-header)', '--bg-canvas': 'var(--bg-recessed)', '--bg-ctl': 'var(--bg-dropdown)', '--text': 'var(--fg)', '--text-2': 'var(--fg-header)', '--text-3': 'var(--fg-header)', '--accent': 'var(--primary)', '--accent-hover': 'var(--primary-hover)', '--accent-press': 'var(--primary-press)', '--accent-soft': 'var(--select-inactive)', '--border-strong': 'var(--border-mid)', '--danger': 'var(--error)', '--text-on-accent': '#fff', '--vp-bg': 'var(--bg-recessed)', '--cad-bg': 'var(--bg-recessed)' },
    'ue5>hy': { '--bg-title': 'var(--bg-window)', '--bg-recessed': 'var(--bg-canvas)', '--bg-header': 'var(--bg-raised)', '--bg-dropdown': 'var(--bg-ctl)', '--bg-hover2': 'var(--bg-active)', '--fg': 'var(--text)', '--fg-hover': 'var(--text)', '--fg-header': 'var(--text-2)', '--primary': 'var(--accent)', '--primary-hover': 'var(--accent-hover)', '--primary-press': 'var(--accent-press)', '--select-inactive': 'var(--accent-soft)', '--select-parent': 'var(--bg-hover)', '--error': 'var(--danger)', '--border-soft': 'var(--border)', '--border-mid': 'var(--border-strong)' },
  };
  const SIZE_TOKEN_RE = /^--(h|fs|r|sp|pad|font|dur|sb|sash|ed|ic)(-|$)/;

  /* ───────── 文档初始化与快照 ───────── */
  const LIT_RE = /#[0-9a-fA-F]{8}\b|#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3,4}\b|rgba?\([^)]*\)/g;
  const SKIP_SPLIT = /(url\([^)]*\)|"[^"]*"|'[^']*')/;

  function mapText(text, fn) {
    return text.split(SKIP_SPLIT).map((seg, i) => (i % 2 ? seg : seg.replace(LIT_RE, m => fn(m)))).join('');
  }
  function splitDecls(text) {
    const out = []; let depth = 0, q = null, cur = '';
    for (const ch of text) {
      if (q) { cur += ch; if (ch === q) q = null; continue; }
      if (ch === '"' || ch === "'") { q = ch; cur += ch; continue; }
      if (ch === '(') depth++;
      if (ch === ')') depth--;
      if (ch === ';' && depth === 0) { if (cur.trim()) out.push(cur.trim()); cur = ''; continue; }
      cur += ch;
    }
    if (cur.trim()) out.push(cur.trim());
    return out;
  }
  function mapDeclText(text, fn) {
    return splitDecls(text).map(d => {
      const i = d.indexOf(':');
      if (i < 0) return d;
      return d.slice(0, i + 1) + mapText(d.slice(i + 1), fn);
    }).join('; ');
  }
  function hasLiteral(text) {
    return splitDecls(text).some(d => {
      const i = d.indexOf(':');
      if (i <= 0) return false;
      return d.slice(i + 1).split(SKIP_SPLIT).some((seg, k) => k % 2 === 0 && (seg.match(LIT_RE) || []).length > 0);
    });
  }
  function walkRules(list, cb) {
    for (const r of list) {
      if (r.style) cb(r);
      if (r.cssRules) walkRules(r.cssRules, cb);
    }
  }
  function sheetRules(sheet) { try { return sheet.cssRules; } catch (e) { return []; } }

  function computeMeta(win, fam) {
    const T = fam === 'ue5' ? { bg: '--bg-title', tx: '--fg', ac: '--primary' } : { bg: '--bg-window', tx: '--text', ac: '--accent' };
    const doc = win.document, cs = win.getComputedStyle(doc.documentElement);
    const g = n => parseColor(cs.getPropertyValue(n));
    const bg = g(T.bg) || { r: 27, g: 27, b: 27, a: 1 }, tx = g(T.tx) || { r: 230, g: 230, b: 230, a: 1 }, ac = g(T.ac) || { r: 10, g: 132, b: 255, a: 1 };
    const refs = { base: { lBg: lum(bg), lText: lum(tx) } };
    for (const sh of doc.styleSheets) {
      walkRules(sheetRules(sh), r => {
        const m = /data-theme=["']?(\w+)/.exec(r.selectorText || '');
        if (!m) return;
        const b = parseColor(r.style.getPropertyValue(T.bg)), t = parseColor(r.style.getPropertyValue(T.tx));
        if (b && t) refs[m[1]] = { lBg: lum(b), lText: lum(t) };
      });
    }
    const [h, s, l] = rgb2hsl(ac);
    return { family: fam, refs, accent: { h, s, l }, baseTheme: lum(bg) < lum(tx) ? 'dark' : 'light' };
  }

  A.init = function (win, fam) {
    const doc = win.document;
    if (doc.__uits) return doc.__uits;
    const root = doc.documentElement;
    const U = doc.__uits = { win, family: fam, rules: [], ruleOrig: new Map(), origText: new WeakMap(), props: [], densProps: [], defined: {}, sizeTokens: {}, origTheme: root.getAttribute('data-theme'), themeForced: false, attrOrig: new WeakMap(), stats: null, applied: {}, moved: null };
    const cs = win.getComputedStyle(root);
    Object.keys(TOKENS[fam]).forEach(n => { const v = cs.getPropertyValue(n).trim(); if (v) U.defined[n] = v; });
    U.meta = computeMeta(win, fam);
    for (const sh of doc.styleSheets) A.registerSheet(U, sh, null);
    const st = doc.createElement('style');
    st.id = 'uits-style';
    st.textContent = '.uits-vwrap{display:flex;flex-direction:row;min-width:0;min-height:0}' +
      '.uits-vtool{flex:none!important;flex-direction:column!important;width:auto!important;height:auto!important;min-width:40px;max-width:220px;overflow-x:hidden;overflow-y:auto;border-bottom:0!important;border-right:1px solid var(--border,#444);align-items:center!important;justify-content:flex-start!important;padding:4px 3px!important;gap:2px!important}' +
      '.uits-vtool>*{flex:none!important}' +
      '.uits-vtool .sep,.uits-vtool .tb-sep,.uits-vtool [class*="sep"]{width:70%!important;height:1px!important;margin:3px 0!important}' +
      '.uits-vtool .sp{display:none!important}' +
      '.uits-vtool input,.uits-vtool select{max-width:46px}' +
      '.uits-vmain{flex:1 1 auto!important;min-width:0!important;height:auto!important;align-self:stretch!important}' +
      '[data-uits-hl]{outline:2px solid #ff9f0a!important;outline-offset:-2px}';
    (doc.head || root).appendChild(st);
    return U;
  };

  A.registerSheet = function (U, sheet, meta) {
    walkRules(sheetRules(sheet), r => {
      const text = r.style.cssText;
      if (!text || !hasLiteral(text)) return;
      const m = /data-theme=["']?(\w+)/.exec(r.selectorText || '');
      U.rules.push({ rule: r, sheet, orig: text, ctx: m ? m[1] : null, meta });
      U.ruleOrig.set(r, text);
    });
  };

  /* ───────── 整体颜色应用（幂等） ───────── */
  function makeMapper(D) {
    return function (c, ref, acc) {
      const [h, s, l] = rgb2hsl(c);
      if (s < 0.2) {
        const u = (lum(c) - ref.lBg) / ((ref.lText - ref.lBg) || 1);
        const o = mixc(D.bg, D.text, clamp(u, -0.4, 1.25));
        o.a = c.a;
        return o;
      }
      if (acc && s >= 0.25 && hueDist(h, acc.h) <= 30) {
        const [H, S, L] = D.accHSL;
        return hsl2rgb(H, clamp(S * (s / (acc.s || 1)), 0, 1), clamp(L + (l - acc.l) * 0.9, 0.04, 0.96), c.a);
      }
      return null;
    };
  }

  A.applyColor = function (win, color) {
    const U = win.document.__uits;
    if (!U) return null;
    const root = win.document.documentElement;
    U.props.forEach(n => root.style.removeProperty(n));
    U.props = [];
    root.style.removeProperty('color-scheme');
    U.rules = U.rules.filter(e => e.sheet.ownerNode && e.sheet.ownerNode.isConnected);
    const enabled = color && color.enabled;
    if (!enabled) {
      U.rules.forEach(e => { if (e.rule.style.cssText !== e.orig) e.rule.style.cssText = e.orig; });
      restoreAttrs(U, win.document);
      if (U.themeForced) {
        if (U.origTheme === null) root.removeAttribute('data-theme'); else root.setAttribute('data-theme', U.origTheme);
        U.themeForced = false;
      }
      U.stats = null;
      win.dispatchEvent(new win.Event('hy-theme'));
      return null;
    }
    const D = A.derive(color.seeds);
    if (U.family === 'hy') { root.setAttribute('data-theme', color.seeds.mode); U.themeForced = true; }
    root.style.setProperty('color-scheme', color.seeds.mode);
    const map = TOKENS[U.family];
    Object.keys(map).forEach(n => {
      if (!(n in U.defined)) return;
      root.style.setProperty(n, D.css[map[n]], 'important');
      U.props.push(n);
    });
    const stats = { total: 0, neutral: 0, accent: 0, kept: 0 };
    const mapper = makeMapper(D);
    const conv = (lit, ref, acc) => {
      const c = parseColor(lit);
      stats.total++;
      if (!c) { stats.kept++; return lit; }
      const o = mapper(c, ref, acc);
      if (!o) { stats.kept++; return lit; }
      if (rgb2hsl(c)[1] < 0.2) stats.neutral++; else stats.accent++;
      return fmt(o);
    };
    U.rules.forEach(e => {
      const meta = e.meta || U.meta;
      const ref = meta.refs[e.ctx] || meta.refs.base;
      const next = mapDeclText(e.orig, lit => conv(lit, ref, meta.accent));
      if (e.rule.style.cssText !== next) e.rule.style.cssText = next;
    });
    mapAttrs(U, win.document, lit => conv(lit, U.meta.refs.base, U.meta.accent));
    U.stats = stats;
    win.dispatchEvent(new win.Event('hy-theme'));
    return stats;
  };

  const ATTR_SEL = '[style*="#"],[style*="rgb"],[fill^="#"],[stroke^="#"],[stop-color^="#"]';
  function mapAttrs(U, doc, fn) {
    doc.querySelectorAll(ATTR_SEL).forEach(el => {
      if (el === doc.documentElement || el.classList.contains('uits-host') || el.closest('#info-drawer,.review,.hy-review-bar') || (el.__uitsOv && el.__uitsOv.length)) return;
      let o = U.attrOrig.get(el);
      if (!o) {
        o = {};
        ['style', 'fill', 'stroke', 'stop-color'].forEach(a => { if (el.hasAttribute(a)) o[a] = el.getAttribute(a); });
        U.attrOrig.set(el, o);
        (U.attrEls = U.attrEls || new Set()).add(el);
      }
      Object.keys(o).forEach(a => {
        const v = a === 'style' ? mapDeclText(o[a], fn) : mapText(o[a], fn);
        if (el.getAttribute(a) !== v) el.setAttribute(a, v);
      });
    });
  }
  function restoreAttrs(U, doc) {
    (U.attrEls || []).forEach(el => {
      const o = U.attrOrig.get(el) || {};
      Object.keys(o).forEach(a => { if (el.getAttribute(a) !== o[a]) el.setAttribute(a, o[a]); });
    });
  }

  /* ───────── 模块颜色覆盖（只存角色 + 偏移） ───────── */
  A.OVERRIDE_SURFACES = [['window', '窗口底'], ['panel', '面板底'], ['raised', '抬升层'], ['canvas', '画布'], ['ctl', '控件底']];
  A.OVERRIDE_ACCENTS = [['accent', '强调色'], ['success', '成功色'], ['warning', '警告色'], ['danger', '危险色']];
  A.applyOverride = function (win, el, ov, color) {
    if (!el) return;
    (el.__uitsOv || []).forEach(n => el.style.removeProperty(n));
    el.__uitsOv = [];
    const U = win.document.__uits;
    if (!ov || !color || !color.enabled || !U) return;
    const D = A.derive(color.seeds);
    let surf = D.c[ov.surface || 'panel'] || D.c.panel;
    const lift = (ov.lift || 0) / 100;
    if (lift) surf = mixc(surf, lift > 0 ? D.text : D.bg, Math.abs(lift));
    const acc = D.c[ov.accent || 'accent'] || D.c.accent;
    const set = (n, v) => { el.style.setProperty(n, v, 'important'); el.__uitsOv.push(n); };
    SURFACE_TOKENS[U.family].forEach(n => set(n, fmt(surf)));
    const AD = A.deriveAccent(acc, D.dark);
    Object.entries(ACCENT_TOKENS[U.family]).forEach(([n, role]) => set(n, fmt(AD[role])));
    set('background-color', fmt(surf));
  };

  /* ───────── 紧凑 ───────── */
  A.supportsDensity = function (doc) {
    if (doc.__uitsDens !== undefined) return doc.__uitsDens;
    let ok = false;
    for (const sh of doc.styleSheets) walkRules(sheetRules(sh), r => { if (/data-density/.test(r.selectorText || '')) ok = true; });
    return (doc.__uitsDens = ok);
  };
  A.readDensity = function (win) {
    const U = win.document.__uits;
    if (!U) return null;
    if (A.supportsDensity(win.document)) return win.document.documentElement.dataset.density === 'comfortable' ? false : true;
    return U.densProps.length ? false : true;
  };
  A.applyDensity = function (win, compact) {
    const U = win.document.__uits;
    if (!U || compact === null || compact === undefined) return;
    const root = win.document.documentElement;
    if (A.supportsDensity(win.document)) {
      const want = compact ? 'compact' : 'comfortable';
      if (root.dataset.density !== want) {
        if (win.HY && win.HY.__origSetDensity) win.HY.__origSetDensity(want); else root.dataset.density = want;
        win.dispatchEvent(new win.Event('hy-resize'));
      }
      return;
    }
    U.densProps.forEach(n => root.style.removeProperty(n));
    U.densProps = [];
    if (compact) return;
    const cs = win.getComputedStyle(root);
    for (const sh of win.document.styleSheets) {
      walkRules(sheetRules(sh), r => {
        if (!/^(:root|html)$/.test((r.selectorText || '').trim())) return;
        for (let i = 0; i < r.style.length; i++) {
          const n = r.style[i];
          if (!/^--h-/.test(n) || U.densProps.includes(n)) continue;
          const v = parseFloat(cs.getPropertyValue(n));
          if (v > 0) { root.style.setProperty(n, v + 4 + 'px'); U.densProps.push(n); }
        }
      });
    }
    win.dispatchEvent(new win.Event('hy-resize'));
  };

  /* ───────── 与模板自带切换联动（单一控制入口） ───────── */
  A.hookHY = function (win, hooks) {
    const HY = win.HY;
    if (!HY || HY.__uitsHooked) return;
    HY.__uitsHooked = true;
    HY.__origSetTheme = HY.setTheme;
    HY.__origSetDensity = HY.setDensity;
    if (HY.setTheme) HY.setTheme = function (t) { return hooks.theme(t) ? undefined : HY.__origSetTheme.apply(this, arguments); };
    if (HY.setDensity) HY.setDensity = function (t) { return hooks.density(t) ? undefined : HY.__origSetDensity.apply(this, arguments); };
  };

  /* ───────── 工具栏方向 ───────── */
  const NATIVE = {
    blender(win, v) {
      const want = v ? 'side' : 'top', root = win.document.documentElement;
      if (root.dataset.toolbarPos === want) return true;
      if (win.HY && win.HY.setToolbarPos) { win.HY.setToolbarPos(want); return true; }
      return false;
    },
    max3ds(win, v) {
      const want = v ? 'side' : 'top', root = win.document.documentElement;
      if (root.dataset.tbpos === want) return true;
      const b = win.document.querySelector('[data-tbpos-toggle]');
      if (!b) return false;
      b.click();
      return true;
    },
    ue5(win, v) {
      const want = v ? 'side' : 'top', app = win.document.querySelector('.app');
      if (app && app.getAttribute('data-toolbar-pos') === want) return true;
      const b = win.document.querySelector('#toolbar-pos-seg button[data-pos="' + want + '"]');
      if (!b) return false;
      b.click();
      return true;
    },
  };
  const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; };
  A.findPrimaryToolbar = function (doc) {
    const c = [...doc.querySelectorAll('.toolbar,.tshelf,.tool-rail')].filter(e => !e.closest('.pane,' + EXCLUDE) && visible(e));
    const bars = c.filter(e => { const r = e.getBoundingClientRect(); return r.width >= 500 && r.height <= 90; });
    return bars[0] || c[0] || null;
  };
  A.toolbarIsHorizontal = el => { const r = el.getBoundingClientRect(); return r.width >= r.height; };

  function genericMove(win, el, vertical) {
    const doc = win.document;
    if (el.__uitsMove) {
      if (vertical) return true;
      const m = el.__uitsMove;
      m.parent.insertBefore(el, m.wrap);
      m.seq.forEach(n => m.parent.insertBefore(n, m.wrap));
      if (m.mainWrap) m.mainWrap.remove();
      m.wrap.remove();
      el.classList.remove('uits-vtool');
      m.main.classList.remove('uits-vmain');
      el.__uitsMove = null;
      return true;
    }
    if (!vertical || !A.toolbarIsHorizontal(el)) return true;
    const parent = el.parentElement;
    if (!parent) return false;
    const seq = [];
    let main = null, n = el.nextElementSibling;
    while (n) {
      seq.push(n);
      if (!/^(SCRIPT|STYLE)$/i.test(n.tagName) && visible(n)) {
        const c = win.getComputedStyle(n);
        if (parseFloat(c.flexGrow) > 0 || n.getBoundingClientRect().height >= 150) { main = n; break; }
      }
      n = n.nextElementSibling;
    }
    if (!main) return false;
    const mcs = win.getComputedStyle(main), pcs = win.getComputedStyle(parent);
    const grow = parseFloat(mcs.flexGrow) > 0;
    const wrap = doc.createElement('div');
    wrap.className = 'uits-vwrap';
    wrap.style.cssText = 'flex:' + (grow ? '1 1 0' : '0 0 auto') + ';' + (pcs.display === 'grid' ? `grid-column:${mcs.gridColumn};grid-row:${mcs.gridRow};` : '') + (grow ? '' : 'height:' + main.getBoundingClientRect().height + 'px;');
    parent.insertBefore(wrap, el);
    wrap.appendChild(el);
    let mainEl = main, mainWrap = null;
    if (seq.length > 1) {
      mainWrap = doc.createElement('div');
      mainWrap.className = 'uits-vmain';
      mainWrap.style.cssText = 'display:flex;flex-direction:column;min-height:0';
      seq.slice(0, seq.indexOf(main) + 1).forEach(x => mainWrap.appendChild(x));
      seq.length = seq.indexOf(main) + 1;
      wrap.appendChild(mainWrap);
      mainEl = mainWrap;
    } else {
      wrap.appendChild(main);
      main.classList.add('uits-vmain');
    }
    el.classList.add('uits-vtool');
    el.__uitsMove = { parent, wrap, main: mainEl, mainWrap, seq };
    return true;
  }

  A.applyNativeToolbar = function (win, tplMeta, dir) {
    const fn = tplMeta && NATIVE[tplMeta.toolbar];
    return fn ? fn(win, dir === 'v') : false;
  };
  A.applyGenericToolbar = function (win, el, dir) {
    if (!el) return { ok: false, how: 'none' };
    const v = dir === 'v';
    if (el.closest('#ribbon,.ribbon') || /\bribbon\b/.test(el.className || '')) return { ok: !v, how: 'ribbon' };
    return { ok: genericMove(win, el, v), how: 'generic' };
  };
  A.nativeToolbarDir = function (win, tplMeta) {
    const root = win.document.documentElement;
    if (!tplMeta) return null;
    if (tplMeta.toolbar === 'blender') return root.dataset.toolbarPos === 'side' ? 'v' : 'h';
    if (tplMeta.toolbar === 'max3ds') return root.dataset.tbpos === 'side' ? 'v' : 'h';
    if (tplMeta.toolbar === 'ue5') { const app = win.document.querySelector('.app'); return app && app.getAttribute('data-toolbar-pos') === 'side' ? 'v' : 'h'; }
    return null;
  };
  A.hasNativeToolbar = tplMeta => !!(tplMeta && NATIVE[tplMeta.toolbar]);
  A.nativeToolbarFn = (win, tplMeta, v) => NATIVE[tplMeta.toolbar](win, v);

  /* ───────── 模块枚举与描述 ───────── */
  function pathOf(el) {
    const parts = [];
    let e = el;
    const root = el.ownerDocument.documentElement;
    while (e && e.nodeType === 1 && e !== root) {
      if (e.id && el.ownerDocument.querySelectorAll('#' + CSS.escape(e.id)).length === 1) { parts.unshift('#' + CSS.escape(e.id)); break; }
      const p = e.parentElement;
      const i = p ? [...p.children].indexOf(e) + 1 : 1;
      parts.unshift(e.tagName.toLowerCase() + ':nth-child(' + i + ')');
      e = p;
    }
    return parts.join('>');
  }
  A.pathOf = pathOf;
  const hintOf = e => e.tagName.toLowerCase() + (e.id ? '#' + e.id : '') + (typeof e.className === 'string' && e.className.trim() ? '.' + e.className.trim().split(/\s+/).slice(0, 3).join('.') : '');
  A.visibleEl = el => (el && el.__uitsHost) || el;

  A.enumerate = function (win) {
    const doc = win.document, out = [], used = new Set();
    const okEl = (e, cat) => {
      if (e.closest(EXCLUDE) && !e.hasAttribute('data-uits-slot')) return false;
      if (cat.id === 'toolbar' && e.closest('.pane')) return false;
      if (cat.id === 'panel') return true;
      return e.hasAttribute('data-uits-hidden') || visible(e);
    };
    const counters = {};
    const add = (e, cat) => {
      const path = pathOf(e);
      const key = cat.id + '|' + path;
      if (used.has(key)) return;
      used.add(key);
      counters[cat.id] = (counters[cat.id] || 0) + 1;
      const title = e.getAttribute('data-title');
      out.push({ key, cat: cat.id, catLabel: cat.label, el: e, path, hint: hintOf(e), label: title || (cat.label + ' · ' + (e.id || (typeof e.className === 'string' && e.className.trim().split(/\s+/)[0]) || counters[cat.id])), hidden: !e.hasAttribute('data-uits-hidden') && !visible(e) });
    };
    for (const cat of A.CATS) {
      let els = [...doc.querySelectorAll(cat.sel)].filter(e => okEl(e, cat));
      els = els.filter(e => !els.some(o => o !== e && o.contains(e)));
      els.forEach(e => add(e, cat));
    }
    doc.querySelectorAll('[data-uits-slot]').forEach(e => {
      const cat = A.CATS.find(c => c.id === e.getAttribute('data-uits-slot'));
      if (cat) add(e, cat);
    });
    return out;
  };

  function stripForMatch(sel) {
    let s = sel.replace(/:{1,2}(hover|active|focus|focus-visible|focus-within|before|after|first-line|first-letter|placeholder|selection|disabled|checked|-webkit-[\w-]+|-moz-[\w-]+)(\([^)]*\))?/g, '');
    s = s.replace(/(^|[\s,>+~(])(html|:root)(\[[^\]]*\]|:[\w-]+(\([^)]*\))?)*/g, '$1html');
    return s.trim();
  }
  function splitTop(sel) {
    const out = []; let d = 0, cur = '';
    for (const ch of sel) {
      if (ch === '(' || ch === '[') d++;
      if (ch === ')' || ch === ']') d--;
      if (ch === ',' && d === 0) { out.push(cur); cur = ''; continue; }
      cur += ch;
    }
    out.push(cur);
    return out.map(s => s.trim()).filter(Boolean);
  }
  function shadowSel(sel) {
    return splitTop(sel).map(s => {
      let m = /^(html|:root)((?:\[[^\]]*\])*)\s*(.*)$/s.exec(s);
      if (m) {
        const base = m[2] ? ':host-context(html' + m[2] + ')' : '';
        const rest = m[3];
        return base ? (rest ? base + ' ' + rest : base) : (rest || ':host');
      }
      m = /^body\b\s*(.*)$/s.exec(s);
      if (m) return m[1] || ':host';
      return s;
    }).join(',');
  }
  A.shadowSel = shadowSel;

  function matchedCss(doc, el, U) {
    const parts = [], tokens = new Set();
    const matches = sel => {
      try {
        const t = stripForMatch(sel);
        if (!t) return true;
        return splitTop(t).some(x => { try { return el.matches(x) || !!el.querySelector(x); } catch (e) { return true; } });
      } catch (e) { return true; }
    };
    const pristine = r => (U && U.ruleOrig.get(r)) || r.style.cssText;
    const emit = list => {
      const out = [];
      for (const r of list) {
        if (r.selectorText !== undefined && r.style && !r.cssRules.length) {
          if (!matches(r.selectorText)) continue;
          const body = pristine(r);
          (body.match(/var\((--[\w-]+)/g) || []).forEach(v => tokens.add(v.slice(4)));
          out.push(shadowSel(r.selectorText) + '{' + body + '}');
        } else if (r.cssRules && r.conditionText !== undefined) {
          const inner = emit(r.cssRules);
          if (inner) out.push('@' + (r.constructor.name.includes('Supports') ? 'supports' : 'media') + ' ' + r.conditionText + '{' + inner + '}');
        } else if (r.name !== undefined && r.cssRules) {
          out.push(r.cssText);
        }
      }
      return out.join('\n');
    };
    for (const sh of doc.styleSheets) { const t = emit(sheetRules(sh)); if (t) parts.push(t); }
    return { css: parts.join('\n'), tokens: [...tokens] };
  }

  function regionOf(win, el) {
    const dockAttr = (el.closest('[data-dock]') || {}).getAttribute ? el.closest('[data-dock]').getAttribute('data-dock') : null;
    const r = el.getBoundingClientRect(), W = win.innerWidth, H = win.innerHeight;
    if (!r.width || !r.height) return dockAttr || 'hidden';
    const cx = (r.left + r.width / 2) / W, cy = (r.top + r.height / 2) / H;
    const h = cx < 0.28 ? 'left' : cx > 0.72 ? 'right' : 'center';
    const v = cy < 0.2 ? 'top' : cy > 0.8 ? 'bottom' : 'middle';
    return (dockAttr ? dockAttr + ' / ' : '') + v + '-' + h;
  }

  A.describe = function (win, m, tplMeta) {
    const doc = win.document, U = doc.__uits;
    const el = m.el, vis = A.visibleEl(el);
    const r = vis.getBoundingClientRect(), W = win.innerWidth, H = win.innerHeight;
    const cs = win.getComputedStyle(vis);
    const cmds = [];
    const seen = new Set();
    el.querySelectorAll('[data-cmd]').forEach(b => {
      const id = b.getAttribute('data-cmd');
      if (seen.has(id)) return;
      seen.add(id);
      const c = (win.HY && win.HY.cmds && win.HY.cmds[id]) || {};
      cmds.push({ id, label: c.l || b.getAttribute('data-tip') || b.getAttribute('title') || (b.textContent || '').trim().slice(0, 24), key: c.k || '', icon: c.i || '' });
    });
    const icons = [...new Set([...el.querySelectorAll('use')].map(u => (u.getAttribute('href') || u.getAttribute('xlink:href') || '').replace('#', '')).filter(Boolean))];
    const html = el.outerHTML;
    const { css, tokens } = matchedCss(doc, el, U);
    const root = win.getComputedStyle(doc.documentElement);
    const tokenVals = {};
    tokens.forEach(n => { tokenVals[n] = root.getPropertyValue(n).trim(); });
    const sizeTokens = {};
    for (const sh of doc.styleSheets) walkRules(sheetRules(sh), rule => {
      if (!/^(:root|html)$/.test((rule.selectorText || '').trim())) return;
      for (let i = 0; i < rule.style.length; i++) { const n = rule.style[i]; if (SIZE_TOKEN_RE.test(n)) sizeTokens[n] = root.getPropertyValue(n).trim(); }
    });
    const symIds = new Set();
    (html.match(/(?:xlink:)?href="#([^"]+)"/g) || []).forEach(s => symIds.add(s.replace(/.*#/, '').replace(/"$/, '')));
    (html.match(/url\(#([^)]+)\)/g) || []).forEach(s => symIds.add(s.slice(5, -1)));
    let sprite = '';
    symIds.forEach(id => { const n = doc.getElementById(id); if (n && !el.contains(n)) sprite += n.outerHTML; });
    const parentMod = el.parentElement ? el.parentElement.closest('[data-title],[data-dock],#dockL,#dockR,#dockB,.app,.workspace') : null;
    const sibs = el.parentElement ? [...el.parentElement.children] : [];
    const fam = U ? U.family : 'hy';
    return {
      category: m.cat, categoryLabel: m.catLabel, label: m.label, key: m.key,
      source: tplMeta ? { templateId: tplMeta.id, group: tplMeta.group, file: tplMeta.file, title: tplMeta.title, family: fam } : { templateId: 'blank', family: fam },
      element: { tag: el.tagName.toLowerCase(), id: el.id || null, classes: typeof el.className === 'string' ? el.className.trim().split(/\s+/).filter(Boolean) : [], dataTitle: el.getAttribute('data-title'), path: m.path },
      region: {
        area: regionOf(win, vis), dock: (el.closest('[data-dock]') || { getAttribute: () => null }).getAttribute('data-dock'),
        parent: parentMod ? hintOf(parentMod) : null, siblingIndex: sibs.indexOf(el), siblingCount: sibs.length, childCount: el.children.length,
      },
      position: { px: { x: Math.round(r.left + win.scrollX), y: Math.round(r.top + win.scrollY), w: Math.round(r.width), h: Math.round(r.height) }, pct: { x: +(r.left / W * 100).toFixed(2), y: +(r.top / H * 100).toFixed(2), w: +(r.width / W * 100).toFixed(2), h: +(r.height / H * 100).toFixed(2) }, viewport: { w: W, h: H } },
      layout: { orientation: m.cat === 'toolbar' ? (r.width >= r.height ? 'horizontal' : 'vertical') : null, display: cs.display, flex: cs.flex, position: cs.position, density: doc.documentElement.dataset.density || null, theme: doc.documentElement.dataset.theme || null },
      colors: { background: cs.backgroundColor, color: cs.color, border: cs.borderTopColor, tokens: tokenVals },
      content: { commands: cmds, icons, subPanels: [...el.querySelectorAll('.pane[data-title]')].map(p => p.getAttribute('data-title')), text: (el.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 240) },
      payload: { html, css, sprite, sizeTokens, htmlBytes: html.length },
      meta: U ? U.meta : null,
    };
  };

  /* ───────── 影子 DOM 替换 ───────── */
  A.mountCandidate = function (win, m, rec) {
    const doc = win.document, U = doc.__uits;
    A.unmountCandidate(win, m);
    const target = m.el;
    const cs = win.getComputedStyle(target), r = target.getBoundingClientRect();
    const parent = target.parentElement, pcs = win.getComputedStyle(parent);
    const flexRow = /flex/.test(pcs.display) && pcs.flexDirection.startsWith('row'), flexCol = /flex/.test(pcs.display) && pcs.flexDirection.startsWith('column');
    const grow = parseFloat(cs.flexGrow) > 0;
    const host = doc.createElement('div');
    host.className = 'uits-host';
    host.setAttribute('data-uits-for', m.key);
    let st = 'display:flex;flex-direction:column;position:relative;min-width:0;min-height:0;overflow:hidden;box-sizing:border-box;order:' + cs.order + ';flex:' + cs.flex + ';align-self:' + cs.alignSelf + ';';
    if (pcs.display === 'grid') st += 'grid-column:' + cs.gridColumn + ';grid-row:' + cs.gridRow + ';';
    if (flexRow) st += 'height:auto;width:' + (grow ? 'auto' : r.width + 'px') + ';';
    else if (flexCol) st += 'width:auto;height:' + (grow ? 'auto' : r.height + 'px') + ';';
    else st += 'width:' + r.width + 'px;height:' + r.height + 'px;';
    host.style.cssText = st;
    const sr = host.attachShadow({ mode: 'open', serializable: true });
    const p = rec.payload;
    const cross = rec.source.family !== U.family;
    let head = '';
    if (cross) head += ':host{' + Object.entries(ALIAS[rec.source.family + '>' + U.family] || {}).map(([k, v]) => k + ':' + v).join(';') + '}';
    const rootCs = win.getComputedStyle(doc.documentElement);
    const missing = Object.entries(p.sizeTokens || {}).filter(([n]) => !rootCs.getPropertyValue(n).trim());
    if (missing.length) head += ':host{' + missing.map(([k, v]) => k + ':' + v).join(';') + '}';
    head += ':host{font-family:var(--font-ui,sans-serif);font-size:var(--fs-ui,12px);color:var(--text,var(--fg,#ccc))}' +
      '.uits-root{flex:1 1 auto!important;min-width:0!important;min-height:0!important;width:auto!important;height:auto!important;max-width:none!important;margin:0!important}' +
      '[data-uits-hl]{outline:2px solid #ff9f0a;outline-offset:-2px}';
    sr.innerHTML = '<style class="uits-base">' + head + '</style><style class="uits-src">' + p.css + '</style>' +
      (p.sprite ? '<svg width="0" height="0" style="position:absolute" aria-hidden="true">' + p.sprite + '</svg>' : '') + p.html;
    const first = [...sr.children].find(c => !/^(STYLE|SVG)$/i.test(c.tagName));
    if (first) first.classList.add('uits-root');
    sr.addEventListener('click', ev => {
      const b = ev.target.closest && ev.target.closest('[data-cmd]');
      if (!b || !win.HY || !win.HY.cmds) return;
      const id = b.getAttribute('data-cmd');
      if (win.HY.cmds[id]) { try { win.HY.run(id); } catch (e) { /* 命令来自其它模板时可能缺依赖 */ } }
    });
    target.setAttribute('data-uits-hidden', '1');
    target.__uitsPrevDisplay = target.style.display;
    target.style.setProperty('display', 'none', 'important');
    parent.insertBefore(host, target);
    target.__uitsHost = host;
    host.__uitsRec = rec.id;
    const srcStyle = sr.querySelector('style.uits-src');
    if (srcStyle && srcStyle.sheet) A.registerSheet(U, srcStyle.sheet, rec.meta);
    return host;
  };
  A.unmountCandidate = function (win, m) {
    const el = m.el;
    if (!el.__uitsHost) return false;
    el.__uitsHost.remove();
    el.__uitsHost = null;
    el.removeAttribute('data-uits-hidden');
    if (el.__uitsPrevDisplay) el.style.display = el.__uitsPrevDisplay; else el.style.removeProperty('display');
    return true;
  };
  A.highlight = function (win, el, on) {
    if (!el) return;
    const t = A.visibleEl(el);
    if (on) t.setAttribute('data-uits-hl', ''); else t.removeAttribute('data-uits-hl');
  };

  /* ───────── 从空白建立 ───────── */
  A.blankHtml = function () {
    const slot = (cat, label, extra) => `<div class="slot s-${cat}" data-uits-slot="${cat}" data-label="${label}"${extra || ''}></div>`;
    return `<!DOCTYPE html><html lang="zh-CN" data-page="blank" data-theme="dark" data-density="compact"><head><meta charset="utf-8"><title>空白</title><style>
:root{--font-ui:"Segoe UI","Microsoft YaHei UI","PingFang SC",system-ui,sans-serif;--font-mono:Consolas,monospace;--fs-ui:12px;--h-title:32px;--h-menu:28px;--h-tool:38px;--h-status:24px;--bg-window:#1B1B1B;--bg-panel:#222222;--bg-raised:#2C2C2C;--bg-input:#161616;--bg-canvas:#212830;--bg-header:#1B1B1B;--bg-ctl:#2E2E2E;--bg-hover:rgba(255,255,255,.06);--bg-active:rgba(255,255,255,.11);--border:#2E2E2E;--border-strong:#3F3F3F;--text:#E6E6E6;--text-2:#A6A6A6;--text-3:#7C7C7C;--accent:#0A84FF;--accent-hover:#409CFF;--accent-press:#0070E0;--accent-soft:rgba(10,132,255,.22);--text-on-accent:#fff;--danger:#FF453A;--warning:#FFB340;--success:#30D158}
html[data-theme="light"]{--bg-window:#F5F5F7;--bg-panel:#FFFFFF;--bg-raised:#F0F0F2;--bg-input:#FFFFFF;--bg-canvas:#EEF1F5;--bg-header:#F5F5F7;--bg-ctl:#E8E8EA;--bg-hover:rgba(0,0,0,.05);--bg-active:rgba(0,0,0,.09);--border:#DCDCE0;--border-strong:#C4C4CA;--text:#1D1D1F;--text-2:#55555A;--text-3:#86868B;--accent:#0071E3;--accent-hover:#0A84FF;--accent-press:#0063C7;--accent-soft:rgba(0,113,227,.16)}
html[data-density="comfortable"]{--h-title:38px;--h-menu:32px;--h-tool:44px;--h-status:28px}
*{box-sizing:border-box}html,body{margin:0;height:100%;overflow:hidden;background:var(--bg-window);color:var(--text);font:var(--fs-ui)/1.4 var(--font-ui)}
.app{display:flex;flex-direction:column;height:100%}.row{flex:1;min-height:0;display:flex}.col{flex:1;min-width:0;display:flex;flex-direction:column}
.slot{display:flex;align-items:center;justify-content:center;color:var(--text-3);background:var(--bg-panel);border:1px dashed var(--border-strong);margin:1px}
.slot::after{content:attr(data-label) "（空槽）"}
.s-titlebar{height:var(--h-title);flex:none}.s-menubar{height:var(--h-menu);flex:none}.s-toolbar{height:var(--h-tool);flex:none}
.s-dockL{width:240px;flex:none}.s-dockR{width:280px;flex:none}.s-dockB{height:170px;flex:none}.s-viewport{flex:1;min-height:0;background:var(--bg-canvas)}.s-statusbar{height:var(--h-status);flex:none}
</style></head><body><div class="app">${slot('titlebar', '标题栏')}${slot('menubar', '菜单栏')}${slot('toolbar', '工具栏')}<div class="row">${slot('dockL', '左侧停靠区')}<div class="col">${slot('viewport', '视口')}${slot('dockB', '底部停靠区')}</div>${slot('dockR', '右侧停靠区')}</div>${slot('statusbar', '状态栏')}</div></body></html>`;
  };

  /* ───────── 静态快照 ───────── */
  A.snapshotHtml = function (win) {
    const doc = win.document;
    const clone = doc.documentElement.cloneNode(true);
    clone.querySelectorAll('script,#info-drawer,#uits-style').forEach(n => n.remove());
    let html = '<!DOCTYPE html>\n' + clone.outerHTML;
    if (doc.documentElement.getHTML) {
      try {
        const live = doc.documentElement.getHTML({ serializableShadowRoots: true });
        const tmp = doc.implementation.createHTMLDocument('');
        tmp.documentElement.innerHTML = live;
        tmp.querySelectorAll('script,#info-drawer,#uits-style').forEach(n => n.remove());
        tmp.querySelectorAll('[data-uits-hl]').forEach(n => n.removeAttribute('data-uits-hl'));
        html = '<!DOCTYPE html>\n<html lang="zh-CN"' + [...doc.documentElement.attributes].map(a => ' ' + a.name + '="' + a.value.replace(/"/g, '&quot;') + '"').join('') + '>' + tmp.documentElement.innerHTML + '</html>';
      } catch (e) { /* 回退到不含影子根的克隆 */ }
    }
    return html;
  };

  g.UiAdapters = A;
})(window);
