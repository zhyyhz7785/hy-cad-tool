import { getLastSnapshotDims } from './univer-bridge';

/**
 * 布局 Tab（口径 B）。
 *
 * DOM 工具条保底实现：Univer 0.25 OSS 是否能注册原生顶层 Tab 未证，
 * 这里用一个固定浮动工具条承载「行高/列宽 mm」「比例」「行列/合并」「落图/拾取/角色」。
 *
 * 全部尺寸为纸面 mm（与 AutoCAD 落图对齐）；只有 CAD 落图按比例 ×Scale 放大。
 */

type PostHostMessage = (payload: Record<string, unknown>) => void;

const PANEL_ID = 'hycad-layout-tab';

let post: PostHostMessage | null = null;
let suppressInput = false;

let rowHeightInput: HTMLInputElement | null = null;
let colWidthInput: HTMLInputElement | null = null;
let scaleInput: HTMLInputElement | null = null;

function sendLayoutOp(op: string, value = 0): void {
  post?.({ type: 'hyCadLayout', op, value });
}

function sendFileAction(action: string): void {
  post?.({ type: 'hyCadFileAction', action });
}

function num(input: HTMLInputElement | null, fallback: number): number {
  if (!input)
    return fallback;
  const v = Number(input.value);
  return Number.isFinite(v) && v > 0 ? v : fallback;
}

function makeButton(label: string, title: string, onClick: () => void): HTMLButtonElement {
  const btn = document.createElement('button');
  btn.type = 'button';
  btn.textContent = label;
  btn.title = title;
  btn.className = 'hycad-layout-btn';
  btn.addEventListener('click', (e) => {
    e.preventDefault();
    onClick();
  });
  return btn;
}

function makeNumberInput(width: number): HTMLInputElement {
  const input = document.createElement('input');
  input.type = 'number';
  input.min = '0';
  input.step = '1';
  input.className = 'hycad-layout-num';
  input.style.width = `${width}px`;
  return input;
}

function makeLabel(text: string): HTMLSpanElement {
  const span = document.createElement('span');
  span.textContent = text;
  span.className = 'hycad-layout-label';
  return span;
}

function makeSeparator(): HTMLSpanElement {
  const sep = document.createElement('span');
  sep.className = 'hycad-layout-sep';
  return sep;
}

function buildPanel(): HTMLElement {
  const panel = document.createElement('div');
  panel.id = PANEL_ID;
  panel.className = 'hycad-layout-tab';

  const title = document.createElement('span');
  title.textContent = '布局';
  title.className = 'hycad-layout-title';
  panel.appendChild(title);

  // 行高 / 列宽（纸面 mm）
  panel.appendChild(makeLabel('行高'));
  rowHeightInput = makeNumberInput(56);
  rowHeightInput.title = '当前选区行高（纸面 mm）';
  rowHeightInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setRowHeight', num(rowHeightInput, 10));
  });
  panel.appendChild(rowHeightInput);

  panel.appendChild(makeLabel('列宽'));
  colWidthInput = makeNumberInput(56);
  colWidthInput.title = '当前选区列宽（纸面 mm）';
  colWidthInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setColWidth', num(colWidthInput, 25));
  });
  panel.appendChild(colWidthInput);

  panel.appendChild(makeSeparator());

  // 行列 / 合并
  panel.appendChild(makeButton('+行', '在选区下方插入行', () => sendLayoutOp('insertRow')));
  panel.appendChild(makeButton('-行', '删除选中行', () => sendLayoutOp('deleteRow')));
  panel.appendChild(makeButton('+列', '在选区右侧插入列', () => sendLayoutOp('insertCol')));
  panel.appendChild(makeButton('-列', '删除选中列', () => sendLayoutOp('deleteCol')));
  panel.appendChild(makeButton('合并', '合并选区', () => sendLayoutOp('merge')));
  panel.appendChild(makeButton('拆分', '拆分合并区', () => sendLayoutOp('unmerge')));

  panel.appendChild(makeSeparator());

  // 比例（默认取 hy 面板 Scale；可改，不持久化）
  panel.appendChild(makeLabel('比例'));
  scaleInput = makeNumberInput(56);
  scaleInput.title = '落图比例（纸面 mm × 比例 = 模型 mm）；重开恢复 hy 面板值';
  scaleInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setScale', num(scaleInput, 1));
  });
  panel.appendChild(scaleInput);

  panel.appendChild(makeSeparator());

  // HyCAD 命令归位
  panel.appendChild(makeButton('落图', '落图到 AutoCAD（按比例放大）', () => sendFileAction('publish')));
  panel.appendChild(makeButton('拾取', '从图面拾取 HyTable', () => sendFileAction('pick')));
  panel.appendChild(makeButton('角色', '单元格角色（R6，后期开放）', () => {
    post?.({ type: 'error', message: 'R6 角色功能后期开放' });
  }));

  return panel;
}

