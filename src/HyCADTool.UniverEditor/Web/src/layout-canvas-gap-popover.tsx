import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Button, InputNumber } from '@univerjs/design';

import { DEFAULT_CANVAS_SHEET_GAP_MM, normalizeCanvasSheetGapMm } from './page-canvas-gap';

const PORTAL_HOST_ID = 'hycad-canvas-gap-popover-host';
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

interface LayoutCanvasGapPopoverProps {
  gapMm: number;
  onChange: (gapMm: number) => void;
}

export function LayoutCanvasGapPopover(props: LayoutCanvasGapPopoverProps): JSX.Element {
  const { gapMm, onChange } = props;
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(gapMm);
  const anchorRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open)
      setDraft(gapMm);
  }, [open, gapMm]);

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

  const commit = (value: number | null): void => {
    const next = normalizeCanvasSheetGapMm(positiveNumber(value, draft), DEFAULT_CANVAS_SHEET_GAP_MM);
    setDraft(next);
    onChange(next);
  };

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
          aria-label="外距参数"
        >
          <div className="hycad-margin-popover__title">外距参数</div>
          <div className="hycad-margin-popover__row">
            <span className="hycad-margin-popover__label">图纸与标尺间距</span>
            <InputNumber
              className={COMPACT_INPUT_CLASS}
              size="mini"
              controls={false}
              min={0}
              value={draft}
              onChange={commit}
            />
          </div>
          <div className="hycad-margin-popover__hint">
            白纸外框距画布/标尺四边距离（mm）
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
        title="图纸与标尺间距（画布边距）"
        onClick={() => setOpen((v) => !v)}
      >
        外距 {gapMm}
      </Button>
      {panel ? createPortal(panel, ensurePortalHost()) : null}
    </>
  );
}
