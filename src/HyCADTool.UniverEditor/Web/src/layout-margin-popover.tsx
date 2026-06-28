import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Button, InputNumber } from '@univerjs/design';

import {
  DEFAULT_PAGE_MARGINS,
  normalizeMarginMm,
  patchPageMargins,
  syncAllMarginsFromOutline,
  type PageMarginsMm,
} from './page-margins';

const PORTAL_HOST_ID = 'hycad-margin-popover-host';
const COMPACT_INPUT_CLASS = 'hycad-margin-popover__input !univer-w-[2.75rem]';

function ensurePortalHost(): HTMLElement {
  let host = document.getElementById(PORTAL_HOST_ID);
  if (!host) {
    host = document.createElement('div');
    host.id = PORTAL_HOST_ID;
    host.style.cssText = 'position:fixed;left:0;top:0;z-index:30000;pointer-events:none;width:0;height:0;overflow:visible;';
    document.body.appendChild(host);
  }
  return host;
}

function positiveNumber(value: number | null, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : fallback;
}

interface LayoutMarginPopoverProps {
  margins: PageMarginsMm;
  onChange: (next: PageMarginsMm) => void;
}

export function LayoutMarginPopover(props: LayoutMarginPopoverProps): JSX.Element {
  const { margins, onChange } = props;
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(margins);
  const anchorRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open)
      setDraft(margins);
  }, [open, margins]);

  useEffect(() => {
    if (!open)
      return;

    const onDocClick = (ev: MouseEvent): void => {
      const t = ev.target;
      if (!(t instanceof Node))
        return;
      if (anchorRef.current?.contains(t) || panelRef.current?.contains(t))
        return;
      setOpen(false);
    };

    const timer = window.setTimeout(() => {
      document.addEventListener('mousedown', onDocClick);
    }, 0);

    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('mousedown', onDocClick);
    };
  }, [open]);

  const commit = (next: PageMarginsMm): void => {
    setDraft(next);
    onChange(next);
  };

  const onOutline = (value: number | null): void => {
    commit(syncAllMarginsFromOutline(positiveNumber(value, draft.outlineMm)));
  };

  const onSide = (key: 'top' | 'bottom' | 'left' | 'right', value: number | null): void => {
    commit(patchPageMargins(draft, { [key]: positiveNumber(value, draft[key]) }));
  };

  const summary = draft.top === draft.bottom && draft.left === draft.right && draft.top === draft.left
    ? `${draft.top}`
    : `${draft.top}/${draft.bottom}/${draft.left}/${draft.right}`;

  const panel = open && anchorRef.current
    ? (
        <div
          ref={panelRef}
          className="hycad-margin-popover"
          style={{
            position: 'fixed',
            top: anchorRef.current.getBoundingClientRect().bottom + 4,
            left: anchorRef.current.getBoundingClientRect().left,
            pointerEvents: 'auto',
          }}
          role="dialog"
          aria-label="边距参数"
        >
          <div className="hycad-margin-popover__title">边距参数</div>

          <div className="hycad-margin-popover__row">
            <span className="hycad-margin-popover__label">整体轮廓距离</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft.outlineMm}
              onChange={onOutline}
            />
          </div>
          <div className="hycad-margin-popover__hint">
            修改整体轮廓距离将同步上下左右边距
          </div>

          <div className="hycad-margin-popover__grid">
            <span className="hycad-margin-popover__label">上边距</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft.top}
              onChange={(v) => onSide('top', v)}
            />
            <span className="hycad-margin-popover__label">下边距</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft.bottom}
              onChange={(v) => onSide('bottom', v)}
            />
          </div>

          <div className="hycad-margin-popover__grid">
            <span className="hycad-margin-popover__label">左边距</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft.left}
              onChange={(v) => onSide('left', v)}
            />
            <span className="hycad-margin-popover__label">右边距</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft.right}
              onChange={(v) => onSide('right', v)}
            />
          </div>
        </div>
      )
    : null;

  return (
    <>
      <Button
        ref={anchorRef}
        variant="text"
        size="small"
        className="!univer-h-[22px] !univer-px-1.5 !univer-text-[11px]"
        aria-expanded={open}
        title="页面边距（图纸内白边）"
        onClick={() => setOpen((v) => !v)}
      >
        边距 {summary}
      </Button>
      {panel ? createPortal(panel, ensurePortalHost()) : null}
    </>
  );
}

export function marginsFromLegacy(value: number): PageMarginsMm {
  return syncAllMarginsFromOutline(normalizeMarginMm(value, DEFAULT_PAGE_MARGINS.outlineMm));
}
