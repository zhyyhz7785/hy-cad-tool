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

import { registerHyCadRibbonMenus } from './ribbon-hycad';

import {

  handleHostCommand,

  installHyCadBridge,

} from './univer-bridge';

import './global.css';



declare global {

  interface Window {

    univer?: Univer;

    univerAPI?: ReturnType<typeof FUniver.newAPI>;

  }

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

  });

  univer.registerPlugin(UniverDocsPlugin);

  univer.registerPlugin(UniverDocsUIPlugin);

  univer.registerPlugin(UniverSheetsPlugin);

  univer.registerPlugin(UniverSheetsUIPlugin);

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

  registerHyCadRibbonMenus(univerAPI);



  const sheetValueChanged = univerAPI.Event?.SheetValueChanged;

  if (sheetValueChanged) {

    univerAPI.addEvent(sheetValueChanged, (params: { row?: number; column?: number; value?: unknown }) => {

      const row = params.row ?? 0;

      const col = params.column ?? 0;

      const text = String(params.value ?? '');

      postHostMessage({ type: 'cellChanged', row, col, text });

    });

  }



  window.chrome?.webview?.addEventListener?.('message', (event: MessageEvent<string>) => {

    handleHostCommand(univerAPI, postHostMessage, event.data);

  });



  installHyCadFileTab(postHostMessage);

  postHostMessage({ type: 'ready' });

}



bootstrap();

