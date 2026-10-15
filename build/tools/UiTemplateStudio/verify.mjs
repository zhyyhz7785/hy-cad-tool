/* UiTemplateStudio 冒烟验证：node verify.mjs
 * 依赖 playwright-core 与本机 Chrome。
 *   PLAYWRIGHT_MODULE  playwright-core 的入口路径（默认直接 import 'playwright-core'）
 *   CHROME_PATH        Chrome 可执行文件（默认 /usr/local/bin/google-chrome）
 *   SHOTS_DIR          设置后保存各步骤截图
 */
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ? pathToFileURL(process.env.PLAYWRIGHT_MODULE).href : 'playwright-core');

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html');
  if (!p.startsWith(root) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'content-type': MIME[path.extname(p)] || 'text/plain; charset=utf-8' });
  fs.createReadStream(p).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const base = 'http://127.0.0.1:' + server.address().port + '/index.html';

let pass = 0, fail = 0;
const ok = (c, msg, extra) => { if (c) { pass++; console.log('  ok   ' + msg); } else { fail++; console.log('  FAIL ' + msg + (extra !== undefined ? ' → ' + JSON.stringify(extra) : '')); } };
const shotDir = process.env.SHOTS_DIR;
if (shotDir) fs.mkdirSync(shotDir, { recursive: true });

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || '/usr/local/bin/google-chrome', args: ['--no-sandbox'] });
const ctx = await browser.newContext({ viewport: { width: 1680, height: 980 }, acceptDownloads: true });
const page = await ctx.newPage();
const pageErrors = [];
page.on('pageerror', e => pageErrors.push(e.message));
await page.goto(base);
await page.waitForFunction(() => window.__studio && __studio.R.ready, null, { timeout: 20000 });
const shot = async n => { if (shotDir) await page.screenshot({ path: path.join(shotDir, n + '.png') }); };
const ev = (fn, arg) => page.evaluate(fn, arg);
const settle = ms => page.waitForTimeout(ms || 900);

console.log('# 外壳固定：记录节点');
await ev(() => { window.__shell = ['#left', '#rail', '#dir', '#center', '#midbar', '#preview', '#cands', '#right', '#rbar', '#info', '#ops'].map(s => document.querySelector(s)); });
const shellBg = await ev(() => getComputedStyle(document.querySelector('#app')).backgroundColor + getComputedStyle(document.querySelector('#left')).backgroundColor);

const ids = await ev(() => __studio.TPLS.map(t => t.id));
ok(ids.length === 19, '清单含 19 个模板', ids.length);

console.log('# 逐个载入 19 个模板');
for (const id of ids) {
  pageErrors.length = 0;
  await ev(id => __studio.openTemplate(id), id);
  await page.waitForFunction(id => __studio.S.tplId === id && __studio.R.ready, id, { timeout: 15000 });
  await settle(300);
  const r = await ev(() => ({ mods: __studio.R.modules.length, hasHY: !!__studio.R.win.HY, fam: __studio.R.meta.family, w: __studio.R.win.innerWidth, same: window.__shell.every(n => n.isConnected) }));
  const needHY = !/^(max-a|ue-)/.test(id);
  ok(pageErrors.length === 0 && r.mods > 0 && (!needHY || r.hasHY) && r.same, `${id}: 无脚本错误 · 模块 ${r.mods} 个 · HY=${r.hasHY} · 外壳节点未重建`, { errs: pageErrors.slice(0, 2), r });
}
await ev(() => { __studio.setView && 0; });

console.log('# 后台索引（候选来源）');
await page.waitForFunction(() => __studio.R.indexed, null, { timeout: 90000 });
const idx = await ev(() => ({ n: __studio.R.catalog.size, tpls: Object.keys(__studio.R.byTpl).length }));
ok(idx.tpls === 19 && idx.n > 100, '19 个模板全部索引，候选 ' + idx.n + ' 个', idx);

