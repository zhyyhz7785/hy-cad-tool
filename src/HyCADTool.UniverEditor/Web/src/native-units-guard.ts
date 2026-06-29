/**
 * 隐藏/中和 Univer 原生 px/pt 尺寸入口，引导用户使用 HyCAD mm 控件。
 */
const ROW_COL_MENU_RE = /行高|列宽|row\s*height|column\s*width|行\s*高|列\s*宽/i;

function hideMenuItems(root: ParentNode): void {
  for (const item of root.querySelectorAll('[role="menuitem"], [role="menuitemradio"], button')) {
    if (!(item instanceof HTMLElement))
      continue;
    const text = item.textContent?.trim() ?? '';
    const title = item.getAttribute('title') ?? '';
    if (ROW_COL_MENU_RE.test(text) || ROW_COL_MENU_RE.test(title))
      item.classList.add('hycad-hidden-native-px-control');
  }
}

function patchOpenMenus(): void {
  for (const menu of document.querySelectorAll('[role="menu"], [data-u-comp*="menu"]'))
    hideMenuItems(menu);
}

/** 隐藏行列头右键「行高/列宽(px)」菜单项。 */
export function installNativeRowColMmGuard(): void {
  document.documentElement.classList.add('hycad-mm-units');

  const observer = new MutationObserver(() => patchOpenMenus());
  observer.observe(document.body, { childList: true, subtree: true });

  document.addEventListener('contextmenu', () => {
    window.setTimeout(patchOpenMenus, 0);
    window.setTimeout(patchOpenMenus, 80);
  }, true);
}
