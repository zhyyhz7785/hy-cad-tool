export type HyCadFileAction =
  | 'newEmpty'
  | 'loadPersonnel'
  | 'importXlsx'
  | 'exportXlsx'
  | 'exportJson'
  | 'pick'
  | 'publish';

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
];

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const FILE_ROOT_ATTR = 'data-hycad-comp';
const FILE_ROOT_VALUE = 'file-tab-root';
const INJECTED_FLAG = 'data-hycad-injected';

let dropdownOpen = false;
let teardown: (() => void) | null = null;

function closeDropdown(): void {
  dropdownOpen = false;
  document.querySelectorAll(`[${FILE_ROOT_ATTR}="${FILE_ROOT_VALUE}"]`).forEach((root) => {
    root.classList.remove('hycad-file-tab-root--open');
    const btn = root.querySelector('[data-hycad-comp="file-tab"]');
    btn?.classList.remove('hycad-file-tab--active');
    root.querySelector('[data-hycad-comp="file-dropdown"]')?.classList.add('hycad-file-dropdown--hidden');
  });
}

function toggleDropdown(root: HTMLElement, button: HTMLButtonElement): void {
  const dropdown = root.querySelector('[data-hycad-comp="file-dropdown"]');
  if (!dropdown)
    return;

  dropdownOpen = !dropdownOpen;
  if (dropdownOpen) {
    root.classList.add('hycad-file-tab-root--open');
    button.classList.add('hycad-file-tab--active');
    dropdown.classList.remove('hycad-file-dropdown--hidden');
  } else {
    closeDropdown();
  }
}

function createFileTabRoot(
  postHostMessage: (payload: Record<string, unknown>) => void,
): HTMLElement {
  const root = document.createElement('div');
  root.setAttribute(FILE_ROOT_ATTR, FILE_ROOT_VALUE);
  root.className = 'hycad-file-tab-root';

  const button = document.createElement('button');
  button.type = 'button';
  button.setAttribute('data-hycad-comp', 'file-tab');
  button.className = 'hycad-file-tab';
  button.textContent = '文件';
  button.title = 'HyCAD 文件';
  button.setAttribute('aria-haspopup', 'true');
  button.setAttribute('aria-expanded', 'false');

  const dropdown = document.createElement('div');
  dropdown.setAttribute('data-hycad-comp', 'file-dropdown');
  dropdown.className = 'hycad-file-dropdown hycad-file-dropdown--hidden';
  dropdown.setAttribute('role', 'menu');

  for (const item of FILE_MENU_ITEMS) {
    if (item.separatorBefore) {
      const sep = document.createElement('div');
      sep.className = 'hycad-file-dropdown-separator';
      sep.setAttribute('role', 'separator');
      dropdown.appendChild(sep);
    }

    const menuItem = document.createElement('button');
    menuItem.type = 'button';
    menuItem.className = 'hycad-file-dropdown-item';
    menuItem.textContent = item.label;
    menuItem.setAttribute('role', 'menuitem');
    menuItem.addEventListener('click', (event) => {
      event.stopPropagation();
      closeDropdown();
      postHostMessage({ type: 'hyCadFileAction', action: item.action });
    });
    dropdown.appendChild(menuItem);
  }

  button.addEventListener('click', (event) => {
    event.stopPropagation();
    toggleDropdown(root, button);
  });

  root.appendChild(button);
  root.appendChild(dropdown);
  return root;
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
    if (!target)
      return;
    if (target.closest(`[${FILE_ROOT_ATTR}="${FILE_ROOT_VALUE}"]`))
      return;
    if (target.closest('[role="tab"]'))
      closeDropdown();
  });

  document.addEventListener('click', (event) => {
    const target = event.target as HTMLElement | null;
    if (!target?.closest(`[${FILE_ROOT_ATTR}="${FILE_ROOT_VALUE}"]`))
      closeDropdown();
  });

  return true;
}

export function installHyCadFileTab(
  postHostMessage: (payload: Record<string, unknown>) => void,
): void {
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
  closeDropdown();
  teardown?.();
  teardown = null;
  document.querySelector(`[${FILE_ROOT_ATTR}="${FILE_ROOT_VALUE}"]`)?.remove();
  document.querySelector(ROOT_SELECTOR)?.removeAttribute(INJECTED_FLAG);
}
