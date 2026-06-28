export interface DevPanelOptions {
  onLoadPersonnel: () => void;
  onNewEmpty: () => void;
  onExportSnapshot: () => void;
  dispatchHostMessage: (raw: string) => void;
}

const UI_CHECKLIST = [
  '最左：文件▾（下拉 9 项）',
  '居中：开始 | 公式 | 数据',
  '数据后：布局 Tab（第二行 Ribbon 面板）',
  '布局后：主题▾（Univer + HyCAD 面板主题）',
  '最右：— □ ✕ 窗口控制（仅 CAD 宿主生效）',
  '开始 Tab：落图范围▾（工具栏左侧）',
];

let panelEl: HTMLDivElement | null = null;
let selectionEl: HTMLSpanElement | null = null;
let actionEl: HTMLSpanElement | null = null;

function colLabel(col: number): string {
  let value = col + 1;
  let letters = '';
  while (value > 0) {
    const remainder = (value - 1) % 26;
    letters = String.fromCharCode(65 + remainder) + letters;
    value = Math.floor((value - 1) / 26);
  }
  return letters;
}

function formatSelection(startRow: number, startCol: number, endRow: number, endCol: number): string {
  const r1 = startRow + 1;
  const c1 = colLabel(startCol);
  if (startRow === endRow && startCol === endCol)
    return `${c1}${r1}`;
  return `${c1}${r1}:${colLabel(endCol)}${endRow + 1}`;
}

export function showDevAction(message: string): void {
  if (actionEl)
    actionEl.textContent = message;
}

export function showDevSelection(startRow: number, startCol: number, endRow: number, endCol: number): void {
  if (selectionEl)
    selectionEl.textContent = formatSelection(startRow, startCol, endRow, endCol);
}

export function installDevPanel(options: DevPanelOptions): void {
  if (panelEl)
    return;

  const panel = document.createElement('div');
  panel.className = 'hycad-dev-panel';
  panel.innerHTML = `
    <header class="hycad-dev-panel-header">
      <span class="hycad-dev-panel-title">HyCAD Univer Dev</span>
      <span class="hycad-dev-panel-hint">5174 · mock 宿主</span>
      <button type="button" class="hycad-dev-panel-toggle" title="折叠/展开">▾</button>
    </header>
    <div class="hycad-dev-panel-body">
      <div class="hycad-dev-panel-row">
        <button type="button" data-action="personnel">人员样表</button>
        <button type="button" data-action="newEmpty">新建空表</button>
        <button type="button" data-action="export">导出快照</button>
        <button type="button" data-action="viewport">回填布局初值</button>
      </div>
      <div class="hycad-dev-panel-meta">
        <span>选区 <strong data-dev-selection>—</strong></span>
        <span>状态 <strong data-dev-action>就绪</strong></span>
      </div>
      <details class="hycad-dev-checklist" open>
        <summary>Tab 行 / Ribbon 验收</summary>
        <ul></ul>
      </details>
    </div>
  `;

  document.body.appendChild(panel);
  panelEl = panel;
  panel.classList.add('hycad-dev-panel--collapsed');
  selectionEl = panel.querySelector('[data-dev-selection]');
  actionEl = panel.querySelector('[data-dev-action]');

  const list = panel.querySelector('.hycad-dev-checklist ul');
  if (list) {
    for (const item of UI_CHECKLIST) {
      const li = document.createElement('li');
      li.textContent = item;
      list.appendChild(li);
    }
  }

  const body = panel.querySelector('.hycad-dev-panel-body') as HTMLElement | null;
  const toggle = panel.querySelector('.hycad-dev-panel-toggle') as HTMLButtonElement | null;
  if (toggle)
    toggle.textContent = '▸';
  toggle?.addEventListener('click', () => {
    const collapsed = panel.classList.toggle('hycad-dev-panel--collapsed');
    if (toggle)
      toggle.textContent = collapsed ? '▸' : '▾';
  });

  panel.querySelector('[data-action="personnel"]')?.addEventListener('click', () => {
    options.onLoadPersonnel();
  });
  panel.querySelector('[data-action="newEmpty"]')?.addEventListener('click', () => {
    options.onNewEmpty();
  });
  panel.querySelector('[data-action="export"]')?.addEventListener('click', () => {
    options.onExportSnapshot();
  });
  panel.querySelector('[data-action="viewport"]')?.addEventListener('click', () => {
    options.dispatchHostMessage(JSON.stringify({
      type: 'setViewport',
      payload: {
        paperPresetIndex: 1,
        orientation: 0,
        targetWidthMm: 400,
        marginMm: 10,
        rowCount: 5,
        colCount: 4,
        templateIndex: 0,
        structureMode: true,
      },
    }));
    showDevAction('已发送 setViewport（布局 Tab 应回填）');
  });

  if (body)
    body.addEventListener('click', (event) => event.stopPropagation());
}

export function pushMockViewportOnReady(dispatchHostMessage: (raw: string) => void): void {
  window.setTimeout(() => {
    dispatchHostMessage(JSON.stringify({
      type: 'setViewport',
      payload: {
        paperPresetIndex: 1,
        orientation: 0,
        targetWidthMm: 400,
        marginMm: 10,
        rowCount: 5,
        colCount: 4,
        templateIndex: 0,
        structureMode: true,
      },
    }));
    dispatchHostMessage(JSON.stringify({
      type: 'setScale',
      payload: { scale: 40 },
    }));
  }, 400);
}
