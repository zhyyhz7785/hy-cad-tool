/**
 * 防止 SheetBar（footer）被 section 内 univer-h-full 主内容区挤出视口。
 */
const SECTION_SELECTOR = '#app [data-u-comp="workbench-layout"] > section.univer-flex-1';

function fixSheetBarLayout(): void {
  const section = document.querySelector(SECTION_SELECTOR);
  if (!(section instanceof HTMLElement))
    return;

  const content = section.querySelector(':scope > div.univer-grid.univer-h-full');
  if (content instanceof HTMLElement) {
    content.style.flex = '1 1 0';
    content.style.minHeight = '0';
    content.style.height = 'auto';
    content.style.maxHeight = 'none';
    content.style.overflow = 'hidden';
  }

  const footer = section.querySelector(':scope > footer');
  if (!(footer instanceof HTMLElement))
    return;

  footer.hidden = false;
  footer.removeAttribute('hidden');
  footer.style.removeProperty('display');
  footer.style.visibility = 'visible';
  footer.style.pointerEvents = 'auto';
  footer.style.flexShrink = '0';

  const rect = footer.getBoundingClientRect();
  if (rect.height < 8)
    footer.style.minHeight = '36px';

  // footer 被挤出视口时再强制一次（最大化/resize 后）
  if (rect.bottom > window.innerHeight + 1 || rect.top >= window.innerHeight - 1) {
    section.style.display = 'flex';
    section.style.flexDirection = 'column';
    section.style.minHeight = '0';
    section.style.overflow = 'hidden';
  }
}

export function installSheetBarGuard(): void {
  fixSheetBarLayout();

  window.addEventListener('resize', fixSheetBarLayout);

  const root = document.getElementById('app');
  if (!root)
    return;

  const observer = new MutationObserver(() => {
    fixSheetBarLayout();
  });

  observer.observe(root, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['hidden', 'class', 'style'],
  });

  window.setTimeout(fixSheetBarLayout, 300);
  window.setTimeout(fixSheetBarLayout, 1000);
  window.setTimeout(fixSheetBarLayout, 2500);
}
