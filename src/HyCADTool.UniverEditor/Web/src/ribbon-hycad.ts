import type { FUniver } from '@univerjs/core/facade';

import { postHyCadAction } from './dev-host';

export function registerHyCadRibbonMenus(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  const publish = univerAPI.createMenu({
    id: 'hycad-menu-publish',
    title: '落图',
    action: () => postHyCadAction('publish'),
  });
  publish.appendTo('ribbon.start.others');

  const pick = univerAPI.createMenu({
    id: 'hycad-menu-pick',
    title: '拾取',
    action: () => postHyCadAction('pick'),
  });
  pick.appendTo('ribbon.start.others');

  univerAPI
    .createSubmenu({ id: 'hycad-submenu', title: 'HyCAD' })
    .addSubmenu(publish)
    .addSubmenu(pick)
    .appendTo('ribbon.start.others');
}