function ensureStyles(): void {
  if (document.getElementById('hycad-layout-tab-style'))
    return;
  const style = document.createElement('style');
  style.id = 'hycad-layout-tab-style';
  style.textContent = `
.hycad-layout-tab {
  position: fixed;
  right: 12px;
  bottom: 12px;
  z-index: 9000;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 10px;
  background: var(--hycad-layout-bg, #2b2b2b);
  color: var(--hycad-layout-fg, #eee);
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 6px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.35);
  font-size: 12px;
  user-select: none;
}
.hycad-layout-title { font-weight: 600; margin-right: 6px; opacity: 0.85; }
.hycad-layout-label { opacity: 0.8; }
.hycad-layout-num {
  background: #1e1e1e; color: #eee; border: 1px solid #555;
  border-radius: 3px; padding: 2px 4px; font-size: 12px;
}
.hycad-layout-btn {
  background: #3a3a3a; color: #eee; border: 1px solid #555;
  border-radius: 3px; padding: 2px 8px; font-size: 12px; cursor: pointer;
}
.hycad-layout-btn:hover { background: #4a4a4a; }
.hycad-layout-sep {
  width: 1px; align-self: stretch; margin: 0 4px;
  background: rgba(255,255,255,0.15);
}
`;
  document.head.appendChild(style);
}

/** 安装布局 Tab 工具条（幂等）。 */
export function installHyCadLayoutTab(postHostMessage: PostHostMessage): void {
  post = postHostMessage;
  if (document.getElementById(PANEL_ID))
    return;

  ensureStyles();
  const panel = buildPanel();
  document.body.appendChild(panel);
}

/** 由 main.ts 在 SelectionChanged 时调用：刷新行高/列宽 mm 框为当前选区真值。 */
export function updateLayoutTabSelection(
  startRow: number,
  startCol: number,
  endRow: number,
  endCol: number,
): void {
  const dims = getLastSnapshotDims();
  if (!dims)
    return;

  const r = Math.min(startRow, endRow);
  const c = Math.min(startCol, endCol);

  suppressInput = true;
  if (rowHeightInput && dims.rowHeightsMm.length > r && r >= 0)
    rowHeightInput.value = formatMm(dims.rowHeightsMm[r]);
  if (colWidthInput && dims.colWidthsMm.length > c && c >= 0)
    colWidthInput.value = formatMm(dims.colWidthsMm[c]);
  suppressInput = false;
}

/** 由 main.ts 在收到宿主 setScale 时调用：回填比例框。 */
export function updateLayoutTabScale(scale: number): void {
  if (!scaleInput || !(scale > 0))
    return;
  suppressInput = true;
  scaleInput.value = formatMm(scale);
  suppressInput = false;
}

function formatMm(value: number): string {
  if (!Number.isFinite(value))
    return '';
  return Math.round(value * 100) / 100 + '';
}