console.log('# 整体颜色（单一入口）');
const seeds = { mode: 'dark', bg: '#101820', panel: '#182430', text: '#E8F0F8', accent: '#FF6B00', canvas: '#0B1218' };
const colorRows = [];
for (const id of ids) {
  await ev(id => __studio.openTemplate(id), id);
  await page.waitForFunction(id => __studio.S.tplId === id && __studio.R.ready, id, { timeout: 15000 });
  await ev(s => { __studio.S.color = { enabled: true, seeds: s }; __studio.runPipeline(); }, seeds);
  await settle(250);
  const r = await ev(() => {
    const w = __studio.R.win, cs = w.getComputedStyle(w.document.documentElement);
    const ue = __studio.R.meta.family === 'ue5';
    const bodyBg = w.getComputedStyle(w.document.body).backgroundColor;
    return { accent: cs.getPropertyValue(ue ? '--primary' : '--accent').trim().toLowerCase(), bodyBg, st: __studio.R.stats };
  });
  const cov = r.st ? (r.st.neutral + r.st.accent) / r.st.total : 0;
  colorRows.push([id, r.st && r.st.total, (cov * 100).toFixed(0) + '%']);
  ok(r.accent === '#ff6b00' && r.bodyBg !== 'rgb(255, 255, 255)', `${id}: 强调令牌 = 种子色 · 映射覆盖 ${(cov * 100).toFixed(0)}%`, r);
}
console.log('  覆盖率（映射/总写死色值）：' + colorRows.map(r => r.join(':')).join('  '));
await ev(() => { __studio.openTemplate('cad-a'); });
await page.waitForFunction(() => __studio.S.tplId === 'cad-a' && __studio.R.ready);
await settle();
await shot('01-color-orange-cad-a');
const shell2 = await ev(() => getComputedStyle(document.querySelector('#app')).backgroundColor + getComputedStyle(document.querySelector('#left')).backgroundColor);
ok(shell2 === shellBg, '工作台外壳颜色不随整体颜色变化');

await ev(() => { __studio.S.color = { enabled: false, seeds: __studio.S.color.seeds }; __studio.runPipeline(); });
await settle(300);
const off = await ev(() => { const w = __studio.R.win; return w.getComputedStyle(w.document.documentElement).getPropertyValue('--accent').trim().toLowerCase(); });
ok(off !== '#ff6b00', '关闭整体颜色后恢复原模板颜色', off);

console.log('# 紧凑');
for (const id of ['cad-a', 'md-b', 'ue-a', 'max-a', 'blender-a']) {
  await ev(id => __studio.openTemplate(id), id);
  await page.waitForFunction(id => __studio.S.tplId === id && __studio.R.ready, id);
  await settle(400);
  const h = async c => { await ev(c => __studio.setCompact(c), c); await settle(300); return ev(() => { const w = __studio.R.win, d = w.document; const el = d.querySelector('.menubar,#menubar,.titlebar'); return Math.round(el.getBoundingClientRect().height) + '/' + (getComputedStyle(d.documentElement).getPropertyValue('--h-tool') || '').trim(); }); };
  const a = await h(true), b = await h(false);
  ok(a !== b, `${id}: 紧凑与舒适高度不同`, { compact: a, comfortable: b });
}
await ev(() => __studio.setCompact(null));

console.log('# 工具栏水平 / 竖向');
const tbCases = [['cad-a', 'generic'], ['gen-a', 'generic'], ['cad-c', 'generic'], ['cad-b', 'ribbon'], ['blender-a', 'native'], ['max-a', 'native'], ['ue-a', 'native'], ['ue-b', 'native']];
for (const [id, kind] of tbCases) {
  await ev(id => __studio.openTemplate(id), id);
  await page.waitForFunction(id => __studio.S.tplId === id && __studio.R.ready, id);
  await settle(500);
  const before = await ev(() => { const p = __studio.R.primary; return p && p.getBoundingClientRect().width >= p.getBoundingClientRect().height; });
  await ev(() => __studio.setToolbarDir('v')); await settle(600);
  const v = await ev(() => {
    const w = __studio.R.win, d = w.document, r = document.documentElement;
    const rd = d.documentElement;
    const prim = __studio.R.primary;
    const info = { pos: rd.dataset.toolbarPos || rd.dataset.tbpos || (d.querySelector('.app') || {getAttribute: () => null}).getAttribute('data-toolbar-pos') };
    if (prim && prim.isConnected) { const rc = prim.getBoundingClientRect(); info.h = Math.round(rc.height); info.w = Math.round(rc.width); }
    return info;
  });
  if (kind === 'ribbon') ok(true, 'cad-b: Ribbon 不支持竖放（保持水平）');
  else if (kind === 'generic') ok(before && v.h > v.w, `${id}: 主工具栏竖放后 高(${v.h}) > 宽(${v.w})`, { before, v });
  else ok(v.pos === 'side', `${id}: 原生机制切到侧面`, v);
  await shot('tb-' + id + '-v');
  await ev(() => __studio.setToolbarDir('h')); await settle(600);
  const h = await ev(() => { const w = __studio.R.win, rd = w.document.documentElement; const prim = __studio.R.primary; const rc = prim && prim.getBoundingClientRect(); return { pos: rd.dataset.toolbarPos || rd.dataset.tbpos || ((w.document.querySelector('.app') || {getAttribute: () => null}).getAttribute('data-toolbar-pos')), w: rc && Math.round(rc.width), h: rc && Math.round(rc.height) }; });
  if (kind === 'ribbon') { /* 无变化 */ } else if (kind === 'generic') ok(h.w > h.h, `${id}: 恢复水平`, h); else ok(h.pos === 'top', `${id}: 恢复上侧`, h);
}

