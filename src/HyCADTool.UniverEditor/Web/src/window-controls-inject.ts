export type HyCadWindowControlAction = 'minimize' | 'maximize' | 'close';

interface WindowControlItem {
  action: HyCadWindowControlAction;
  label: string;
  title: string;
  variant?: 'close';
}

const CONTROL_ITEMS: WindowControlItem[] = [
  { action: 'minimize', label: '\u2014', title: '\u6700\u5c0f\u5316' },
  { action: 'maximize', label: '\u25a1', title: '\u6700\u5927\u5316 / \u8fd8\u539f' },
  { action: 'close', label: '\u2715', title: '\u5173\u95ed', variant: 'close' },
];

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const GROUP_FLAG = 'data-hycad-window-controls';

let teardown: (() => void) | null = null;

function createControlsGroup(
  postHostMessage: (payload: Record<string, unknown>) => void,
): HTMLElement {
  const group = document.createElement('div');
  group.setAttribute(GROUP_FLAG, 'true');
  group.className = 'hycad-window-controls';

  for (const item of CONTROL_ITEMS) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = item.variant === 'close'
      ? 'hycad-window-control hycad-window-control--close'
      : 'hycad-window-control';
    button.textContent = item.label;
    button.title = item.title;
    button.setAttribute('aria-label', item.title);
    button.addEventListener('click', (event) => {
      event.preventDefault();
      event.stopPropagation();
      postHostMessage({ type: 'hyCadWindowControl', action: item.action });
    });
    group.appendChild(button);
  }

  return group;
}

function injectWindowControls(
  postHostMessage: (payload: Record<string, unknown>) => void,
): boolean {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!headerMenu)
    return false;

  if (headerMenu.querySelector(`[${GROUP_FLAG}]`))
    return true;

  const group = createControlsGroup(postHostMessage);
  headerMenu.appendChild(group);
  return true;
}

export function installHyCadWindowControls(
  postHostMessage: (payload: Record<string, unknown>) => void,
): void {
  if (injectWindowControls(postHostMessage))
    return;

  const observer = new MutationObserver(() => {
    if (injectWindowControls(postHostMessage)) {
      observer.disconnect();
      teardown = null;
    }
  });

  observer.observe(document.body, { childList: true, subtree: true });
  teardown = () => observer.disconnect();

  window.setTimeout(() => {
    if (injectWindowControls(postHostMessage)) {
      observer.disconnect();
      teardown = null;
    }
  }, 3000);
}

export function disposeHyCadWindowControls(): void {
  teardown?.();
  teardown = null;
  document.querySelector(`[${GROUP_FLAG}]`)?.remove();
}
