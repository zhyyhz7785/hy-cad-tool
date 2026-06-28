import { LocaleType, mergeLocales, Univer, UniverInstanceType } from '@univerjs/core';

import { FUniver } from '@univerjs/core/facade';

import DesignZhCN from '@univerjs/design/locale/zh-CN';

import { UniverDocsPlugin } from '@univerjs/docs';

import { UniverDocsUIPlugin } from '@univerjs/docs-ui';

import DocsUIZhCN from '@univerjs/docs-ui/locale/zh-CN';

import { UniverFormulaEnginePlugin } from '@univerjs/engine-formula';

import { UniverRenderEnginePlugin } from '@univerjs/engine-render';

import { UniverSheetsPlugin } from '@univerjs/sheets';

import SheetsZhCN from '@univerjs/sheets/locale/zh-CN';

import { UniverSheetsFormulaPlugin } from '@univerjs/sheets-formula';

import { UniverSheetsFormulaUIPlugin } from '@univerjs/sheets-formula-ui';

import SheetsFormulaUIZhCN from '@univerjs/sheets-formula-ui/locale/zh-CN';

import { UniverSheetsNumfmtPlugin } from '@univerjs/sheets-numfmt';

import { UniverSheetsNumfmtUIPlugin } from '@univerjs/sheets-numfmt-ui';

import SheetsNumfmtUIZhCN from '@univerjs/sheets-numfmt-ui/locale/zh-CN';

import { UniverSheetsUIPlugin } from '@univerjs/sheets-ui';

import SheetsUIZhCN from '@univerjs/sheets-ui/locale/zh-CN';

import { UniverUIPlugin } from '@univerjs/ui';

import UIZhCN from '@univerjs/ui/locale/zh-CN';



import '@univerjs/design/lib/index.css';

import '@univerjs/ui/lib/index.css';

import '@univerjs/docs-ui/lib/index.css';

import '@univerjs/sheets-ui/lib/index.css';

import '@univerjs/sheets-formula-ui/lib/index.css';

import '@univerjs/sheets-numfmt-ui/lib/index.css';



import '@univerjs/engine-formula/facade';

import '@univerjs/ui/facade';

import '@univerjs/docs-ui/facade';

import '@univerjs/sheets/facade';

import '@univerjs/sheets-ui/facade';

import '@univerjs/sheets-formula/facade';

import '@univerjs/sheets-numfmt/facade';



import { DEMO_WORKBOOK_DATA } from './demo-workbook';

import { installDevHostIfNeeded } from './dev-host';

import { installHyCadFileTab } from './file-tab-inject';

import {
  installHyCadLayoutTab,
  updateLayoutTabScale,
  updateLayoutTabSelection,
  updateLayoutTabStructureMode,
  updateLayoutTabViewport,
  type HyCadViewportPayload,
} from './layout-tab-inject';

import { installHyCadRibbonRangeMenu } from './ribbon-range-inject';

import { installHyCadWindowControls } from './window-controls-inject';

import { installHyCadThemeTab } from './theme-menu-inject';

import {

  configureHyCadAutoFitPost,

  handleHostCommand,

  installHyCadBridge,

} from './univer-bridge';

import { installFormulaBarLayout } from './formula-bar-layout';
import { installSheetBarGuard } from './sheet-bar-guard';
import { installRulerOverlay } from './ruler-overlay';
import { registerPaperBoundaryExtension } from './paper-extension';
import { installPageViewport } from './page-viewport';
import { installHeadersToggle } from './sheet-headers';
import { subscribeLayoutViewState } from './layout-view-state';

import './global.css';

declare global {

  interface Window {

    univer?: Univer;

    univerAPI?: ReturnType<typeof FUniver.newAPI>;

  }

}



function handleLayoutHostCommand(raw: string): void {

  try {

    const message = JSON.parse(raw) as {
      type?: string;
      payload?: HyCadViewportPayload & { scale?: number; on?: boolean };
    };

    if (message.type === 'setScale') {

      const scale = message.payload?.scale;

      if (typeof scale === 'number')
        updateLayoutTabScale(scale);

    }

    if (message.type === 'setViewport') {

      const payload = message.payload;
      if (payload)
        updateLayoutTabViewport(payload);

    }

    if (message.type === 'setStructureMode') {

      const on = message.payload?.on ?? message.payload?.structureMode;
      if (typeof on === 'boolean')
        updateLayoutTabStructureMode(on);

    }

  } catch {

    // ignore malformed host messages

  }

}



