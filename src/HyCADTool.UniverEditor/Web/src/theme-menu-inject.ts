import { ThemeService } from '@univerjs/core';
import type { FUniver } from '@univerjs/core/facade';
import { defaultTheme, greenTheme, type Theme } from '@univerjs/themes';

import { createHyCadDropdownRoot, installHyCadDropdownDismiss } from './hycad-dropdown';

type UniverApi = ReturnType<typeof FUniver.newAPI>;

const ROOT_SELECTOR = '[data-u-comp="ribbon-header-menu"]';
const THEME_ROOT_VALUE = 'theme-tab-root';
const INJECTED_FLAG = 'data-hycad-theme-injected';

/** Blender 蓝（取自 HyCAD.BlenderUI BlenderDark/Light：inner_sel=#4772B3, hover=#5680C2），扩成 50~900 色阶 */
const BLENDER_BLUE = {
  50: '#EEF3FA',
  100: '#D6E2F2',
  200: '#B0C7E5',
  300: '#89ABD8',
  400: '#6690C9',
  500: '#5680C2',
  600: '#4772B3',
  700: '#3C619A',
  800: '#324F7E',
  900: '#233A5C',
};

/** Blender 中性灰（取自 BlenderDark：text #E6E6E6 / panel #303030 / header #181818），50(亮)→900(暗) */
const BLENDER_GRAY = {
  50: '#F2F2F2',
  100: '#E6E6E6',
  200: '#C3C3C3',
  300: '#A0A0A0',
  400: '#838383',
  500: '#6B6B6B',
  600: '#545454',
  700: '#3D3D3D',
  800: '#303030',
  900: '#181818',
};

const HY_LIGHT_THEME: Theme = { ...defaultTheme, primary: { ...BLENDER_BLUE } };
const HY_DARK_THEME: Theme = { ...defaultTheme, primary: { ...BLENDER_BLUE }, gray: { ...BLENDER_GRAY } };

interface ThemeMenuItem {
  id: string;
  label: string;
  theme: Theme;
  dark: boolean;
  separatorBefore?: boolean;
}

const THEME_ITEMS: ThemeMenuItem[] = [
  { id: 'univer-light', label: 'Univer 默认（浅色）', theme: defaultTheme, dark: false },
  { id: 'univer-dark', label: 'Univer 深色', theme: defaultTheme, dark: true },
  { id: 'univer-green', label: 'Univer 绿色', theme: greenTheme, dark: false },
  { id: 'hy-light', label: 'HyCAD 面板（浅色）', theme: HY_LIGHT_THEME, dark: false, separatorBefore: true },
  { id: 'hy-dark', label: 'HyCAD 面板（深色）', theme: HY_DARK_THEME, dark: true },
];

function applyTheme(univerAPI: UniverApi, theme: Theme, dark: boolean): void {
  try {
    const injector = (univerAPI as unknown as { _injector?: { get?: (token: unknown) => unknown } })._injector;
    const themeService = injector?.get?.(ThemeService) as ThemeService | undefined;
    themeService?.setTheme?.(theme as Parameters<ThemeService['setTheme']>[0]);
  } catch (error) {
    console.error('[HyCAD] setTheme failed', error);
  }

  try {
    univerAPI.toggleDarkMode(dark);
  } catch (error) {
    console.error('[HyCAD] toggleDarkMode failed', error);
  }
}

function injectThemeTab(univerAPI: UniverApi): boolean {
  const headerMenu = document.querySelector(ROOT_SELECTOR);
  if (!headerMenu)
    return false;
  if (headerMenu.getAttribute(INJECTED_FLAG) === 'true')
    return true;

  const tablist = headerMenu.querySelector('[role="tablist"]');
  if (!tablist)
    return false;

  const root = createHyCadDropdownRoot(
    THEME_ROOT_VALUE,
    '主题',
    THEME_ITEMS.map((item) => ({
      id: item.id,
      label: item.label,
      separatorBefore: item.separatorBefore,
      onSelect: () => applyTheme(univerAPI, item.theme, item.dark),
    })),
  );

  const tabs = tablist.querySelectorAll('[role="tab"]');
  const lastTab = tabs.item(tabs.length - 1);
  if (lastTab && lastTab.parentElement === tablist)
    tablist.insertBefore(root, lastTab.nextSibling);
  else
    tablist.appendChild(root);

  headerMenu.setAttribute(INJECTED_FLAG, 'true');
  return true;
}

export function installHyCadThemeTab(univerAPI: UniverApi): void {
  installHyCadDropdownDismiss();

  if (injectThemeTab(univerAPI))
    return;

  const observer = new MutationObserver(() => {
    if (injectThemeTab(univerAPI))
      observer.disconnect();
  });

  observer.observe(document.body, { childList: true, subtree: true });

  window.setTimeout(() => {
    if (injectThemeTab(univerAPI))
      observer.disconnect();
  }, 3000);
}

export function disposeHyCadThemeTab(): void {
  document.querySelector(`[data-hycad-dropdown-root="${THEME_ROOT_VALUE}"]`)?.remove();
  document.querySelector(ROOT_SELECTOR)?.removeAttribute(INJECTED_FLAG);
}
