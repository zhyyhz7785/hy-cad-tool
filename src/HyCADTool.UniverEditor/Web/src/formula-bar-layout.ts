/**
 * U4 公式栏与 U7 网格分离：将公式栏移到 U3（Ribbon 第二行）正下方，左对齐。
 */

const FORMULA_BAR_SLOT_ID = 'hycad-u4-formula-slot';
const FORMULA_BAR_CLASS = 'hycad-u4-formula-bar';
const U7_SECTION_CLASS = 'hycad-u7-section';
const INJECTED_FLAG = 'data-hycad-u4-split';

function findSheetBox(): HTMLElement | null {
  const content = document.querySelector('#app [data-range-selector]');
  if (!(content instanceof HTMLElement))
    return null;
  const parent = content.parentElement;
  return parent instanceof HTMLElement ? parent : null;
}

function findRibbonHeader(): HTMLElement | null {
  const node = document.querySelector('#app header[data-u-comp="headerbar"]');
  return node instanceof HTMLElement ? node : null;
}

function findFormulaHeader(sheetBox: HTMLElement): HTMLElement | null {
  const header = sheetBox.querySelector(':scope > header');
  return header instanceof HTMLElement ? header : null;
}

function ensureFormulaSlot(ribbonHeader: HTMLElement): HTMLElement {
  let slot = document.getElementById(FORMULA_BAR_SLOT_ID);
  if (slot instanceof HTMLElement)
    return slot;

  slot = document.createElement('div');
  slot.id = FORMULA_BAR_SLOT_ID;
  slot.className = 'hycad-u4-formula-slot';
  ribbonHeader.insertAdjacentElement('afterend', slot);
  return slot;
}

function splitFormulaBarFromGrid(): boolean {
  const sheetBox = findSheetBox();
  const ribbonHeader = findRibbonHeader();
  if (!sheetBox || !ribbonHeader)
    return false;

  const formulaHeader = findFormulaHeader(sheetBox);
  if (!formulaHeader)
    return sheetBox.getAttribute(INJECTED_FLAG) === 'true';

  if (formulaHeader.getAttribute(INJECTED_FLAG) === 'true')
    return true;

  const slot = ensureFormulaSlot(ribbonHeader);
  slot.appendChild(formulaHeader);
  formulaHeader.classList.add(FORMULA_BAR_CLASS);
  formulaHeader.setAttribute(INJECTED_FLAG, 'true');
  sheetBox.classList.add(U7_SECTION_CLASS);
  sheetBox.setAttribute(INJECTED_FLAG, 'true');
  return true;
}

/** U4 公式栏底边（viewport px）；未拆分时返回 0。 */
export function getFormulaBarBottomPx(): number {
  const slot = document.getElementById(FORMULA_BAR_SLOT_ID);
  if (slot instanceof HTMLElement) {
    const rect = slot.getBoundingClientRect();
    if (rect.height > 0)
      return rect.bottom;
  }
  const bar = document.querySelector('.hycad-u4-formula-bar');
  if (bar instanceof HTMLElement) {
    const rect = bar.getBoundingClientRect();
    if (rect.height > 0)
      return rect.bottom;
  }
  return 0;
}

export function installFormulaBarLayout(): () => void {
  const trySplit = (): void => {
    splitFormulaBarFromGrid();
  };

  trySplit();

  const app = document.getElementById('app');
  const observer = app
    ? new MutationObserver(trySplit)
    : undefined;
  observer?.observe(app!, { childList: true, subtree: true });

  window.setTimeout(trySplit, 400);
  window.setTimeout(trySplit, 1200);

  return () => {
    observer?.disconnect();
  };
}
