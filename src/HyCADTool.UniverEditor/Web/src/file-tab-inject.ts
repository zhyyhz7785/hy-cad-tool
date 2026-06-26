import {
  closeHyCadDropdown,
  createHyCadDropdownRoot,
  installHyCadDropdownDismiss,
  isInsideHyCadDropdown,
} from './hycad-dropdown';

export type HyCadFileAction =
  | 'newEmpty'
  | 'loadPersonnel'
  | 'importXlsx'
  | 'exportXlsx'
  | 'exportJson'
  | 'pick'
  | 'publish'
  | 'publishRangeFull'
  | 'publishRangeContent';

interface FileMenuItem {
  action: HyCadFileAction;
  label: string;
  separatorBefore?: boolean;
}

const FILE_MENU_ITEMS: FileMenuItem[] = [
  { action: 'newEmpty', label: '新建空表' },
  { action: 'loadPersonnel', label: '加载人员样表' },
  { action: 'importXlsx', label: '导入 xlsx...', separatorBefore: true },
  { action: 'exportXlsx', label: '导出 xlsx...' },
  { action: 'exportJson', label: '导出 JSON 快照...' },
  { action: 'pick', label: '拾取 HyTable', separatorBefore: true },
  { action: 'publish', label: '落图到 CAD' },
  { action: 'publishRangeFull', label: '落图范围：整个选区' },
  { action: 'publishRangeContent', label: '落图范围：选区内仅有内容' },
];

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const FILE_ROOT_VALUE = 'file-tab-root';
const INJECTED_FLAG = 'data-hycad-injected';

let teardown: (() => void) | null = null;

function createFileTabRoot(
  postHostMessage: (payload: Record<string, unknown>) => void,
): HTMLElement {
  return createHyCadDropdownRoot(
    FILE_ROOT_VALUE,
    '文件',
    FILE_MENU_ITEMS.map((item) => ({
      id: item.action,
      label: item.label,
      separatorBefore: item.separatorBefore,
      onSelect: () => postHostMessage({ type: 'hyCadFileAction', action: item.action }),
    })),
  );
}

function injectFileTab(postHostMessage: (payload: Record<string, unknown>) => void): boolean {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!headerMenu || headerMenu.getAttribute(INJECTED_FLAG) === 'true')
    return headerMenu?.getAttribute(INJECTED_FLAG) === 'true';

  const tablist = headerMenu.querySelector('[role="tablist"]');
  if (!tablist)
    return false;

  const root = createFileTabRoot(postHostMessage);
  tablist.insertBefore(root, tablist.firstChild);
  headerMenu.setAttribute(INJECTED_FLAG, 'true');

  tablist.addEventListener('click', (event) => {
    const target = event.target as HTMLElement | null;
    if (!target || isInsideHyCadDropdown(target))
      return;
    if (target.closest('[role="tab"]'))
      closeHyCadDropdown();
  });

  return true;
}

export function installHyCadFileTab(
  postHostMessage: (payload: Record<string, unknown>) => void,
): void {
  installHyCadDropdownDismiss();

  if (injectFileTab(postHostMessage))
    return;

  const observer = new MutationObserver(() => {
    if (injectFileTab(postHostMessage)) {
      observer.disconnect();
      teardown = null;
    }
  });

  observer.observe(document.body, { childList: true, subtree: true });
  teardown = () => observer.disconnect();

  window.setTimeout(() => {
    if (injectFileTab(postHostMessage)) {
      observer.disconnect();
      teardown = null;
    }
  }, 3000);
}

export function disposeHyCadFileTab(): void {
  closeHyCadDropdown();
  teardown?.();
  teardown = null;
  document.querySelector(`[data-hycad-dropdown-root="${FILE_ROOT_VALUE}"]`)?.remove();
  document.querySelector(ROOT_SELECTOR)?.removeAttribute(INJECTED_FLAG);
}