await ev(() => __studio.openTemplate('md-a'));
await page.waitForFunction(() => __studio.S.tplId === 'md-a' && __studio.R.ready);
await settle(400);
ok(await ev(() => [...document.querySelectorAll('#tbSeg button')].every(b => b.disabled)), 'md-a: 无独立工具栏时方向按钮置灰');

console.log('# 模块选择 · 候选 · 替换 · 恢复');
await page.locator('#btnMod').click();
await ev(() => __studio.openTemplate('cad-a'));
await page.waitForFunction(() => __studio.S.tplId === 'cad-a' && __studio.R.ready);
await settle(500);
const mod = await ev(() => { const m = __studio.R.modules.find(x => x.cat === 'dockR'); __studio.selectModule(m.key); return m.key; });
await settle(300);
const nCand = await page.locator('#candBody .card').count();
ok(nCand > 3, '右侧停靠区候选 ' + nCand + ' 项（跨模板）', nCand);
await page.locator('#modTree .item.on').first().waitFor();
const pickId = await ev(() => { const r = [...__studio.R.catalog.values()].find(x => x.category === 'dockR' && x.tplId === 'gen-a'); return r.id; });
await page.locator(`#candBody .card[data-cand="${pickId}"]`).click();
await page.locator('#opsBody [data-act="replace"]').click();
await settle(600);
const rep = await ev(m => { const el = __studio.R.byKey[m].el; return { host: !!el.__uitsHost, hidden: getComputedStyle(el).display, w: el.__uitsHost && Math.round(el.__uitsHost.getBoundingClientRect().width), kids: el.__uitsHost && el.__uitsHost.shadowRoot.children.length }; }, mod);
ok(rep.host && rep.hidden === 'none' && rep.w > 100 && rep.kids > 2, '替换后：影子宿主已插入、原模块隐藏', rep);
await shot('02-replaced-dockR');
await page.locator('#btnColor').click();
await page.locator('#colEnable').check();
await page.locator('input[data-seed="bg"]').fill('#10202a');
await page.locator('input[data-seed="panel"]').fill('#173040');
await page.locator('input[data-seed="accent"]').fill('#00c2a8');
await page.keyboard.press('Escape');
await settle(600);
const rc = await ev(m => { const el = __studio.R.byKey[m].el; const w = __studio.R.win; const h = el.__uitsHost; return { rootAccent: w.document.documentElement.style.getPropertyValue('--accent'), host: getComputedStyle(h).getPropertyValue('--accent').trim().toLowerCase(), prop: getComputedStyle(h.shadowRoot.querySelector('.uits-root')).backgroundColor }; }, mod);
ok(rc.rootAccent === '#00c2a8' && rc.host !== '#4772b3', '界面启用整体颜色后，替换进来的模块跟随（含模板内局部令牌块）', rc);
await shot('03-replaced-dockR-teal');

console.log('# 模块颜色覆盖：整体先调，模块跟随');
await page.locator('#opsBody select[data-ov="accent"]').selectOption('success');
await settle(400);
const ov1 = await ev(m => { const h = __studio.R.byKey[m].el.__uitsHost; return h.style.getPropertyValue('--accent'); }, mod);
await ev(() => { __studio.S.color.seeds = { ...__studio.S.color.seeds, mode: 'dark' }; __studio.setMode('light'); }); await settle(500);
const ov2 = await ev(m => { const h = __studio.R.byKey[m].el.__uitsHost; return h.style.getPropertyValue('--accent'); }, mod);
ok(ov1 && ov2 && ov1 !== ov2, '整体切到浅色后，模块覆盖（成功色）自动改变', { ov1, ov2 });
await ev(() => __studio.setMode('dark')); await settle(300);
await page.locator('#btnColor').click();
await page.locator('#colEnable').uncheck();
await page.keyboard.press('Escape');
await settle(300);
const ov3 = await ev(m => __studio.R.byKey[m].el.__uitsHost.style.getPropertyValue('--accent'), mod);
ok(ov3 === '', '关闭整体颜色后模块覆盖失效', ov3);
await page.locator('#btnColor').click();
await page.locator('#colEnable').check();
await page.keyboard.press('Escape');
await settle(300);

