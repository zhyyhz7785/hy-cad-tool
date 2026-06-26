import { createHyCadDropdownRoot, installHyCadDropdownDismiss } from './hycad-dropdown';
import { postHyCadAction } from './dev-host';

const ROOT_ID = 'ribbon-range-root';
const INJECTED_FLAG = 'data-hycad-range-injected';

function findRibbonToolbar(): HTMLElement | null {
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

  // #region agent log
  fetch('http://127.0.0.1:7417/ingest/b62f39af-ebab-4e9a-8fab-1bee8b67a4ba', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Debug-Session-Id': '646873' }, body: JSON.stringify({ sessionId: '646873', hypothesisId: 'UI-RIBBON-INJECT', location: 'ribbon-range-inject.inject', message: 'range dropdown injected', data: { toolbarClass: toolbar.className }, timestamp: Date.now(), runId: 'post-fix-2' }) }).catch(() => {});
  // #endregion

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
