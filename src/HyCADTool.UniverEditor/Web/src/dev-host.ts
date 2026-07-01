import type { FUniver } from '@univerjs/core/facade';

import type { HyCadFileAction } from './file-tab-inject';
import { installDevPanel, pushMockViewportOnReady, showDevAction, showDevSelection } from './dev-panel';
import { DEMO_WORKBOOK_DATA } from './demo-workbook';
import type { HyCadGridSnapshot } from './univer-bridge';

type HostCommandHandler = (
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  raw: string,
) => void;

const CAD_ONLY_FILE_ACTIONS: HyCadFileAction[] = [
  'importXlsx',
  'exportXlsx',
  'exportJson',
  'pick',
  'publish',
  'publishRangeFull',
  'publishRangeContent',
];

const CAD_ONLY_LAYOUT_OPS = new Set([
  'setRowHeight',
  'setColWidth',
  'insertRow',
  'deleteRow',
  'insertCol',
  'deleteCol',
  'merge',
  'unmerge',
  'setTargetWidth',
  'setMargin',
  'setRowCount',
  'setColCount',
  'fitColumnsToPaper',
]);

const PERSONNEL_FIXTURE_URL = '/fixtures/personnel-snapshot.json';

let devMode = false;

function isWebViewHost(): boolean {
  if (devMode)
    return false;

  try {
    return Boolean(window.chrome?.webview?.postMessage);
  } catch {
    return false;
  }
}

function dispatchHostCommand(
  handleHostCommand: HostCommandHandler,
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  payload: Record<string, unknown>,
): void {
  handleHostCommand(univerAPI, postHostMessage, JSON.stringify(payload));
}

function dispatchDevHostMessage(raw: string): void {
  window.dispatchEvent(new CustomEvent('hycad-dev-host-message', { detail: raw }));
}

async function loadPersonnelFixture(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  handleHostCommand: HostCommandHandler,
): Promise<void> {
  showDevAction('加载人员样表…');
  try {
    const response = await fetch(PERSONNEL_FIXTURE_URL);
    if (!response.ok)
      throw new Error(`HTTP ${response.status}`);

    const snapshot = (await response.json()) as HyCadGridSnapshot;
    dispatchHostCommand(handleHostCommand, univerAPI, postHostMessage, {
      type: 'loadSnapshot',
      payload: snapshot,
    });
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'unknown error';
    showDevAction(`加载失败: ${detail}`);
    postHostMessage({ type: 'error', message: `loadPersonnel fixture failed: ${detail}` });
  }
}

function resetToDemoWorkbook(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  const workbook = univerAPI.getActiveWorkbook();
  const unitId = workbook?.getId?.();
  if (!unitId) {
    showDevAction('新建空表失败：无活动工作簿');
    return;
  }

  univerAPI.disposeUnit(unitId);
  univerAPI.createWorkbook(DEMO_WORKBOOK_DATA);
  showDevAction('已重置为 demo 空表');
}

function exportSnapshotToConsole(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  handleHostCommand: HostCommandHandler,
): void {
  dispatchHostCommand(handleHostCommand, univerAPI, postHostMessage, { type: 'exportSnapshot' });
}

function handleHyCadLayoutDev(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  handleHostCommand: HostCommandHandler,
  op: string,
  value: number,
): void {
  switch (op) {
    case 'newTable':
      resetToDemoWorkbook(univerAPI);
      return;
    case 'loadTemplate':
      if (Math.round(value) === 1)
        void loadPersonnelFixture(univerAPI, postHostMessage, handleHostCommand);
      else if (Math.round(value) === 0)
        resetToDemoWorkbook(univerAPI);
      else
        showDevAction('家庭模板 fixture 未提供');
      return;
    case 'setScale':
      dispatchDevHostMessage(JSON.stringify({ type: 'setScale', payload: { scale: value } }));
      showDevAction(`Scale = ${value}（仅回填布局框，落图需 CAD）`);
      return;
    case 'setStructureMode':
      dispatchDevHostMessage(JSON.stringify({
        type: 'setStructureMode',
        payload: { on: value >= 0.5 },
      }));
      showDevAction(`结构模式 ${value >= 0.5 ? '开' : '关'}`);
      return;
    case 'setPaperPreset':
      // CAD 宿主会据此回算纸张尺寸并回发 setViewport；dev mock 直接回显预设索引，
      // 让布局视图（纸张盒子 + 网格铺满）能在浏览器里切换、验证网格锁定纸张。
      dispatchDevHostMessage(JSON.stringify({
        type: 'setViewport',
        payload: { paperPresetIndex: Math.round(value) },
      }));
      showDevAction(`纸张预设 = ${Math.round(value)}（dev 本地预览）`);
      return;
    case 'setOrientation':
      dispatchDevHostMessage(JSON.stringify({
        type: 'setViewport',
        payload: { orientation: value >= 0.5 ? 1 : 0 },
      }));
      showDevAction(`方向 = ${value >= 0.5 ? '纵向' : '横向'}（dev 本地预览）`);
      return;
    default:
      if (CAD_ONLY_LAYOUT_OPS.has(op)) {
        showDevAction(`${op} 仅 CAD 宿主可用`);
        console.warn('[HyCAD dev]', op, 'requires AutoCAD WebView2 host');
      } else {
        showDevAction(`布局 op: ${op}`);
      }
  }
}

