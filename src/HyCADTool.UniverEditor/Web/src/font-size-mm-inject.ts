import { createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import type { FUniver } from '@univerjs/core/facade';

import { FontSizeMmPanel } from './font-size-mm-panel';
import { findRibbonToolbar } from './ribbon-toolbar-locate';

const HOST_CLASS = 'hycad-font-size-mm-host';
const HOST_FLAG = 'data-hycad-font-size-mm-host';
const NATIVE_PT_SIZE_RE = /^(9|10|11|12|14|16|18|20|22|24|26|28|36|48|72)$/;

let panelRoot: Root | null = null;
let panelHost: HTMLElement | null = null;
let univerAPIRef: ReturnType<typeof FUniver.newAPI> | null = null;
let loggedInject = false;

function hideElement(el: Element | null | undefined): void {
  if (el instanceof HTMLElement)
    el.classList.add('hycad-hidden-native-pt-control');
}

function findFontSizeControlAncestor(node: Element): HTMLElement | null {
  let current: Element | null = node;
  for (let depth = 0; depth < 6 && current; depth++) {
    if (current instanceof HTMLElement && current.classList.contains('univer-grid-flow-col'))
      return current;
    current = current.parentElement;
  }
  return node instanceof HTMLElement ? node : null;
}

/** 隐藏 Univer 原生 pt 字号下拉与放大/缩小按钮。 */
function hideNativeFontSizeControls(toolbar: HTMLElement): void {
  for (const combo of toolbar.querySelectorAll('[role="combobox"]')) {
    const trigger = combo.querySelector('button') ?? combo;
    const text = trigger.textContent?.trim() ?? '';
    if (/^\d{1,2}(\.\d+)?$/.test(text) || NATIVE_PT_SIZE_RE.test(text))
      hideElement(findFontSizeControlAncestor(combo));
  }

  for (const btn of toolbar.querySelectorAll('button[title], button[aria-label]')) {
    const hint = `${btn.getAttribute('title') ?? ''} ${btn.getAttribute('aria-label') ?? ''}`;
    if (/字号|字体大小|font\s*size|字型大小|放大字|缩小字|fontSize/i.test(hint))
      hideElement(findFontSizeControlAncestor(btn));
  }

  for (const el of toolbar.querySelectorAll('button, span, div')) {
    if (!(el instanceof HTMLElement))
      continue;
    if (el.closest(`.${HOST_CLASS}`))
      continue;
    const text = el.textContent?.trim() ?? '';
    if (!NATIVE_PT_SIZE_RE.test(text))
      continue;
    if (el.children.length > 0)
      continue;
    hideElement(findFontSizeControlAncestor(el));
  }
}

function disposePanelRoot(): void {
  if (panelRoot) {
    panelRoot.unmount();
    panelRoot = null;
  }
  panelHost = null;
}

function ensurePanelMounted(host: HTMLElement, univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  if (panelHost === host && panelRoot && host.isConnected)
    return;

  if (panelHost !== host || !host.isConnected)
    disposePanelRoot();

  panelHost = host;
  panelRoot = createRoot(host);
  panelRoot.render(createElement(FontSizeMmPanel, { univerAPI }));
}

function findExistingHost(toolbar: HTMLElement): HTMLElement | null {
  const host = toolbar.querySelector(`.${HOST_CLASS}`);
  return host instanceof HTMLElement ? host : null;
}

function insertHostAfterFontFamily(toolbar: HTMLElement, host: HTMLElement): void {
  const fontFamilyCombo = [...toolbar.querySelectorAll('[role="combobox"]')]
    .find((combo) => {
      const text = (combo.querySelector('button') ?? combo).textContent?.trim() ?? '';
      return /Arial|SimSun|宋体|字体|Font/i.test(text) || text.length > 3;
    });

  if (fontFamilyCombo?.parentElement)
    fontFamilyCombo.parentElement.insertAdjacentElement('afterend', host);
  else
    toolbar.insertBefore(host, toolbar.firstChild);
}

function syncFontSizeMmControl(univerAPI: ReturnType<typeof FUniver.newAPI>): boolean {
  const toolbar = findRibbonToolbar();
  if (!toolbar)
    return false;

  document.documentElement.classList.add('hycad-mm-units');

  let host = findExistingHost(toolbar);
  if (!host || !host.isConnected) {
    host = document.createElement('div');
    host.className = HOST_CLASS;
    host.setAttribute(HOST_FLAG, 'true');
    insertHostAfterFontFamily(toolbar, host);
    if (!loggedInject) {
      console.info('[HyCAD mm] font-size injected');
      loggedInject = true;
    }
  }

  ensurePanelMounted(host, univerAPI);
  hideNativeFontSizeControls(toolbar);
  return true;
}

/** 在「开始」Ribbon 注入字高(mm) 控件并隐藏原生 pt 字号。 */
export function installFontSizeMmControl(univerAPI: ReturnType<typeof FUniver.newAPI>): void {
  univerAPIRef = univerAPI;
  syncFontSizeMmControl(univerAPI);

  const observer = new MutationObserver(() => {
    if (univerAPIRef)
      syncFontSizeMmControl(univerAPIRef);
  });

  observer.observe(document.body, { childList: true, subtree: true });
  window.setTimeout(() => syncFontSizeMmControl(univerAPI), 400);
  window.setTimeout(() => syncFontSizeMmControl(univerAPI), 1200);
  window.setTimeout(() => syncFontSizeMmControl(univerAPI), 3000);
}

export { updateFontSizeMmFromSelection } from './font-size-mm-model';
