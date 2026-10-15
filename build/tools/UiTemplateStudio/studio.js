/* UiTemplateStudio · 工作台逻辑
 * 单一状态 S（可持久化） + 运行时 R（预览相关）。每个按钮只写 S 的一个字段，
 * runPipeline() 按固定顺序把 S 重放到预览上：替换 → 整体颜色 → 紧凑 → 工具栏方向 → 模块颜色覆盖。
 */
(function () {
  'use strict';
  const A = window.UiAdapters;
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
  const esc = s => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const LS = 'uits.state.v1';

  /* ───────── 状态 ───────── */
  const DEF = { tplId: 'cad-a', view: 'tpl', color: { enabled: false, seeds: { ...A.DEFAULT_SEEDS.dark } }, compact: null, toolbar: 'h', tpl: {}, open: {}, splitC: 0.62 };
  const S = (() => { try { return Object.assign(structuredClone(DEF), JSON.parse(localStorage.getItem(LS) || '{}')); } catch (e) { return structuredClone(DEF); } })();
  const saveState = () => { try { localStorage.setItem(LS, JSON.stringify(S)); } catch (e) { /* 配额满时忽略 */ } };
  const T = () => S.tpl[S.tplId] || (S.tpl[S.tplId] = { repl: {}, ov: {}, tb: {} });

  const R = { token: 0, win: null, meta: null, ready: false, modules: [], byKey: {}, primary: null, sel: null, hover: null, cand: null, catalog: new Map(), byTpl: {}, indexing: { n: 0, total: 0 }, mine: [], stats: null, desc: {}, writeHandle: null, tplDir: null, scale: 1 };

  /* ───────── 模板清单 ───────── */
  let GROUPS = [], TPLS = [], TPL = {};
  const BLANK = { id: 'blank', group: '空白', file: '', title: '空白', family: 'hy', toolbar: 'none' };
  const metaOf = id => (id === 'blank' ? BLANK : TPL[id]);
  const shortName = t => (t.file || t.title || '空白').replace(/\.html(\.bak-cmdline)?$/, m => (m.includes('bak') ? ' (bak)' : ''));

  const LOAD_FIXES = (() => {
    const blender = t => {
      t = t.split("toast('已退出（演示）')}})}}]}").join("toast('已退出（演示）')})}}]}");
      const re = /\r?\n\}\);(\r?\n)\}\)\(\);/g;
      let last = null, m;
      while ((m = re.exec(t))) last = m;
      if (last) t = t.slice(0, last.index) + '\n}});' + last[1] + '})();' + t.slice(last.index + last[0].length);
      return t.replace(/(?<!\$)\$\(('[^']*')\)\.forEach/g, (m0, sel) => '$' + '$(' + sel + ').forEach');
    };
    return { 'blender-a': blender, 'blender-b': blender, 'blender-c': blender };
  })();

  /* ───────── IndexedDB ───────── */
  const idb = () => new Promise((res, rej) => { const r = indexedDB.open('uits', 1); r.onupgradeneeded = () => r.result.createObjectStore('kv'); r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); });
  const idbGet = async k => { try { const db = await idb(); return await new Promise(res => { const q = db.transaction('kv').objectStore('kv').get(k); q.onsuccess = () => res(q.result); q.onerror = () => res(undefined); }); } catch (e) { return undefined; } };
  const idbSet = async (k, v) => { try { const db = await idb(); await new Promise(res => { const tx = db.transaction('kv', 'readwrite'); tx.objectStore('kv').put(v, k); tx.oncomplete = res; tx.onerror = res; }); } catch (e) { /* 无 IndexedDB 时仅本次有效 */ } };

  /* ───────── 加载器 ───────── */
  const textCache = new Map();
  async function ensurePerm(h, mode) {
    if (!h.queryPermission) return true;
    if ((await h.queryPermission({ mode })) === 'granted') return true;
    return (await h.requestPermission({ mode })) === 'granted';
  }
  async function readFromDir(t) {
    const h = R.tplDir || (R.tplDir = await idbGet('tplDir'));
    if (!h) throw new Error('NEED_DIR');
    if (!(await ensurePerm(h, 'read'))) throw new Error('NEED_DIR');
    let root = h;
    try { root = await h.getDirectoryHandle('templates'); } catch (e) { /* 选的就是 templates 目录 */ }
    const gd = await root.getDirectoryHandle(t.group);
    return (await (await gd.getFileHandle(t.file)).getFile()).text();
  }
  async function getText(t) {
    if (textCache.has(t.id)) return textCache.get(t.id);
    let txt;
    try {
      const r = await fetch(t.url);
      if (!r.ok) throw new Error('http ' + r.status);
      txt = await r.text();
    } catch (e) {
      txt = await readFromDir(t);
    }
    if (LOAD_FIXES[t.id]) txt = LOAD_FIXES[t.id](txt);
    textCache.set(t.id, txt);
    return txt;
  }
  function showBanner(html) { const b = $('#banner'); b.innerHTML = html; b.hidden = false; }
  const hideBanner = () => { $('#banner').hidden = true; };

  /* ───────── 预览 ───────── */
  function layoutScaler() {
    const st = $('#stage'), W = st.clientWidth, H = st.clientHeight;
    if (!W || !H) return;
    const s = Math.min(1, W / 1440), sc = $('#scaler');
    sc.style.width = W / s + 'px';
    sc.style.height = H / s + 'px';
    sc.style.transform = 'scale(' + s + ')';
    R.scale = s;
    refreshOverlay();
  }

  async function openTemplate(id) {
    S.tplId = id;
    saveState();
    const token = ++R.token;
    Object.assign(R, { ready: false, modules: [], byKey: {}, primary: null, sel: null, hover: null, cand: null, desc: {}, win: null });
    renderAll();
    let text;
    try { text = id === 'blank' ? A.blankHtml() : await getText(TPL[id]); } catch (e) {
      if (token !== R.token) return;
      showBanner('<div>无法直接读取模板文件（可能是 file:// 打开）。</div><div class="dim">请选择 <code>UiTemplateStudio</code> 或其中的 <code>templates</code> 目录，之后会被记住。</div><button class="mb-btn pri" id="btnPickTpl">选择模板目录…</button>');
      $('#btnPickTpl').onclick = async () => {
        try { R.tplDir = await window.showDirectoryPicker({ id: 'uits-templates' }); await idbSet('tplDir', R.tplDir); openTemplate(id); } catch (err) { toast('未选择目录'); }
      };
      return;
    }
    if (token !== R.token) return;
    hideBanner();
    const f = document.createElement('iframe');
    f.id = 'frame';
    f.title = '模板预览';
    f.onload = () => onFrameLoad(token);
    $('#frame').replaceWith(f);
    f.srcdoc = text;
    layoutScaler();
  }

  function onFrameLoad(token) {
    if (token !== R.token) return;
    const f = $('#frame'), win = f.contentWindow, meta = metaOf(S.tplId);
    if (!win || !win.document.documentElement) return;
    R.win = win;
    R.meta = meta;
    A.init(win, meta.family);
    A.hookHY(win, {
      theme: t => { if (!S.color.enabled) return false; setMode(t === 'dark' ? 'dark' : 'light'); return true; },
      density: t => { if (S.compact === null) return false; setCompact(t === 'compact'); return true; },
    });
    win.addEventListener('hy-resize', () => { updateCompactUI(); refreshOverlay(); });
    win.document.addEventListener('click', onFramePick, true);
    A.applyColor(win, S.color);
    A.applyDensity(win, S.compact);
    setTimeout(() => finishLoad(token), 700);
  }

  function finishLoad(token) {
    if (token !== R.token || !R.win) return;
    const win = R.win, doc = win.document;
    R.modules = A.enumerate(win);
    R.byKey = Object.fromEntries(R.modules.map(m => [m.key, m]));
    R.primary = S.tplId === 'blank' ? doc.querySelector('[data-uits-slot="toolbar"]') : A.findPrimaryToolbar(doc);
    R.ready = true;
    runPipeline();
    renderAll();
  }

  function onFramePick(e) {
    if (!e.altKey) return;
    e.preventDefault();
    e.stopPropagation();
    let best = null, ba = Infinity;
    R.modules.forEach(m => {
      const v = A.visibleEl(m.el);
      if (v === e.target || v.contains(e.target)) { const r = v.getBoundingClientRect(), a = r.width * r.height; if (a > 0 && a < ba) { best = m; ba = a; } }
    });
    if (best) selectModule(best.key);
  }

  /* ───────── 应用管线（幂等） ───────── */
  let pipeQueued = false;
  function schedulePipeline() { if (pipeQueued) return; pipeQueued = true; requestAnimationFrame(() => { pipeQueued = false; runPipeline(); }); }

  function runPipeline() {
    const win = R.win;
    if (!win || !R.ready) return;
    const tp = T();
    R.modules.forEach(m => {
      const want = tp.repl[m.key], cur = m.el.__uitsHost ? m.el.__uitsHost.__uitsRec : null;
      if (want === cur) return;
      if (!want) { A.unmountCandidate(win, m); return; }
      const rec = getRec(want);
      if (!rec || m.hidden) { delete tp.repl[m.key]; return; }
      A.mountCandidate(win, m, rec);
    });
    R.stats = A.applyColor(win, S.color);
    A.applyDensity(win, S.compact);
    applyToolbars();
    R.modules.forEach(m => {
      const vis = A.visibleEl(m.el), ov = tp.ov[m.key] || null;
      if (vis !== m.el) A.applyOverride(win, m.el, null, S.color);
      A.applyOverride(win, vis, ov, S.color);
    });
    R.desc = {};
    refreshOverlay();
    updateColorNote();
    updateCompactUI();
    updateToolbarUI();
    clearTimeout(runPipeline._t);
    runPipeline._t = setTimeout(() => { renderInfo(); }, 120);
  }

  function applyToolbars() {
    const win = R.win, meta = R.meta;
    if (A.hasNativeToolbar(meta)) { A.applyNativeToolbar(win, meta, S.toolbar); return; }
    const prim = R.primary && !R.primary.__uitsHost ? R.primary : null;
    if (prim) A.applyGenericToolbar(win, prim, S.toolbar);
    const tb = T().tb;
    R.modules.filter(m => m.cat === 'toolbar' && m.el !== R.primary && !m.el.__uitsHost).forEach(m => {
      const d = tb[m.key] || 'h';
      if (d === 'v' || m.el.__uitsMove) A.applyGenericToolbar(win, m.el, d);
    });
  }

  function getRec(id) { return R.catalog.get(id) || R.mine.find(r => r.id === id); }

  /* ───────── 状态写入（每个控件只写一个字段） ───────── */
  function setMode(mode) {
    if (S.color.seeds.mode !== mode) S.color.seeds = { ...A.DEFAULT_SEEDS[mode] };
    saveState(); schedulePipeline(); renderColorPop();
  }
  function setCompact(v) { S.compact = v; saveState(); schedulePipeline(); updateCompactUI(); }
  function setToolbarDir(dir) {
    const sel = R.sel && R.byKey[R.sel];
    if (!A.hasNativeToolbar(R.meta) && sel && sel.cat === 'toolbar' && sel.el !== R.primary) T().tb[sel.key] = dir; else S.toolbar = dir;
    saveState(); schedulePipeline(); updateToolbarUI();
  }

  /* ───────── 渲染：左侧目录 ───────── */
  function renderAll() { renderCurName(); renderTplTree(); renderModTree(); renderCandidates(); renderInfo(); renderOps(); updateColorNote(); updateCompactUI(); updateToolbarUI(); refreshOverlay(); renderDirName(); }

  function renderCurName() { const m = metaOf(S.tplId); $('#curName').textContent = m ? (m.group + ' · ' + (m.file ? shortName(m) : '从空白建立')) : '—'; $('#btnBlank').classList.toggle('on', S.tplId === 'blank'); }

  function renderTplTree() {
    const cur = metaOf(S.tplId);
    $('#tplTree').innerHTML = GROUPS.map(g => {
      const open = S.open[g.id] !== undefined ? S.open[g.id] : (cur && cur.group === g.id);
      return '<div class="grp' + (open ? ' open' : '') + '" data-g="' + esc(g.id) + '"><button class="grp-h"><span class="chev">▸</span>' + esc(g.name) + '<span class="n dim" style="margin-left:auto">' + g.items.length + '</span></button><div class="grp-b">' +
        g.items.map(it => '<button class="item' + (it.id === S.tplId ? ' on' : '') + '" data-id="' + esc(it.id) + '" title="' + esc(it.title) + '">' + esc(shortName(it)) + (LOAD_FIXES[it.id] ? '<span class="tag" title="磁盘文件保持原样；加载时修复了原文件的脚本错误">修复</span>' : '') + '</button>').join('') + '</div></div>';
    }).join('');
  }

  function renderModTree() {
    const box = $('#modTree');
    if (!R.ready) { box.innerHTML = '<div class="empty">' + (R.win ? '正在识别模块…' : '尚未载入模板') + '</div>'; return; }
    const byCat = {};
    R.modules.forEach(m => (byCat[m.cat] = byCat[m.cat] || []).push(m));
    let h = '';
    A.CATS.forEach(c => {
      const list = byCat[c.id];
      if (!list) return;
      const open = S.open['m:' + c.id] !== false;
      h += '<div class="grp' + (open ? ' open' : '') + '" data-mg="' + c.id + '"><button class="grp-h"><span class="chev">▸</span>' + esc(c.label) + '<span class="n dim" style="margin-left:auto">' + list.length + '</span></button><div class="grp-b">' +
        list.map(m => '<button class="item' + (m.key === R.sel ? ' on' : '') + (m.hidden ? ' hid' : '') + '" data-key="' + esc(m.key) + '" title="' + esc(m.hint) + '">' + esc(m.label) + (T().repl[m.key] ? '<span class="tag">已替换</span>' : '') + '</button>').join('') + '</div></div>';
    });
    if (R.mine.length) h += '<div class="grp open"><button class="grp-h"><span class="chev">▸</span>我的模块<span class="n dim" style="margin-left:auto">' + R.mine.length + '</span></button><div class="grp-b">' + R.mine.map(r => '<button class="item" data-mine="' + esc(r.id) + '">' + esc(r.label) + '<span class="tag">' + esc(r.categoryLabel) + '</span></button>').join('') + '</div></div>';
    box.innerHTML = h || '<div class="empty">此模板没有可识别的模块</div>';
  }

  function setView(v) {
    S.view = v; saveState();
    $('#btnTpl').classList.toggle('on', v === 'tpl');
    $('#btnMod').classList.toggle('on', v === 'mod');
    $('#dirTpl').hidden = v !== 'tpl';
    $('#dirMod').hidden = v !== 'mod';
  }

  /* ───────── 选中模块与候选 ───────── */
  function selectModule(key) {
    R.sel = key; R.cand = null;
    renderModTree(); renderCandidates(); renderInfo(); renderOps(); updateToolbarUI(); refreshOverlay();
    const it = $('#modTree .item.on'); if (it) it.scrollIntoView({ block: 'nearest' });
  }

  function candList(cat) {
    const groups = [];
    GROUPS.forEach(g => {
      const items = [];
      g.items.forEach(t => (R.byTpl[t.id] || []).forEach(r => { if (r.category === cat) items.push(r); }));
      if (items.length) groups.push({ name: g.name, items });
    });
    const mine = R.mine.filter(r => r.category === cat);
    if (mine.length) groups.push({ name: '我的模块', items: mine });
    return groups;
  }

  function renderCandidates() {
    const body = $('#candBody'), sel = R.sel && R.byKey[R.sel];
    const ix = R.indexing;
    $('#candInfo').textContent = ix.total && ix.n < ix.total ? '索引中 ' + ix.n + ' / ' + ix.total : (ix.total ? '已索引 ' + ix.total + ' 个模板' : '');
    if (!sel) { $('#candTitle').textContent = '候选'; body.innerHTML = '<div class="empty">在左侧「模块」目录选择一个模块（或在预览中按住 Alt 点击），这里会列出它所在类别的全部候选。</div>'; return; }
    $('#candTitle').textContent = '候选 · ' + sel.catLabel;
    const groups = candList(sel.cat);
    body.innerHTML = groups.length ? groups.map(g => '<div class="cand-grp"><h5>' + esc(g.name) + ' · ' + g.items.length + '</h5><div class="cards">' + g.items.map(r => {
      const p = r.position && r.position.px;
      const isCur = r.tplId === S.tplId && r.key === R.sel;
      return '<button class="card' + (r.id === R.cand ? ' on' : '') + '" data-cand="' + esc(r.id) + '"><b>' + esc(r.label) + (isCur ? '<span class="badge">当前</span>' : '') + (r.kind === 'mine' ? '<span class="badge mine">我的</span>' : '') + '</b><small>' + esc(r.kind === 'mine' ? '保存于 ' + r.savedAt.slice(0, 10) : shortName(r.source)) + (p && p.w ? ' · ' + p.w + '×' + p.h : '') + '</small></button>';
    }).join('') + '</div></div>').join('') : '<div class="empty">' + (ix.n < ix.total ? '索引尚未完成，请稍候…' : '没有同类候选') + '</div>';
  }

  /* ───────── 渲染：右侧信息与操作 ───────── */
  const kb = n => (n > 1024 ? (n / 1024).toFixed(1) + ' KB' : n + ' B');
  function getDesc(m) { return R.desc[m.key] || (R.desc[m.key] = A.describe(R.win, m, R.meta === BLANK ? null : R.meta)); }

  function renderInfo() {
    const body = $('#infoBody'), sel = R.sel && R.byKey[R.sel];
    $('#infoTag').textContent = '';
    if (!sel) {
      const m = metaOf(S.tplId);
      body.innerHTML = '<div class="empty">尚未选中模块</div>' + (m ? '<dl class="kv"><dt>模板</dt><dd>' + esc(m.title) + '</dd><dt>分组</dt><dd>' + esc(m.group) + '</dd><dt>家族</dt><dd>' + esc(m.family) + '</dd><dt>模块数</dt><dd>' + R.modules.length + '</dd>' + (LOAD_FIXES[m.id] ? '<dt>加载修复</dt><dd>原文件有脚本错误（括号错位、$ 误用），已在加载时修复（磁盘文件未改）</dd>' : '') + '</dl>' : '');
      return;
    }
    let d;
    try { d = getDesc(sel); } catch (e) { body.innerHTML = '<div class="msg warn">读取模块信息失败：' + esc(e.message) + '</div>'; return; }
    const o = T().ov[sel.key], rep = T().repl[sel.key] && getRec(T().repl[sel.key]);
    $('#infoTag').textContent = d.categoryLabel;
    const p = d.position.px, q = d.position.pct;
    let h = '<dl class="kv"><dt>名称</dt><dd><b>' + esc(d.label) + '</b></dd><dt>类别</dt><dd>' + esc(d.categoryLabel) + '</dd><dt>来源</dt><dd>' + esc(d.source.group || '') + ' · ' + esc(d.source.file || '空白') + '</dd>' +
      '<dt>区域</dt><dd>' + esc(d.region.area) + (d.region.parent ? '（父：' + esc(d.region.parent) + '）' : '') + '</dd>' +
      '<dt>位置</dt><dd>x ' + p.x + ' · y ' + p.y + ' px（' + q.x + '% , ' + q.y + '%）</dd><dt>尺寸</dt><dd>' + p.w + ' × ' + p.h + ' px（' + q.w + '% × ' + q.h + '%）</dd>' +
      (d.layout.orientation ? '<dt>方向</dt><dd>' + (d.layout.orientation === 'horizontal' ? '水平' : '竖向') + '</dd>' : '') +
      '<dt>元素</dt><dd><code>' + esc(d.element.tag + (d.element.id ? '#' + d.element.id : '') + d.element.classes.map(c => '.' + c).join('')) + '</code></dd>' +
      '<dt>DOM 路径</dt><dd><code>' + esc(d.element.path) + '</code></dd><dt>子元素</dt><dd>' + d.region.childCount + ' 个，兄弟序号 ' + d.region.siblingIndex + '/' + d.region.siblingCount + '</dd>' +
      '<dt>命令</dt><dd>' + d.content.commands.length + ' 个' + (d.content.commands.length ? '：' + esc(d.content.commands.slice(0, 6).map(c => c.label).join('、')) + (d.content.commands.length > 6 ? '…' : '') : '') + '</dd>' +
      '<dt>图标</dt><dd>' + d.content.icons.length + ' 个</dd>' + (d.content.subPanels.length ? '<dt>子面板</dt><dd>' + esc(d.content.subPanels.join('、')) + '</dd>' : '') +
      '<dt>颜色</dt><dd>背景 ' + esc(d.colors.background) + '<br>文字 ' + esc(d.colors.color) + '</dd><dt>令牌</dt><dd>' + Object.keys(d.colors.tokens).length + ' 个引用</dd>' +
      '<dt>体积</dt><dd>HTML ' + kb(d.payload.html.length) + ' · CSS ' + kb(d.payload.css.length) + '</dd></dl>';
    if (rep) h += '<div class="sec-h">已替换为</div><div class="msg">' + esc(rep.label) + ' · ' + esc(rep.source.file || '') + (rep.source.family !== R.meta.family ? '（跨家族：颜色令牌已映射，交互行为不保留）' : '') + '</div>';
    if (o) h += '<div class="sec-h">模块颜色覆盖</div><div class="msg">底色角色 ' + esc(o.surface) + ' · 偏移 ' + o.lift + '% · 强调 ' + esc(o.accent) + '</div>';
    const cand = R.cand && getRec(R.cand);
    if (cand) h += '<div class="sec-h">所选候选</div><dl class="kv"><dt>名称</dt><dd>' + esc(cand.label) + '</dd><dt>来自</dt><dd>' + esc(cand.source.group || '我的模块') + ' · ' + esc(cand.source.file || '') + '</dd><dt>家族</dt><dd>' + esc(cand.source.family) + (cand.source.family !== R.meta.family ? '（与当前模板不同）' : '') + '</dd><dt>体积</dt><dd>HTML ' + kb(cand.payload.html.length) + '</dd></dl>';
    body.innerHTML = h;
  }

  function renderOps() {
    const body = $('#opsBody'), sel = R.sel && R.byKey[R.sel];
    $('#opsTag').textContent = sel ? sel.label : '';
    if (!sel) { body.innerHTML = '<div class="empty">选中模块后可在这里替换、调色并保存模块模型</div>'; return; }
    const tp = T(), replaced = !!tp.repl[sel.key], cand = R.cand && getRec(R.cand), ov = tp.ov[sel.key] || { surface: 'panel', lift: 0, accent: 'accent' };
    const dis = !S.color.enabled ? ' disabled' : '';
    const opts = (list, v) => list.map(([k, l]) => '<option value="' + k + '"' + (k === v ? ' selected' : '') + '>' + l + '</option>').join('');
    body.innerHTML = '<div class="btn-row"><button class="mb-btn pri" data-act="replace"' + (cand && !sel.hidden ? '' : ' disabled') + ' title="用候选区所选模块替换预览中的当前模块">替换为所选候选</button>' +
      '<button class="mb-btn" data-act="restore"' + (replaced ? '' : ' disabled') + '>恢复当前模块</button><button class="mb-btn" data-act="restoreAll"' + (Object.keys(tp.repl).length ? '' : ' disabled') + '>恢复全部</button></div>' +
      '<div class="sec-h">模块颜色（只能在整体颜色已启用后调整，且随整体变化）</div>' +
      '<div class="field"><span style="width:60px">底色角色</span><select data-ov="surface"' + dis + '>' + opts(A.OVERRIDE_SURFACES, ov.surface) + '</select></div>' +
      '<div class="field"><span style="width:60px">明暗偏移</span><input type="range" min="-20" max="20" step="1" value="' + ov.lift + '" data-ov="lift"' + dis + '><span data-liftv style="width:34px;text-align:right">' + ov.lift + '%</span></div>' +
      '<div class="field"><span style="width:60px">强调角色</span><select data-ov="accent"' + dis + '>' + opts(A.OVERRIDE_ACCENTS, ov.accent) + '</select></div>' +
      '<div class="btn-row"><button class="mb-btn" data-act="clearOv"' + (tp.ov[sel.key] ? '' : ' disabled') + '>清除覆盖</button></div>' +
      (S.color.enabled ? '' : '<div class="msg warn">请先在中间顶栏「调整颜色」中启用整体颜色。</div>') +
      '<div class="sec-h">保存当前模块的全部结果</div><div class="btn-row"><button class="mb-btn pri" data-act="saveMod" title="写入「模块模型」目录，并加入「我的模块」">保存为模块模型</button><button class="mb-btn" data-act="copyAI" title="复制 Markdown + JSON，供 AI 使用">复制给 AI</button></div>' +
      '<div class="msg" id="opsMsg">替换后模块会跟随整体颜色、紧凑和工具栏方向。</div>';
  }

  /* ───────── 中间顶栏控件 ───────── */
  function updateCompactUI() {
    const b = $('#btnCompact');
    let on = S.compact;
    if (on === null) on = R.win && R.ready ? A.readDensity(R.win) : true;
    b.classList.toggle('on', !!on);
    b.title = (S.compact === null ? '当前跟随模板：' : '已设定：') + (on ? '紧凑' : '舒适');
  }
  function updateToolbarUI() {
    const seg = $('#tbSeg'), sel = R.sel && R.byKey[R.sel];
    const native = A.hasNativeToolbar(R.meta);
    const forSel = !native && sel && sel.cat === 'toolbar' && sel.el !== R.primary;
    let dir = forSel ? (T().tb[sel.key] || 'h') : S.toolbar;
    const hasAny = native || R.primary || R.modules.some(m => m.cat === 'toolbar');
    const ribbonSel = sel && sel.cat === 'ribbon';
    $('#tbLab').textContent = forSel ? '工具栏 · ' + sel.label.replace(/^工具栏 · /, '') : (native ? '工具栏（原生）' : '工具栏');
    $$('#tbSeg button').forEach(b => { b.classList.toggle('on', b.dataset.dir === dir); b.disabled = !R.ready || !hasAny; });
    seg.title = !R.ready ? '' : !hasAny ? '此模板没有独立工具栏' : ribbonSel ? 'Ribbon 功能区不支持竖放' : forSel ? '作用于选中的工具栏' : '作用于模板的主工具栏';
  }
  function updateColorNote() {
    $('#btnColor').classList.toggle('on', !!S.color.enabled);
    const n = $('#colorNote');
    if (n) n.textContent = !S.color.enabled ? '未启用：预览保持原模板的颜色。' : (R.stats ? '已映射 ' + (R.stats.neutral + R.stats.accent) + ' / ' + R.stats.total + ' 处写死色值（中性 ' + R.stats.neutral + ' · 强调 ' + R.stats.accent + '），语义色 ' + R.stats.kept + ' 处保留。' : '等待预览载入…');
  }

  const SEED_LABELS = [['bg', '底色'], ['panel', '面板'], ['text', '文字'], ['accent', '强调'], ['canvas', '画布']];
  function renderColorPop() {
    const pop = $('#colorPop');
    if (pop.hidden) return;
    const sd = S.color.seeds;
    pop.innerHTML = '<div class="row-between"><label class="switch"><input type="checkbox" id="colEnable"' + (S.color.enabled ? ' checked' : '') + '> 启用整体颜色</label>' +
      '<div class="seg"><button data-mode="dark" class="' + (sd.mode === 'dark' ? 'on' : '') + '">深色</button><button data-mode="light" class="' + (sd.mode === 'light' ? 'on' : '') + '">浅色</button></div></div>' +
      '<h4>预设</h4><div class="chips">' + A.PRESETS.map(p => '<button class="chip" data-preset="' + p.id + '">' + esc(p.name) + '</button>').join('') + '</div>' +
      '<h4>种子色（其余颜色由它们派生）</h4><div class="swatches">' + SEED_LABELS.map(([k, l]) => '<label>' + l + '<input type="color" data-seed="' + k + '" value="' + sd[k].toLowerCase() + '"></label>').join('') + '</div>' +
      '<div class="note" id="colorNote"></div><div class="row-between" style="margin-top:8px"><button class="chip" id="colReset">重置</button></div>';
    updateColorNote();
  }

  /* ───────── 覆盖层 ───────── */
  function placeOv(id, key) {
    const box = $(id), m = key && R.byKey[key];
    if (!m || !R.ready) { box.style.display = 'none'; return; }
    const r = A.visibleEl(m.el).getBoundingClientRect();
    if (!r.width || !r.height) { box.style.display = 'none'; return; }
    Object.assign(box.style, { display: 'block', left: r.left + 'px', top: r.top + 'px', width: r.width + 'px', height: r.height + 'px' });
  }
  function refreshOverlay() { placeOv('#ovSel', R.sel); placeOv('#ovHover', R.hover && R.hover !== R.sel ? R.hover : null); }

  /* ───────── 导出 ───────── */
  const pad = n => String(n).padStart(2, '0');
  const stamp = () => { const d = new Date(); return d.getFullYear() + pad(d.getMonth() + 1) + pad(d.getDate()) + '-' + pad(d.getHours()) + pad(d.getMinutes()) + pad(d.getSeconds()); };
  const safe = s => String(s).replace(/[\\/:*?"<>|\s]+/g, '_').slice(0, 60);
  function colorModel() { return { enabled: !!S.color.enabled, mode: S.color.seeds.mode, seeds: { ...S.color.seeds }, mapped: R.stats }; }

  function regionList() {
    const tp = T();
    return R.modules.map(m => {
      const v = A.visibleEl(m.el), r = v.getBoundingClientRect(), rep = tp.repl[m.key] && getRec(tp.repl[m.key]);
      return { key: m.key, category: m.cat, label: m.label, path: m.path, hidden: m.hidden, rect: { x: Math.round(r.left), y: Math.round(r.top), w: Math.round(r.width), h: Math.round(r.height) }, replacedBy: rep ? { id: rep.id, label: rep.label, from: rep.source } : null, colorOverride: tp.ov[m.key] || null, toolbarDir: m.cat === 'toolbar' ? (tp.tb[m.key] || null) : undefined };
    });
  }
  function buildWhole() {
    const m = metaOf(S.tplId), tp = T();
    return {
      type: 'whole-model', version: 1, savedAt: new Date().toISOString(), name: (m.file ? shortName(m) : '空白') + '-' + stamp(),
      template: S.tplId === 'blank' ? { blank: true } : { id: m.id, group: m.group, file: m.file, title: m.title, family: m.family },
      color: colorModel(), compact: R.win ? A.readDensity(R.win) : null, compactSetting: S.compact,
      toolbar: { global: S.toolbar, native: A.hasNativeToolbar(R.meta) ? A.nativeToolbarDir(R.win, R.meta) : null, perModule: tp.tb },
      regions: regionList(), replacements: Object.entries(tp.repl).map(([k, id]) => ({ target: k, candidate: id })),
    };
  }
  function buildModule() {
    const sel = R.byKey[R.sel], tp = T();
    if (!sel) return null;
    const d = JSON.parse(JSON.stringify(getDesc(sel)));
    const rep = tp.repl[sel.key] && getRec(tp.repl[sel.key]);
    if (rep) { d.payload = rep.payload; d.replacedFrom = rep.source; d.source = rep.source; d.label = rep.label; d.meta = rep.meta; }
    const ov = tp.ov[sel.key] || null;
    return Object.assign(d, {
      type: 'module-model', version: 1, id: 'mine::' + Date.now().toString(36) + Math.random().toString(36).slice(2, 6), kind: 'mine', savedAt: new Date().toISOString(),
      studio: { targetTemplate: S.tplId, color: colorModel(), compact: S.compact, toolbar: { global: S.toolbar, forThis: tp.tb[sel.key] || null }, colorOverride: ov },
      override: ov, tplId: S.tplId,
    });
  }

  async function writeOut(rel, text) {
    const h = R.writeHandle;
    if (h && (await ensurePerm(h, 'readwrite'))) {
      const parts = rel.split('/');
      let dir = h;
      for (const p of parts.slice(0, -1)) dir = await dir.getDirectoryHandle(p, { create: true });
      const w = await (await dir.getFileHandle(parts[parts.length - 1], { create: true })).createWritable();
      await w.write(text);
      await w.close();
      return '已写入 ' + h.name + '/' + rel;
    }
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([text], { type: rel.endsWith('.html') ? 'text/html' : 'application/json' }));
    a.download = rel.split('/').join('__');
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    return '已下载 ' + a.download;
  }
  async function saveWhole() {
    if (!R.ready) { toast('预览尚未就绪'); return null; }
    const w = buildWhole(), base = '整体模型/' + safe(w.name);
    const msg = await writeOut(base + '.whole.json', JSON.stringify(w, null, 2));
    await writeOut(base + '.whole.html', A.snapshotHtml(R.win));
    toast(msg + '（含静态快照 .whole.html）');
    return w;
  }
  async function saveModule() {
    const mod = buildModule();
    if (!mod) return null;
    const rec = JSON.parse(JSON.stringify(mod));
    rec.id = mod.id; rec.kind = 'mine';
    const msg = await writeOut('模块模型/' + safe(mod.categoryLabel) + '/' + safe(mod.label) + '-' + stamp() + '.module.json', JSON.stringify(mod, null, 2));
    R.mine.push(rec);
    await idbSet('mine', R.mine);
    renderModTree(); renderCandidates();
    toast(msg + '，已加入「我的模块」');
    return mod;
  }
  function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) return navigator.clipboard.writeText(text).catch(() => fallbackCopy(text));
    return Promise.resolve(fallbackCopy(text));
  }
  function fallbackCopy(text) { const t = document.createElement('textarea'); t.value = text; document.body.appendChild(t); t.select(); document.execCommand('copy'); t.remove(); }
  async function copyAI() {
    const mod = buildModule();
    if (!mod) return;
    const slim = JSON.parse(JSON.stringify(mod));
    slim.payload.css = slim.payload.css.slice(0, 12000);
    slim.payload.html = slim.payload.html.slice(0, 20000);
    const md = '# 模块模型：' + mod.label + '\n\n- 类别：' + mod.categoryLabel + '\n- 来源：' + (mod.source.group || '') + ' / ' + (mod.source.file || '空白') + '\n- 区域：' + mod.region.area + '\n- 位置：' + JSON.stringify(mod.position.px) + '（占预览 ' + JSON.stringify(mod.position.pct) + '）\n- 方向：' + (mod.layout.orientation || '—') + '\n- 颜色覆盖：' + JSON.stringify(mod.override) + '\n\n```json\n' + JSON.stringify(slim, null, 2) + '\n```\n';
    await copyText(md);
    toast('已复制给 AI（' + kb(md.length) + '，CSS/HTML 已截断）');
  }

  async function chooseDir() {
    if (!window.showDirectoryPicker) { toast('此浏览器不支持目录写入，将改为下载文件'); return; }
    try {
      R.writeHandle = await window.showDirectoryPicker({ id: 'uits-write', mode: 'readwrite' });
      await idbSet('writeDir', R.writeHandle);
      renderDirName();
      toast('写入目录：' + R.writeHandle.name);
    } catch (e) { /* 用户取消 */ }
  }
  function renderDirName() { $('#dirName').textContent = R.writeHandle ? R.writeHandle.name : (window.showDirectoryPicker ? '未选择 · 将下载文件' : '下载模式'); $('#dirName').title = '整体模型 / 模块模型 两个子目录会自动创建'; }

  let toastT;
  function toast(msg) { const t = $('#toast'); t.textContent = msg; t.hidden = false; clearTimeout(toastT); toastT = setTimeout(() => { t.hidden = true; }, 3200); }

  /* ───────── 后台索引：逐个载入模板，收集全部模块作为候选 ───────── */
  function indexOne(t) {
    return new Promise(async res => {
      let text;
      try { text = await getText(t); } catch (e) { res(); return; }
      const f = document.createElement('iframe');
      f.style.cssText = 'position:fixed;left:-20000px;top:0;width:1440px;height:900px;border:0;visibility:hidden;pointer-events:none';
      f.setAttribute('aria-hidden', 'true');
      let done = false;
      const finish = () => { if (done) return; done = true; try { f.remove(); } catch (e) { /* 已移除 */ } res(); };
      f.onload = () => setTimeout(() => {
        try {
          const win = f.contentWindow;
          A.init(win, t.family);
          const recs = [];
          A.enumerate(win).forEach(m => {
            try { const d = A.describe(win, m, t); d.id = t.id + '::' + m.key; d.kind = 'catalog'; d.tplId = t.id; recs.push(d); R.catalog.set(d.id, d); } catch (e) { /* 单个模块失败不影响其它 */ }
          });
          R.byTpl[t.id] = recs;
        } catch (e) { console.warn('index', t.id, e); }
        finish();
      }, 450);
      setTimeout(finish, 15000);
      f.srcdoc = text;
      document.body.appendChild(f);
    });
  }
  async function indexAll() {
    R.indexing = { n: 0, total: TPLS.length };
    renderCandidates();
    for (const t of TPLS) {
      await indexOne(t);
      R.indexing.n++;
      renderCandidates();
    }
    R.indexed = true;
  }

  /* ───────── 事件 ───────── */
  function bind() {
    $('#btnTpl').onclick = () => setView('tpl');
    $('#btnMod').onclick = () => setView('mod');
    $('#btnBlank').onclick = () => openTemplate('blank');
    $('#tplTree').onclick = e => {
      const it = e.target.closest('.item');
      if (it) { openTemplate(it.dataset.id); return; }
      const h = e.target.closest('.grp-h');
      if (h) { const g = h.parentElement; g.classList.toggle('open'); S.open[g.dataset.g] = g.classList.contains('open'); saveState(); }
    };
    $('#modTree').onclick = e => {
      const it = e.target.closest('.item');
      if (it && it.dataset.key) { selectModule(it.dataset.key); return; }
      if (it && it.dataset.mine) { R.cand = it.dataset.mine; renderCandidates(); renderInfo(); renderOps(); return; }
      const h = e.target.closest('.grp-h');
      if (h) { const g = h.parentElement; g.classList.toggle('open'); if (g.dataset.mg) { S.open['m:' + g.dataset.mg] = g.classList.contains('open'); saveState(); } }
    };
    $('#modTree').onmouseover = e => { const it = e.target.closest('.item[data-key]'); const k = it ? it.dataset.key : null; if (k !== R.hover) { R.hover = k; refreshOverlay(); } };
    $('#modTree').onmouseleave = () => { R.hover = null; refreshOverlay(); };
    $('#btnColor').onclick = e => { e.stopPropagation(); const p = $('#colorPop'); p.hidden = !p.hidden; renderColorPop(); };
    document.addEventListener('click', e => { const p = $('#colorPop'); if (!p.hidden && !e.target.closest('.pop-wrap')) p.hidden = true; });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') $('#colorPop').hidden = true; });
    const pop = $('#colorPop');
    pop.addEventListener('change', e => {
      if (e.target.id === 'colEnable') { S.color.enabled = e.target.checked; saveState(); schedulePipeline(); renderOps(); updateColorNote(); }
    });
    pop.addEventListener('input', e => {
      const k = e.target.dataset && e.target.dataset.seed;
      if (k) { S.color.seeds[k] = e.target.value; if (!S.color.enabled) { S.color.enabled = true; const c = $('#colEnable'); if (c) c.checked = true; renderOps(); } saveState(); schedulePipeline(); }
    });
    pop.addEventListener('click', e => {
      const m = e.target.closest('[data-mode]');
      if (m) { setMode(m.dataset.mode); return; }
      const p = e.target.closest('[data-preset]');
      if (p) { S.color.seeds = { ...A.PRESETS.find(x => x.id === p.dataset.preset).seeds }; S.color.enabled = true; saveState(); schedulePipeline(); renderColorPop(); renderOps(); return; }
      if (e.target.id === 'colReset') { S.color = { enabled: false, seeds: { ...A.DEFAULT_SEEDS.dark } }; saveState(); schedulePipeline(); renderColorPop(); renderOps(); }
    });
    $('#btnCompact').onclick = () => { const cur = S.compact === null ? (R.win && R.ready ? A.readDensity(R.win) : true) : S.compact; setCompact(!cur); };
    $('#tbSeg').onclick = e => { const b = e.target.closest('button'); if (b && !b.disabled) setToolbarDir(b.dataset.dir); };
    $('#candBody').onclick = e => { const c = e.target.closest('.card'); if (c) { R.cand = c.dataset.cand; renderCandidates(); renderInfo(); renderOps(); } };
    $('#btnDir').onclick = chooseDir;
    $('#btnSaveWhole').onclick = () => saveWhole();
    const ops = $('#opsBody');
    ops.addEventListener('click', e => {
      const b = e.target.closest('[data-act]');
      if (!b) return;
      const sel = R.byKey[R.sel], tp = T();
      switch (b.dataset.act) {
        case 'replace': if (sel && R.cand) { const rec = getRec(R.cand); tp.repl[sel.key] = R.cand; if (rec && rec.override && !tp.ov[sel.key]) tp.ov[sel.key] = { ...rec.override }; saveState(); runPipeline(); renderAll(); toast('已替换：' + sel.label); } break;
        case 'restore': if (sel) { delete tp.repl[sel.key]; saveState(); runPipeline(); renderAll(); } break;
        case 'restoreAll': tp.repl = {}; saveState(); runPipeline(); renderAll(); break;
        case 'clearOv': if (sel) { delete tp.ov[sel.key]; saveState(); runPipeline(); renderOps(); renderInfo(); } break;
        case 'saveMod': saveModule(); break;
        case 'copyAI': copyAI(); break;
      }
    });
    ops.addEventListener('input', e => {
      const k = e.target.dataset && e.target.dataset.ov, sel = R.byKey[R.sel];
      if (!k || !sel) return;
      const o = T().ov[sel.key] || (T().ov[sel.key] = { surface: 'panel', lift: 0, accent: 'accent' });
      o[k] = k === 'lift' ? +e.target.value : e.target.value;
      const lv = $('[data-liftv]', ops); if (lv) lv.textContent = o.lift + '%';
      saveState(); schedulePipeline();
      const c = $('[data-act="clearOv"]', ops); if (c) c.disabled = false;
    });
    new ResizeObserver(layoutScaler).observe($('#stage'));
    const sp = $('#splitC');
    sp.addEventListener('mousedown', e => {
      e.preventDefault();
      const host = $('#center'), top = $('#midbar').offsetHeight + 5;
      document.body.style.userSelect = 'none';
      $('#frame').style.pointerEvents = 'none';
      const mv = ev => { const r = host.getBoundingClientRect(), h = r.height - top, v = Math.min(0.85, Math.max(0.2, (ev.clientY - r.top - $('#midbar').offsetHeight) / h)); S.splitC = v; applySplit(); };
      const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); document.body.style.userSelect = ''; $('#frame').style.pointerEvents = ''; saveState(); };
      addEventListener('mousemove', mv); addEventListener('mouseup', up);
    });
  }
  function applySplit() { $('#preview').style.flex = S.splitC + ' 1 0'; $('#cands').style.flex = (1 - S.splitC) + ' 1 0'; }

  /* ───────── 启动 ───────── */
  async function init() {
    const man = await (await fetch('templates/manifest.json')).json().catch(() => null);
    if (!man) { document.body.insertAdjacentHTML('beforeend', '<div id="fatal" style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:#16171a">请通过本地 HTTP 打开本页，例如：<code>python -m http.server</code></div>'); return; }
    GROUPS = man.groups;
    GROUPS.forEach(g => g.items.forEach(it => { it.group = g.id; it.url = 'templates/' + encodeURIComponent(g.id) + '/' + encodeURIComponent(it.file); TPLS.push(it); TPL[it.id] = it; }));
    R.writeHandle = (await idbGet('writeDir')) || null;
    R.mine = (await idbGet('mine')) || [];
    bind();
    setView(S.view);
    applySplit();
    renderAll();
    await openTemplate(S.tplId === 'blank' || TPL[S.tplId] ? S.tplId : TPLS[0].id);
    indexAll();
  }

  window.__studio = { S, R, A, T, openTemplate, runPipeline, buildWhole, buildModule, saveWhole, saveModule, selectModule, setMode, setCompact, setToolbarDir, getRec, metaOf, get TPLS() { return TPLS; }, get GROUPS() { return GROUPS; } };
  init();
})();