function installViewportResizeFix(): void {
  let lastW = 0;
  let lastH = 0;
  let timer: number | undefined;

  const notify = (): void => {
    const w = window.innerWidth;
    const h = window.innerHeight;
    if (w === lastW && h === lastH)
      return;
    lastW = w;
    lastH = h;

    window.clearTimeout(timer);
    timer = window.setTimeout(() => {
      window.dispatchEvent(new Event('resize'));
    }, 120);
  };

  window.addEventListener('resize', notify);
  window.setTimeout(notify, 300);
}

function bootstrap(): void {

  const univer = new Univer({

    locale: LocaleType.ZH_CN,

    locales: {

      [LocaleType.ZH_CN]: mergeLocales(

        DesignZhCN,

        UIZhCN,

        DocsUIZhCN,

        SheetsZhCN,

        SheetsUIZhCN,

        SheetsFormulaUIZhCN,

        SheetsNumfmtUIZhCN,

      ),

    },

  });



  univer.registerPlugin(UniverRenderEnginePlugin);

  univer.registerPlugin(UniverFormulaEnginePlugin);

  univer.registerPlugin(UniverUIPlugin, {

    container: 'app',

    footer: true,

    header: true,

    toolbar: true,

  });

  univer.registerPlugin(UniverDocsPlugin);

  univer.registerPlugin(UniverDocsUIPlugin);

  univer.registerPlugin(UniverSheetsPlugin);

  univer.registerPlugin(UniverSheetsUIPlugin, {

    footer: {

      sheetBar: true,

      statisticBar: true,

      menus: true,

      zoomSlider: true,

    },

  });

  univer.registerPlugin(UniverSheetsFormulaPlugin);

  univer.registerPlugin(UniverSheetsFormulaUIPlugin);

  univer.registerPlugin(UniverSheetsNumfmtPlugin);

  univer.registerPlugin(UniverSheetsNumfmtUIPlugin);



  univer.createUnit(UniverInstanceType.UNIVER_SHEET, DEMO_WORKBOOK_DATA);



  const univerAPI = FUniver.newAPI(univer);

  window.univer = univer;

  window.univerAPI = univerAPI;



  const postHostMessage = installDevHostIfNeeded({

    univerAPI,

    handleHostCommand,

  });



  installHyCadBridge(univerAPI);
  configureHyCadAutoFitPost(postHostMessage);

  // 落图/拾取已迁入布局 Tab（installHyCadLayoutTab）；此处仅保留范围落图下拉。
  installHyCadRibbonRangeMenu();



  const sheetValueChanged = univerAPI.Event?.SheetValueChanged;

  if (sheetValueChanged) {

    univerAPI.addEvent(sheetValueChanged, (params: { row?: number; column?: number; value?: unknown }) => {

      const row = params.row ?? 0;

      const col = params.column ?? 0;

      const text = String(params.value ?? '');

      postHostMessage({ type: 'cellChanged', row, col, text });

    });

  }



  const selectionChanged = univerAPI.Event?.SelectionChanged;

  if (selectionChanged) {

    univerAPI.addEvent(selectionChanged, (params: { selections?: Array<{ startRow: number; startColumn: number; endRow: number; endColumn: number }> }) => {

      const ranges = params.selections;

      if (!ranges || ranges.length === 0)
        return;

      const range = ranges[ranges.length - 1];

      const startRow = range.startRow ?? 0;
      const startCol = range.startColumn ?? 0;
      const endRow = range.endRow ?? startRow;
      const endCol = range.endColumn ?? startCol;

      postHostMessage({ type: 'selectionChanged', startRow, startCol, endRow, endCol });

      updateLayoutTabSelection(startRow, startCol, endRow, endCol);

    });

  }



  window.chrome?.webview?.addEventListener?.('message', (event: MessageEvent<string>) => {

    handleLayoutHostCommand(event.data);

    handleHostCommand(univerAPI, postHostMessage, event.data);

  });



  installHyCadFileTab(postHostMessage);

  installHyCadLayoutTab(postHostMessage);

  installHyCadThemeTab(univerAPI);

  installHyCadWindowControls(postHostMessage);

  installViewportResizeFix();

  installFormulaBarLayout();
  installSheetBarGuard();

  installRulerOverlay(univerAPI);
  registerPaperBoundaryExtension(univerAPI);
  installPageViewport(univerAPI);
  installHeadersToggle(univerAPI, (listener) => {
    return subscribeLayoutViewState((s) => listener(s.showHeaders));
  });

  postHostMessage({ type: 'ready' });

}



bootstrap();

