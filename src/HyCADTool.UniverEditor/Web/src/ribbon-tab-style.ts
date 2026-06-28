/** 与 Univer ClassicMenu tab 同款 class（随 .dark 切换，不用 prefers-color-scheme）。 */

const RIBBON_TAB_BASE = [
  'univer-focus:outline-none',
  'univer-focus:ring-2',
  'univer-focus:ring-primary-500',
  'dark:!univer-focus:ring-primary-300',
  'univer-flex',
  'univer-cursor-pointer',
  'univer-appearance-none',
  'univer-items-center',
  'univer-gap-1',
  'univer-rounded-sm',
  'univer-border-none',
  'univer-px-2',
  'univer-py-1',
  'univer-text-sm',
  'univer-transition-colors',
].join(' ');

export const RIBBON_TAB_INACTIVE = [
  RIBBON_TAB_BASE,
  'univer-hover:bg-gray-100',
  'dark:!univer-hover:bg-gray-700',
  'univer-bg-transparent',
  'univer-text-gray-700',
  'dark:!univer-text-gray-200',
].join(' ');

export const RIBBON_TAB_ACTIVE = [
  RIBBON_TAB_BASE,
  'univer-bg-primary-50',
  'univer-font-semibold',
  'univer-text-primary-600',
  'univer-shadow-sm',
  'dark:!univer-bg-primary-900',
  'dark:!univer-text-primary-300',
].join(' ');

/** 带 ▾ 的下拉型 Tab（文件 / 主题）在 inactive class 上叠加。 */
export const RIBBON_DROPDOWN_TAB_CLASS = `${RIBBON_TAB_INACTIVE} hycad-ribbon-dropdown-tab`;

export function applyRibbonTabVisual(button: HTMLElement, active: boolean, dropdownTab = false): void {
  if (active) {
    button.className = dropdownTab
      ? `${RIBBON_TAB_ACTIVE} hycad-ribbon-dropdown-tab`
      : RIBBON_TAB_ACTIVE;
  } else {
    button.className = dropdownTab ? RIBBON_DROPDOWN_TAB_CLASS : RIBBON_TAB_INACTIVE;
  }
  button.setAttribute('aria-selected', active ? 'true' : 'false');
}
