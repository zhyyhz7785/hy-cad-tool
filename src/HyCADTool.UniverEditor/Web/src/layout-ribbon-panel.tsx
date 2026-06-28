import { useSyncExternalStore, type ReactNode } from 'react';
import { Button, Checkbox, clsx, divideXClassName, InputNumber, Select } from '@univerjs/design';

import {
  getLayoutRibbonModelState,
  patchLayoutRibbonModel,
  PAPER_PRESETS,
  subscribeLayoutRibbonModel,
  TEMPLATE_OPTIONS,
} from './layout-ribbon-model';
import {
  isCustomPaperPreset,
  nextOrientation,
  orientationLabel,
  resolveContentTargetWidthMm,
  resolveSheetSizeMm,
} from './paper-sheet';
import { LayoutMarginPopover } from './layout-margin-popover';
import type { PageMarginsMm } from './page-margins';
import { marginsToDomainUniformMm } from './page-margins';

const RIBBON_GROUP_CLASS = `
  univer-grid univer-shrink-0 univer-grid-flow-col univer-gap-1.5 univer-px-1.5
  empty:univer-hidden
`;

const COMPACT_SELECT_CLASS = 'hycad-compact-select';

const PAPER_SELECT_CLASS = 'hycad-paper-select';

const TEMPLATE_SELECT_CLASS = 'hycad-template-select';

const COMPACT_NUMBER_CLASS = '!univer-w-[3.25rem]';

const TOOLBAR_BUTTON_CLASS = '!univer-h-[22px] !univer-px-1.5 !univer-text-[11px]';

const ORIENTATION_BUTTON_CLASS = `
  !univer-h-[22px] !univer-min-w-0 !univer-px-1.5 !univer-text-[11px] !univer-gap-0
`;

const LABEL_CLASS = `
  univer-text-xs univer-text-gray-500 dark:!univer-text-gray-400 univer-whitespace-nowrap
`;

export interface LayoutRibbonActions {
  sendLayoutOp: (op: string, value?: number) => void;
  sendFileAction: (action: string) => void;
  autoFitRowHeights: () => void;
  autoFitColWidths: () => void;
  postError: (message: string) => void;
  setShowRulers: (on: boolean) => void;
  setShowHeaders: (on: boolean) => void;
  setShowPaperBoundary: (on: boolean) => void;
  syncPaperFromLayoutInputs: (
    paperPresetIndex: number,
    orientation: number,
    targetWidthMm: number,
    pageMargins: PageMarginsMm,
  ) => void;
}

function RibbonGroup(props: { title?: string; children: ReactNode }): JSX.Element {
  const { title, children } = props;
  return (
    <div className={RIBBON_GROUP_CLASS} title={title}>
      <div className="univer-flex univer-items-center univer-gap-1">
        {children}
      </div>
    </div>
  );
}

function RibbonLabel(props: { children: ReactNode }): JSX.Element {
  return <span className={LABEL_CLASS}>{props.children}</span>;
}

function toPaperValue(index: number): string {
  return PAPER_PRESETS[Math.max(0, Math.min(PAPER_PRESETS.length - 1, index))] ?? PAPER_PRESETS[0];
}

function toTemplateValue(index: number): string {
  return TEMPLATE_OPTIONS[Math.max(0, Math.min(TEMPLATE_OPTIONS.length - 1, index))] ?? TEMPLATE_OPTIONS[0];
}

function indexOfPreset(value: string): number {
  const idx = PAPER_PRESETS.indexOf(value as typeof PAPER_PRESETS[number]);
  return idx >= 0 ? idx : 0;
}

function indexOfTemplate(value: string): number {
  const idx = TEMPLATE_OPTIONS.indexOf(value as typeof TEMPLATE_OPTIONS[number]);
  return idx >= 0 ? idx : 0;
}

function positiveNumber(value: number | null, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : fallback;
}

function positiveInt(value: number | null, fallback: number, min = 1): number {
  if (typeof value !== 'number' || !Number.isFinite(value))
    return fallback;
  const v = Math.round(value);
  return v >= min ? v : fallback;
}

