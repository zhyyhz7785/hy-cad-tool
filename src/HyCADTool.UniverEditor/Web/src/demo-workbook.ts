import { BooleanNumber, LocaleType, type IWorkbookData } from '@univerjs/core';

export const DEMO_WORKBOOK_DATA: IWorkbookData = {
  id: 'hycad-univer-demo',
  name: 'HyCAD Univer Demo',
  appVersion: '0.25.0',
  locale: LocaleType.ZH_CN,
  sheetOrder: ['sheet-01'],
  styles: {
    blueFill: {
      bg: { rgb: '#4e94ff' },
    },
    greenFill: {
      bg: { rgb: '#67c23a' },
    },
    redText: {
      cl: { rgb: '#ff0000' },
    },
    percentFmt: {
      n: { pattern: '0%' },
    },
    dateFmt: {
      n: { pattern: 'yyyy/mm/dd' },
    },
  },
  sheets: {
    'sheet-01': {
      id: 'sheet-01',
      name: 'Sheet1',
      rowCount: 200,
      columnCount: 26,
      defaultRowHeight: 24,
      defaultColumnWidth: 88,
      showGridlines: BooleanNumber.TRUE,
      freeze: {
        xSplit: 0,
        ySplit: 0,
        startRow: 0,
        startColumn: 0,
      },
      mergeData: [
        {
          startRow: 14,
          endRow: 14,
          startColumn: 0,
          endColumn: 2,
        },
      ],
      cellData: {
        0: {
          0: { v: 'Hello, Univer!' },
        },
        1: {
          0: { v: 'Blue', s: 'blueFill' },
          1: { v: 'Green', s: 'greenFill' },
        },
        2: {
          0: { v: 'Border demo' },
        },
        5: {
          0: { v: 'Red text', s: 'redText' },
        },
        14: {
          0: { v: 'Span merged cells' },
        },
        17: {
          0: { v: 'Univer' },
        },
        21: {
          0: { v: 0.25, s: 'percentFmt' },
        },
        23: {
          0: { v: 44032, s: 'dateFmt' },
        },
      },
    },
  },
};