function handleDevOutboundMessage(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  handleHostCommand: HostCommandHandler,
  payload: Record<string, unknown>,
): void {
  const type = String(payload.type ?? '');

  console.info('[HyCAD dev host]', payload);

  if (type === 'ready') {
    showDevAction('浏览器 dev — WebView2 未连接');
    pushMockViewportOnReady(dispatchDevHostMessage);
    return;
  }

  if (type === 'cellChanged')
    return;

  if (type === 'selectionChanged') {
    const startRow = Number(payload.startRow ?? 0);
    const startCol = Number(payload.startCol ?? 0);
    const endRow = Number(payload.endRow ?? startRow);
    const endCol = Number(payload.endCol ?? startCol);
    showDevSelection(startRow, startCol, endRow, endCol);
    return;
  }

  if (type === 'hyCadFileAction') {
    const action = String(payload.action ?? '') as HyCadFileAction;
    switch (action) {
      case 'newEmpty':
        resetToDemoWorkbook(univerAPI);
        break;
      case 'loadPersonnel':
        void loadPersonnelFixture(univerAPI, postHostMessage, handleHostCommand);
        break;
      case 'importXlsx':
      case 'exportXlsx':
      case 'exportJson':
      case 'pick':
      case 'publish':
      case 'publishRangeFull':
      case 'publishRangeContent':
        showDevAction(`${action} 仅 CAD 宿主可用`);
        console.warn('[HyCAD dev]', action, 'requires AutoCAD WebView2 host');
        break;
      default:
        showDevAction(`未知文件动作: ${action}`);
        break;
    }
    return;
  }

  if (type === 'hyCadLayout') {
    const op = String(payload.op ?? '');
    const value = Number(payload.value ?? 0);
    handleHyCadLayoutDev(univerAPI, postHostMessage, handleHostCommand, op, value);
    return;
  }

  if (type === 'hyCadTrackSizes') {
    const axis = String(payload.axis ?? '');
    const start = Number(payload.start ?? 0);
    const sizes = payload.sizesMm as number[] | undefined;
    const count = sizes?.length ?? 0;
    showDevAction(`内容驱动 ${axis} @${start} ×${count}（CAD 才写回 Domain）`);
    return;
  }

  if (type === 'hyCadWindowControl') {
    showDevAction(`窗口 ${String(payload.action ?? '')}（仅 CAD 宿主）`);
    return;
  }

  if (type === 'hyCadAction') {
    const action = String(payload.action ?? '');
    if (CAD_ONLY_FILE_ACTIONS.includes(action as HyCadFileAction))
      showDevAction(`${action} 仅 CAD 宿主可用`);
    else
      showDevAction(`HyCAD 动作: ${action}`);
    return;
  }

  if (type === 'snapshotLoaded') {
    showDevAction('快照已加载');
    return;
  }

  if (type === 'snapshot') {
    const snap = payload.payload as HyCadGridSnapshot | null | undefined;
    const cells = snap?.cells?.length ?? 0;
    console.info('[HyCAD dev] exportSnapshot', snap);
    showDevAction(`快照导出 ${cells} cells → Console`);
    return;
  }

  if (type === 'error')
    showDevAction(`错误: ${String(payload.message ?? 'unknown')}`);
}

export interface DevHostOptions {
  univerAPI: ReturnType<typeof FUniver.newAPI>;
  handleHostCommand: HostCommandHandler;
}

export function installDevHostIfNeeded(options: DevHostOptions): (payload: Record<string, unknown>) => void {
  const realHost = (() => {
    try {
      return Boolean(window.chrome?.webview?.postMessage);
    } catch {
      return false;
    }
  })();

  if (realHost && !(window as unknown as { __hycadDevForceMock?: boolean }).__hycadDevForceMock) {
    return (payload) => {
      try {
        window.chrome?.webview?.postMessage(JSON.stringify(payload));
      } catch {
        // ignore
      }
    };
  }

  devMode = true;
  const { univerAPI, handleHostCommand } = options;

  const devPostMessage = (payload: Record<string, unknown>): void => {
    handleDevOutboundMessage(univerAPI, devPostMessage, handleHostCommand, payload);
  };

  installDevPanel({
    onLoadPersonnel: () => void loadPersonnelFixture(univerAPI, devPostMessage, handleHostCommand),
    onNewEmpty: () => resetToDemoWorkbook(univerAPI),
    onExportSnapshot: () => exportSnapshotToConsole(univerAPI, devPostMessage, handleHostCommand),
    dispatchHostMessage: dispatchDevHostMessage,
  });

  const mockWebView = {
    postMessage: (raw: string): void => {
      try {
        const payload = JSON.parse(raw) as Record<string, unknown>;
        handleDevOutboundMessage(univerAPI, devPostMessage, handleHostCommand, payload);
      } catch (error) {
        console.error('[HyCAD dev host] invalid postMessage', error);
      }
    },
    addEventListener: (
      event: string,
      listener: (event: MessageEvent<string>) => void,
    ): void => {
      if (event !== 'message')
        return;

      window.addEventListener('hycad-dev-host-message', ((customEvent: CustomEvent<string>) => {
        listener({ data: customEvent.detail } as MessageEvent<string>);
      }) as EventListener);
    },
  };

  window.chrome = window.chrome ?? {};
  window.chrome.webview = mockWebView;

  showDevAction('dev 宿主已启用');
  return devPostMessage;
}

export function postHyCadAction(action: string): void {
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'hyCadAction', action }));
}
