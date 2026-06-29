import { createHyCadDropdownRoot, installHyCadDropdownDismiss } from './hycad-dropdown';
import { postHyCadAction } from './dev-host';
import { findRibbonToolbar } from './ribbon-toolbar-locate';

const ROOT_ID = 'ribbon-range-root';
const INJECTED_FLAG = 'data-hycad-range-injected';

function injectRangeDropdown(): boolean {
  const toolbar = findRibbonToolbar();
  if (!toolbar || toolbar.getAttribute(INJECTED_FLAG) === 'true')
    return toolbar?.getAttribute(INJECTED_FLAG) === 'true';

  const root = createHyCadDropdownRoot(
    ROOT_ID,
    '落图范围',
    [
      { id: 'full', label: '整个选区', onSelect: () => postHyCadAction('publishRangeFull') },
      { id: 'content', label: '选区内仅有内容', separatorBefore: false, onSelect: () => postHyCadAction('publishRangeContent') },
    ],
    'hycad-ribbon-menu-btn',
  );

  toolbar.insertBefore(root, toolbar.firstChild);
  toolbar.setAttribute(INJECTED_FLAG, 'true');

  return true;
}

export function installHyCadRibbonRangeMenu(): void {
  installHyCadDropdownDismiss();

  if (injectRangeDropdown())
    return;

  const observer = new MutationObserver(() => {
    if (injectRangeDropdown())
      observer.disconnect();
  });

  observer.observe(document.body, { childList: true, subtree: true });
  window.setTimeout(() => {
    if (injectRangeDropdown())
      observer.disconnect();
  }, 3000);
}
