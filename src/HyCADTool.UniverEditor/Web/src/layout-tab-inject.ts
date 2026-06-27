import { getLastSnapshotDims } from './univer-bridge';

/**
 * 布局 Tab（口径 B）。
 *
 * 注入到 Univer Ribbon 顶栏：「数据」Tab 右侧，工具条显示在 Ribbon 第二行。
 */

type PostHostMessage = (payload: Record<string, unknown>) => void;

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const LAYOUT_TAB_ROOT_ATTR = 'data-hycad-layout-tab-root';
const LAYOUT_PANEL_ID = 'hycad-layout-ribbon-panel';
const INJECTED_FLAG = 'data-hycad-layout-injected';

let post: PostHostMessage | null = null;
let suppressInput = false;
let layoutActive = false;

let layoutTabButton: HTMLButtonElement | null = null;
let layoutRibbonPanel: HTMLElement | null = null;
let univerToolbarRow: HTMLElement | null = null;

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


function makeGroup(caption: string, ...children: HTMLElement[]): HTMLDivElement {
  const group = document.createElement('div');
  group.className = 'hycad-layout-group';
  const row = document.createElement('div');
  row.className = 'hycad-layout-group-row';
  for (const child of children)
    row.appendChild(child);
  const cap = document.createElement('span');
  cap.className = 'hycad-layout-group-caption';
  cap.textContent = caption;
  group.appendChild(row);
  group.appendChild(cap);
  return group;
}

function buildRibbonPanel(): HTMLElement {
  const panel = document.createElement('div');
  panel.id = LAYOUT_PANEL_ID;
  panel.className = 'hycad-layout-ribbon-panel';
  panel.setAttribute('data-hycad-comp', 'layout-ribbon-panel');

  rowHeightInput = makeNumberInput(52);
  rowHeightInput.title = '当前选区行高（纸面 mm）';
  rowHeightInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setRowHeight', num(rowHeightInput, 10));
  });

  colWidthInput = makeNumberInput(52);
  colWidthInput.title = '当前选区列宽（纸面 mm）';
  colWidthInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setColWidth', num(colWidthInput, 25));
  });

  panel.appendChild(makeGroup(
    '单元格大小 mm',
    makeLabel('行高'),
    rowHeightInput,
    makeLabel('列宽'),
    colWidthInput,
  ));

  panel.appendChild(makeGroup(
    '单元格',
    makeButton('+行', '在选区下方插入行', () => sendLayoutOp('insertRow')),
    makeButton('-行', '删除选中行', () => sendLayoutOp('deleteRow')),
    makeButton('+列', '在选区右侧插入列', () => sendLayoutOp('insertCol')),
    makeButton('-列', '删除选中列', () => sendLayoutOp('deleteCol')),
  ));

  panel.appendChild(makeGroup(
    '合并',
    makeButton('合并', '合并选区', () => sendLayoutOp('merge')),
    makeButton('拆分', '拆分合并区', () => sendLayoutOp('unmerge')),
  ));

  scaleInput = makeNumberInput(52);
  scaleInput.title = '落图比例（纸面 mm × 比例 = 模型 mm）；重开恢复 hy 面板值';
  scaleInput.addEventListener('change', () => {
    if (suppressInput)
      return;
    sendLayoutOp('setScale', num(scaleInput, 1));
  });

  panel.appendChild(makeGroup(
    '比例',
    makeLabel('Scale'),
    scaleInput,
  ));

  panel.appendChild(makeGroup(
    'HyCAD',
    makeButton('落图', '落图到 AutoCAD（按比例放大）', () => sendFileAction('publish')),
    makeButton('拾取', '从图面拾取 HyTable', () => sendFileAction('pick')),
    makeButton('角色', '单元格角色（R6，后期开放）', () => {
      post?.({ type: 'error', message: 'R6 角色功能后期开放' });
    }),
  ));

  return panel;
}

function findTabByLabel(tablist: Element, label: string): HTMLElement | null {
  for (const tab of tablist.querySelectorAll('[role="tab"]')) {
    if (tab instanceof HTMLElement && tab.textContent?.trim() === label)
      return tab;
  }
  return null;
}