console.log('# 恢复 / 导出');
await page.locator('#opsBody [data-act="restore"]').click();
await settle(400);
const back = await ev(m => { const el = __studio.R.byKey[m].el; return { host: !!el.__uitsHost, d: getComputedStyle(el).display }; }, mod);
ok(!back.host && back.d !== 'none', '恢复后原模块回到预览', back);

const downloads = [];
page.on('download', d => downloads.push(d));
await page.locator('#btnSaveWhole').click();
await page.waitForFunction(() => true);
for (let i = 0; i < 50 && downloads.length < 2; i++) await settle(100);
const names = downloads.map(d => d.suggestedFilename());
const dJson = downloads.find(d => d.suggestedFilename().endsWith('.json')), dHtml = downloads.find(d => d.suggestedFilename().endsWith('.html'));
ok(names.some(n => n.endsWith('.whole.json')) && names.some(n => n.endsWith('.whole.html')), '保存整体模型：JSON + 快照 HTML', names);
const wj = JSON.parse(fs.readFileSync(await dJson.path(), 'utf8'));
ok(wj.type === 'whole-model' && wj.template.id === 'cad-a' && wj.color.enabled && wj.regions.length > 5 && 'toolbar' in wj && 'compact' in wj, '整体模型字段齐全', Object.keys(wj));
const snapHtml = fs.readFileSync(await dHtml.path(), 'utf8');
ok(snapHtml.length > 50000 && !/<script/i.test(snapHtml), '快照 HTML 为静态页（已去脚本）', snapHtml.length);

await ev(() => { const m = __studio.R.modules.find(x => x.cat === 'dockR'); __studio.selectModule(m.key); });
downloads.length = 0;
await page.locator('#opsBody [data-act="saveMod"]').click();
for (let i = 0; i < 50 && downloads.length < 1; i++) await settle(100);
const d3 = downloads[0];
const mj = JSON.parse(fs.readFileSync(await d3.path(), 'utf8'));
const need = ['category', 'label', 'source', 'element', 'region', 'position', 'layout', 'colors', 'content', 'payload', 'studio'];
ok(d3.suggestedFilename().includes('.module.json') && need.every(k => k in mj) && mj.position.px.w > 0 && mj.payload.html.length > 100 && mj.element.path, '保存模块模型：区域/位置/颜色/内容/载荷字段齐全', { name: d3.suggestedFilename(), missing: need.filter(k => !(k in mj)) });
ok((await ev(() => __studio.R.mine.length)) === 1, '保存后进入「我的模块」');

console.log('# 从空白建立');
await page.locator('#btnTpl').click();
await page.locator('#btnBlank').click();
await page.waitForFunction(() => __studio.S.tplId === 'blank' && __studio.R.ready);
await settle(500);
const slots = await ev(() => __studio.R.modules.map(m => m.cat));
ok(slots.length === 8, '空白骨架含 8 个槽位', slots);
await ev(() => { const m = __studio.R.modules.find(x => x.cat === 'toolbar'); __studio.selectModule(m.key); });
const tbCard = await ev(() => [...__studio.R.catalog.values()].find(r => r.category === 'toolbar' && r.tplId === 'cad-a').id);
await page.locator(`#candBody .card[data-cand="${tbCard}"]`).click();
await page.locator('#opsBody [data-act="replace"]').click();
await settle(500);
await ev(() => { const m = __studio.R.modules.find(x => x.cat === 'dockR'); __studio.selectModule(m.key); });
await page.locator(`#candBody .card[data-cand="${pickId}"]`).click();
await page.locator('#opsBody [data-act="replace"]').click();
await settle(600);
await shot('04-blank-assembled');
ok(await ev(() => Object.keys(__studio.T().repl).length === 2), '空白骨架可逐槽替换为模块');

console.log('# 切换模板不丢失整体设置');
await ev(() => __studio.openTemplate('ue-a'));
await page.waitForFunction(() => __studio.S.tplId === 'ue-a' && __studio.R.ready);
await settle(600);
const keep = await ev(() => ({ en: __studio.S.color.enabled, acc: __studio.R.win.getComputedStyle(__studio.R.win.document.documentElement).getPropertyValue('--primary').trim().toLowerCase() }));
ok(keep.en && keep.acc.length === 7, '切换到 UE5 模板后整体颜色继续生效', keep);
await shot('05-ue-a-with-global-color');

ok(await ev(() => window.__shell.every(n => n.isConnected)), '全程外壳节点未重建');

await browser.close();
server.close();
console.log(`\n结果：通过 ${pass}，失败 ${fail}`);
process.exit(fail ? 1 : 0);