export function LayoutRibbonPanel(props: { actions: LayoutRibbonActions }): JSX.Element {
  const { actions } = props;
  const model = useSyncExternalStore(subscribeLayoutRibbonModel, getLayoutRibbonModelState);

  const pushPaper = (
    paperPresetIndex = model.paperPresetIndex,
    orientation = model.orientation,
    targetWidthMm = model.targetWidthMm,
    pageMargins = model.pageMargins,
  ): void => {
    actions.syncPaperFromLayoutInputs(paperPresetIndex, orientation, targetWidthMm, pageMargins);
  };

  const sheetSize = resolveSheetSizeMm(model.paperPresetIndex, model.orientation);

  const applyPaperState = (
    paperPresetIndex: number,
    orientation: number,
    pageMargins: PageMarginsMm,
    currentTargetWidthMm: number,
  ): void => {
    const targetWidthMm = resolveContentTargetWidthMm(
      paperPresetIndex,
      orientation,
      pageMargins,
      currentTargetWidthMm,
    );
    patchLayoutRibbonModel({ paperPresetIndex, orientation, targetWidthMm, pageMargins });
    pushPaper(paperPresetIndex, orientation, targetWidthMm, pageMargins);
  };

  const applyMargins = (pageMargins: PageMarginsMm): void => {
    applyPaperState(model.paperPresetIndex, model.orientation, pageMargins, model.targetWidthMm);
    actions.sendLayoutOp('setMargin', marginsToDomainUniformMm(pageMargins));
  };

  return (
    <div
      id="hycad-layout-ribbon-panel"
      className={clsx(
        'hycad-layout-ribbon-panel univer-flex univer-w-full univer-min-w-0 univer-flex-1 univer-items-center univer-overflow-x-auto',
        divideXClassName,
      )}
      data-hycad-comp="layout-ribbon-panel"
    >
      <RibbonGroup title="视图">
        <Checkbox
          checked={model.showRulers}
          onChange={(checked) => {
            if (typeof checked === 'boolean') {
              actions.setShowRulers(checked);
            }
          }}
        >
          标尺
        </Checkbox>
        <Checkbox
          checked={model.showHeaders}
          onChange={(checked) => {
            if (typeof checked === 'boolean') {
              actions.setShowHeaders(checked);
            }
          }}
        >
          行列头
        </Checkbox>
        <Checkbox
          checked={model.showPaperBoundary}
          onChange={(checked) => {
            if (typeof checked === 'boolean') {
              actions.setShowPaperBoundary(checked);
            }
          }}
        >
          纸张边界
        </Checkbox>
      </RibbonGroup>

      <RibbonGroup title="结构/填值">
        <Checkbox
          checked={model.structureMode}
          onChange={(checked) => {
            if (typeof checked === 'boolean') {
              patchLayoutRibbonModel({ structureMode: checked });
              actions.sendLayoutOp('setStructureMode', checked ? 1 : 0);
            }
          }}
        >
          结构模式
        </Checkbox>
      </RibbonGroup>

      <RibbonGroup title="纸张 L0">
        <RibbonLabel>图纸</RibbonLabel>
        <Select
          className={clsx(COMPACT_SELECT_CLASS, PAPER_SELECT_CLASS)}
          value={toPaperValue(model.paperPresetIndex)}
          options={PAPER_PRESETS.map((label) => ({ label, value: label }))}
          onChange={(value) => {
            const idx = indexOfPreset(value);
            applyPaperState(idx, model.orientation, model.pageMargins, model.targetWidthMm);
            actions.sendLayoutOp('setPaperPreset', idx);
          }}
        />
        <Button
          variant="text"
          size="small"
          className={ORIENTATION_BUTTON_CLASS}
          title="切换横向/竖向（hymd 同款）"
          onClick={() => {
            const next = nextOrientation(model.orientation);
            applyPaperState(model.paperPresetIndex, next, model.pageMargins, model.targetWidthMm);
            actions.sendLayoutOp('setOrientation', next);
          }}
        >
          <span>{orientationLabel(model.orientation)}</span>
          <span className="univer-ml-1 univer-text-[10px] univer-text-gray-400 dark:!univer-text-gray-500">
            {sheetSize.label}
          </span>
        </Button>
        <LayoutMarginPopover
          margins={model.pageMargins}
          onChange={applyMargins}
        />
        {isCustomPaperPreset(model.paperPresetIndex) ? (
          <>
            <RibbonLabel>总宽</RibbonLabel>
            <InputNumber
              className={COMPACT_NUMBER_CLASS}
              size="mini"
              controls={false}
              min={1}
              value={model.targetWidthMm}
              onChange={(value) => {
                const next = positiveNumber(value, model.targetWidthMm);
                patchLayoutRibbonModel({ targetWidthMm: next });
                pushPaper(model.paperPresetIndex, model.orientation, next, model.pageMargins);
                actions.sendLayoutOp('setTargetWidth', next);
              }}
            />
          </>
        ) : null}
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="内容驱动改列宽后，手动按纸宽与边距再次均分全部列宽"
          onClick={() => actions.sendLayoutOp('fitColumnsToPaper')}
        >
          列宽适配纸宽
        </Button>
      </RibbonGroup>

      <RibbonGroup title="表格">
        <RibbonLabel>行</RibbonLabel>
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={1}
          precision={0}
          step={1}
          value={model.rowCount}
          onChange={(value) => actions.sendLayoutOp('setRowCount', positiveInt(value, model.rowCount))}
        />
        <RibbonLabel>列</RibbonLabel>
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={1}
          precision={0}
          step={1}
          value={model.colCount}
          onChange={(value) => actions.sendLayoutOp('setColCount', positiveInt(value, model.colCount))}
        />
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="按上方行列数创建空表"
          onClick={() => actions.sendLayoutOp('newTable')}
        >
          新建空表
        </Button>
        <Select
          className={clsx(COMPACT_SELECT_CLASS, TEMPLATE_SELECT_CLASS)}
          value={toTemplateValue(model.templateIndex)}
          options={TEMPLATE_OPTIONS.map((label) => ({ label, value: label }))}
          onChange={(value) => actions.sendLayoutOp('setTemplate', indexOfTemplate(value))}
        />
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="加载所选模板"
          onClick={() => actions.sendLayoutOp('loadTemplate')}
        >
          从模板加载
        </Button>
      </RibbonGroup>

      <RibbonGroup title="内容驱动">
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="按单元格内容撑开行高（Excel 式）"
          onClick={() => actions.autoFitRowHeights()}
        >
          自动调整行高
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="按单元格内容撑开列宽（Excel 式）"
          onClick={() => actions.autoFitColWidths()}
        >
          自动调整列宽
        </Button>
      </RibbonGroup>

      <RibbonGroup title="单元格大小 mm">
        <RibbonLabel>行高</RibbonLabel>
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={0}
          value={model.rowHeightMm ?? undefined}
          allowEmpty
          onChange={(value) => {
            if (value !== null)
              actions.sendLayoutOp('setRowHeight', positiveNumber(value, 10));
          }}
        />
        <RibbonLabel>列宽</RibbonLabel>
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={0}
          value={model.colWidthMm ?? undefined}
          allowEmpty
          onChange={(value) => {
            if (value !== null)
              actions.sendLayoutOp('setColWidth', positiveNumber(value, 25));
          }}
        />
      </RibbonGroup>

      <RibbonGroup title="单元格">
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="在选区下方插入行"
          onClick={() => actions.sendLayoutOp('insertRow')}
        >
          +行
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="删除选中行"
          onClick={() => actions.sendLayoutOp('deleteRow')}
        >
          -行
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="在选区右侧插入列"
          onClick={() => actions.sendLayoutOp('insertCol')}
        >
          +列
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="删除选中列"
          onClick={() => actions.sendLayoutOp('deleteCol')}
        >
          -列
        </Button>
      </RibbonGroup>

      <RibbonGroup title="合并">
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="合并选区"
          onClick={() => actions.sendLayoutOp('merge')}
        >
          合并
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          disabled={!model.structureMode}
          title="拆分合并区"
          onClick={() => actions.sendLayoutOp('unmerge')}
        >
          拆分
        </Button>
      </RibbonGroup>

      <RibbonGroup title="比例">
        <RibbonLabel>Scale</RibbonLabel>
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={0}
          value={model.scale ?? undefined}
          allowEmpty
          onChange={(value) => {
            if (value !== null)
              actions.sendLayoutOp('setScale', positiveNumber(value, 1));
          }}
        />
      </RibbonGroup>

      <RibbonGroup title="HyCAD">
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="落图到 AutoCAD（按比例放大）"
          onClick={() => actions.sendFileAction('publish')}
        >
          落图
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="从图面拾取 HyTable"
          onClick={() => actions.sendFileAction('pick')}
        >
          拾取
        </Button>
        <Button
          variant="text"
          size="small"
          className={TOOLBAR_BUTTON_CLASS}
          title="单元格角色（R6，后期开放）"
          onClick={() => actions.postError('R6 角色功能后期开放')}
        >
          角色
        </Button>
      </RibbonGroup>
    </div>
  );
}
