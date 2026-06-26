import type { FUniver } from '@univerjs/core/facade';

import { postHyCadAction } from './dev-host';

/** Univer createMenu：落图 / 拾取（范围落图见 ribbon-range-inject 自定义 portal 下拉） */
export function registerHyCadRibbonMenus(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  univerAPI
    .createMenu({
      id: 'hycad-ribbon-publish',
      title: '落图',
      action: () => postHyCadAction('publish'),
    })
    .appendTo('ribbon.start.others');

  univerAPI
    .createMenu({
      id: 'hycad-ribbon-pick',
      title: '拾取',
      action: () => postHyCadAction('pick'),
    })
    .appendTo('ribbon.start.others');
}