function findUniverToolbarRow(headerMenu: HTMLElement): HTMLElement | null {
  const parent = headerMenu.parentElement;
  if (!parent)
    return null;

  const siblings = Array.from(parent.children);
  const index = siblings.indexOf(headerMenu);
  if (index < 0 || index + 1 >= siblings.length)
    return null;

  const next = siblings[index + 1];
  return next instanceof HTMLElement ? next : null;
}

function setLayoutTabSelected(selected: boolean): void {
  layoutTabButton?.setAttribute('aria-selected', selected ? 'true' : 'false');
  layoutTabButton?.classList.toggle('hycad-layout-ribbon-tab--active', selected);
}

function activateLayoutTab(): void {
  if (!layoutRibbonPanel || !univerToolbarRow)
    return;

  layoutActive = true;
  setLayoutTabSelected(true);

  const tablist = layoutTabButton?.closest('[role="tablist"]');
  tablist?.querySelectorAll('[role="tab"]').forEach((tab) => {
    if (tab !== layoutTabButton)
      tab.setAttribute('aria-selected', 'false');
  });

  univerToolbarRow.hidden = true;
  layoutRibbonPanel.classList.add('hycad-layout-ribbon-panel--active');
}

function deactivateLayoutTab(): void {
  if (!layoutActive)
    return;

  layoutActive = false;
  setLayoutTabSelected(false);

  if (univerToolbarRow)
    univerToolbarRow.hidden = false;
  layoutRibbonPanel?.classList.remove('hycad-layout-ribbon-panel--active');
}

function createLayoutTabRoot(): HTMLElement {
  const root = document.createElement('div');
  root.className = 'hycad-layout-tab-root';
  root.setAttribute(LAYOUT_TAB_ROOT_ATTR, 'true');

  const button = document.createElement('button');
  button.type = 'button';
  button.role = 'tab';
  button.textContent = '布局';
  button.title = '行高列宽 mm / 比例 / 行列合并 / 落图拾取';
  button.className = 'hycad-layout-ribbon-tab';
  button.setAttribute('aria-selected', 'false');
  button.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    activateLayoutTab();
  });

  layoutTabButton = button;
  root.appendChild(button);
  return root;
}

function injectLayoutTab(): boolean {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!headerMenu || !(headerMenu instanceof HTMLElement))
    return false;
  if (headerMenu.getAttribute(INJECTED_FLAG) === 'true')
    return true;

  const tablist = headerMenu.querySelector('[role="tablist"]');
  if (!tablist)
    return false;

  const toolbarRow = findUniverToolbarRow(headerMenu);
  if (!toolbarRow)
    return false;

  univerToolbarRow = toolbarRow;
  layoutRibbonPanel = buildRibbonPanel();
  toolbarRow.insertAdjacentElement('afterend', layoutRibbonPanel);

  const dataTab = findTabByLabel(tablist, '数据');
  const tabRoot = createLayoutTabRoot();
  if (dataTab?.parentElement === tablist)
    tablist.insertBefore(tabRoot, dataTab.nextSibling);
  else
    tablist.appendChild(tabRoot);

  tablist.addEventListener('click', (event) => {
    if (!layoutActive)
      return;
    const target = event.target as HTMLElement | null;
    if (!target || target.closest(`[${LAYOUT_TAB_ROOT_ATTR}]`))
      return;
    deactivateLayoutTab();
  });

  headerMenu.setAttribute(INJECTED_FLAG, 'true');
  return true;
}

/** 安装布局 Tab（幂等）。 */
export function installHyCadLayoutTab(postHostMessage: PostHostMessage): void {
  post = postHostMessage;
  if (document.getElementById(LAYOUT_PANEL_ID))
    return;

  if (injectLayoutTab())
    return;

  const observer = new MutationObserver(() => {
    if (injectLayoutTab())
      observer.disconnect();
  });

  observer.observe(document.body, { childList: true, subtree: true });

  window.setTimeout(() => {
    if (injectLayoutTab())
      observer.disconnect();
  }, 3000);
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
