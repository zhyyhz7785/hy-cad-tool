import type { FUniver } from '@univerjs/core/facade';

import type { HyCadFileAction } from './file-tab-inject';
import { DEMO_WORKBOOK_DATA } from './demo-workbook';
import type { HyCadGridSnapshot } from './univer-bridge';

type HostCommandHandler = (
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  raw: string,
) => void;

const CAD_ONLY_ACTIONS: HyCadFileAction[] = [
  'importXlsx',
  'exportXlsx',
  'exportJson',
  'pick',
  'publish',
  'publishRangeFull',
  'publishRangeContent',
];

const PERSONNEL_FIXTURE_URL = '/fixtures/personnel-snapshot.json';

let statusEl: HTMLDivElement | null = null;

function isWebViewHost(): boolean {
  try {
    return Boolean(window.chrome?.webview?.postMessage);
  } catch {
    return false;
  }
}

function showDevStatus(message: string): void {
  if (!statusEl) {
    statusEl = document.createElement('div');
    statusEl.className = 'hycad-dev-status';
    document.body.appendChild(statusEl);
  }
  statusEl.textContent = `[HyCAD dev] ${message}`;
}

function dispatchHostCommand(
  handleHostCommand: HostCommandHandler,
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  payload: Record<string, unknown>,
): void {
  handleHostCommand(univerAPI, postHostMessage, JSON.stringify(payload));
}

async function loadPersonnelFixture(
  univerAPI: ReturnType<typeof FUniver.newAPI>,
  postHostMessage: (payload: Record<string, unknown>) => void,
  handleHostCommand: HostCommandHandler,
): Promise<void> {
  showDevStatus('加载人员样表…');
  try {
    const response = await fetch(PERSONNEL_FIXTURE_URL);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const snapshot = (await response.json()) as HyCadGridSnapshot;
    dispatchHostCommand(handleHostCommand, univerAPI, postHostMessage, {
      type: 'loadSnapshot',
      payload: snapshot,
    });
    showDevStatus('人员样表已加载');
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'unknown error';
    showDevStatus(`加载失败: ${detail}`);
    postHostMessage({ type: 'error', message: `loadPersonnel fixture failed: ${detail}` });
  }
}

function resetToDemoWorkbook(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  const workbook = univerAPI.getActiveWorkbook();
  const unitId = workbook?.getId?.();
  if (!unitId) {
    showDevStatus('新建空表失败：无活动工作簿');
    return;
  }

  univerAPI.disposeUnit(unitId);
  univerAPI.createWorkbook(DEMO_WORKBOOK_DATA);
  showDevStatus('已重置为 demo 空表');
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
    showDevStatus('浏览器 dev 模式 — WebView2 宿主未连接');
    return;
  }

  if (type === 'cellChanged') {
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
        showDevStatus(`${action} 仅 CAD 宿主可用`);
        console.warn('[HyCAD dev]', action, 'requires AutoCAD WebView2 host');
        break;
      default:
        showDevStatus(`未知文件动作: ${action}`);
        break;
    }
    return;
  }

  if (type === 'hyCadWindowControl') {
    const action = String(payload.action ?? '');
    showDevStatus(`\u7a97\u53e3\u63a7\u5236: ${action}\uff08\u4ec5 CAD \u5bbf\u4e3b\u53ef\u7528\uff09`);
    return;
  }

  if (type === 'hyCadAction') {
    const action = String(payload.action ?? '');
    if (CAD_ONLY_ACTIONS.includes(action as HyCadFileAction)) {
      showDevStatus(`${action} 仅 CAD 宿主可用`);
    } else {
      showDevStatus(`HyCAD 动作: ${action}`);
    }
    return;
  }

  if (type === 'snapshotLoaded') {
    showDevStatus('快照已加载');
    return;
  }

  if (type === 'error') {
    showDevStatus(`错误: ${String(payload.message ?? 'unknown')}`);
  }
}

export interface DevHostOptions {
  univerAPI: ReturnType<typeof FUniver.newAPI>;
  handleHostCommand: HostCommandHandler;
}

export function installDevHostIfNeeded(options: DevHostOptions): (payload: Record<string, unknown>) => void {
  if (isWebViewHost()) {
    return (payload) => {
      try {
        window.chrome?.webview?.postMessage(JSON.stringify(payload));
      } catch {
        // ignore
      }
    };
  }

  const { univerAPI, handleHostCommand } = options;

  const devPostMessage = (payload: Record<string, unknown>): void => {
    handleDevOutboundMessage(univerAPI, devPostMessage, handleHostCommand, payload);
  };

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

  showDevStatus('dev 宿主已启用');
  return devPostMessage;
}

export function postHyCadAction(action: string): void {
  window.chrome?.webview?.postMessage(JSON.stringify({ type: 'hyCadAction', action }));
}
