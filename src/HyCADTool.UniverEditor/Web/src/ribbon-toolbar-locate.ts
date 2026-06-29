/** 定位 Univer Ribbon 第二行 toolbar（与 layout-tab / range-menu 注入同源）。 */
export function findRibbonToolbar(): HTMLElement | null {
  const header = document.querySelector('[data-u-comp="ribbon-header-menu"]');
  let node: Element | null = header;
  for (let depth = 0; depth < 8 && node; depth++) {
    const toolbar = node.querySelector?.('[role="toolbar"]');
    if (toolbar instanceof HTMLElement)
      return toolbar;
    node = node.parentElement;
  }

  const toolbars = document.querySelectorAll('[role="toolbar"]');
  for (const toolbar of toolbars) {
    if (toolbar instanceof HTMLElement && toolbar.querySelector('[class*="ribbon"], [data-u-comp]'))
      return toolbar;
  }

  return toolbars.item(0) instanceof HTMLElement ? toolbars.item(0) as HTMLElement : null;
}
