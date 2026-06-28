import { applyRibbonTabVisual, RIBBON_DROPDOWN_TAB_CLASS } from './ribbon-tab-style';

export interface HyCadDropdownItem {
  id: string;
  label: string;
  separatorBefore?: boolean;
  onSelect: () => void;
}

const PORTAL_HOST_ID = 'hycad-dropdown-portal-host';
const ROOT_ATTR = 'data-hycad-dropdown-root';

let openRoot: HTMLElement | null = null;

function ensurePortalHost(): HTMLElement {
  let host = document.getElementById(PORTAL_HOST_ID);
  if (!host) {
    host = document.createElement('div');
    host.id = PORTAL_HOST_ID;
    // 不用 inset:0 全屏遮罩，避免关闭后 pointer-events 挡住表格
    host.style.cssText = 'position:fixed;left:0;top:0;z-index:30000;pointer-events:none;width:0;height:0;overflow:visible;';
    document.body.appendChild(host);
  }
  return host;
}

export function closeHyCadDropdown(): void {
  if (!openRoot)
    return;

  const dropdown = openRoot.querySelector('[data-hycad-comp="dropdown-panel"]') as HTMLElement | null;
  const anchor = openRoot.querySelector('[data-hycad-comp="dropdown-button"]') as HTMLElement | null;
  if (dropdown && anchor) {
    dropdown.classList.add('hycad-file-dropdown--hidden');
    dropdown.style.pointerEvents = '';
    openRoot.appendChild(dropdown);
    anchor.setAttribute('aria-expanded', 'false');
    applyRibbonTabVisual(anchor, false, true);
  }

  openRoot.classList.remove('hycad-file-tab-root--open');
  openRoot = null;

  const host = ensurePortalHost();
  host.replaceChildren();
  host.style.pointerEvents = 'none';
}

function openHyCadDropdown(root: HTMLElement, button: HTMLElement, dropdown: HTMLElement): void {
  closeHyCadDropdown();
  openRoot = root;

  const host = ensurePortalHost();
  host.replaceChildren();

  dropdown.classList.remove('hycad-file-dropdown--hidden');
  dropdown.style.pointerEvents = 'auto';
  host.appendChild(dropdown);

  const rect = button.getBoundingClientRect();
  dropdown.style.position = 'fixed';
  dropdown.style.top = `${rect.bottom + 4}px`;
  dropdown.style.left = `${rect.left}px`;
  dropdown.style.right = 'auto';
  dropdown.style.bottom = 'auto';
  dropdown.style.maxHeight = 'min(420px, calc(100vh - 16px))';
  dropdown.style.overflowY = 'auto';

  root.classList.add('hycad-file-tab-root--open');
  button.setAttribute('aria-expanded', 'true');
  applyRibbonTabVisual(button, true, true);
}

export function createHyCadDropdownRoot(
  rootId: string,
  buttonLabel: string,
  items: HyCadDropdownItem[],
  buttonClassName = RIBBON_DROPDOWN_TAB_CLASS,
): HTMLElement {
  const root = document.createElement('div');
  root.setAttribute(ROOT_ATTR, rootId);
  root.className = 'hycad-file-tab-root';

  const button = document.createElement('button');
  button.type = 'button';
  button.setAttribute('data-hycad-comp', 'dropdown-button');
  button.className = buttonClassName;
  button.textContent = buttonLabel;
  applyRibbonTabVisual(button, false, true);
  button.setAttribute('aria-haspopup', 'true');
  button.setAttribute('aria-expanded', 'false');

  const dropdown = document.createElement('div');
  dropdown.setAttribute('data-hycad-comp', 'dropdown-panel');
  dropdown.className = 'hycad-file-dropdown hycad-file-dropdown--hidden';
  dropdown.setAttribute('role', 'menu');

  for (const item of items) {
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
    const runSelect = () => {
      closeHyCadDropdown();
      item.onSelect();
    };

    // mousedown 先于 document capture dismiss，避免菜单项 click 被销毁
    menuItem.addEventListener('mousedown', (event) => {
      event.preventDefault();
      event.stopPropagation();
      runSelect();
    });
    dropdown.appendChild(menuItem);
  }

  button.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    if (openRoot === root)
      closeHyCadDropdown();
    else
      openHyCadDropdown(root, button, dropdown);
  });

  root.appendChild(button);
  root.appendChild(dropdown);
  return root;
}

export function installHyCadDropdownDismiss(): void {
  if (document.documentElement.getAttribute('data-hycad-dropdown-dismiss') === 'true')
    return;

  document.documentElement.setAttribute('data-hycad-dropdown-dismiss', 'true');
  document.addEventListener('click', (event) => {
    if (!openRoot)
      return;

    const target = event.target as HTMLElement | null;
    if (!target)
      return;
    if (target.closest(`[${ROOT_ATTR}]`))
      return;
    if (target.closest(`#${PORTAL_HOST_ID}`))
      return;
    closeHyCadDropdown();
  }, true);

  window.addEventListener('resize', closeHyCadDropdown);
  window.addEventListener('scroll', closeHyCadDropdown, true);
}

export function isInsideHyCadDropdown(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement))
    return false;
  return Boolean(target.closest(`[${ROOT_ATTR}]`) || target.closest(`#${PORTAL_HOST_ID}`));
}
