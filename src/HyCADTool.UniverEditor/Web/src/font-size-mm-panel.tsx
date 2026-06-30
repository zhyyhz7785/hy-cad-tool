import { useSyncExternalStore } from 'react';
import { Button, InputNumber, Select } from '@univerjs/design';
import type { FUniver } from '@univerjs/core/facade';

import {
  applyFontSizeMmToSelection,
  getFontSizeMm,
  setFontSizeMm,
  stepFontSizeMm,
  subscribeFontSizeMm,
} from './font-size-mm-model';
import { CAD_TEXT_HEIGHTS_MM, positiveMm, TEXT_HEIGHT_MM } from './mm-display';

const LABEL_CLASS = `
  univer-text-xs univer-text-gray-500 dark:!univer-text-gray-400 univer-whitespace-nowrap
`;

const COMPACT_NUMBER_CLASS = '!univer-w-[3.25rem]';
const COMPACT_SELECT_CLASS = 'hycad-compact-select hycad-font-size-select';
const STEP_BTN_CLASS = 'hycad-font-size-step !univer-min-w-0 !univer-px-1';

function toSelectValue(mm: number | null): string {
  if (mm == null)
    return '';
  const preset = CAD_TEXT_HEIGHTS_MM.find(v => Math.abs(v - mm) < 0.001);
  return preset != null ? String(preset) : 'custom';
}

export function FontSizeMmPanel(props: { univerAPI: ReturnType<typeof FUniver.newAPI> }): JSX.Element {
  const { univerAPI } = props;
  const mm = useSyncExternalStore(subscribeFontSizeMm, getFontSizeMm);

  const applyMm = (value: number): void => {
    applyFontSizeMmToSelection(univerAPI, positiveMm(value, TEXT_HEIGHT_MM));
  };

  const selectValue = toSelectValue(mm);
  const showCustom = selectValue === 'custom';

  return (
    <div
      className="hycad-font-size-mm univer-flex univer-items-center univer-gap-1 univer-px-1"
      data-hycad-comp="font-size-mm"
    >
      <span className={LABEL_CLASS} title="纸面字高（毫米）">字高(mm)</span>
      <Button
        className={STEP_BTN_CLASS}
        size="mini"
        title="减小字高"
        onClick={() => applyMm(stepFontSizeMm(mm, -1))}
      >
        −
      </Button>
      <Select
        className={COMPACT_SELECT_CLASS}
        value={selectValue || String(TEXT_HEIGHT_MM)}
        options={[
          ...CAD_TEXT_HEIGHTS_MM.map(v => ({ label: `${v}`, value: String(v) })),
          { label: '自定义…', value: 'custom' },
        ]}
        onChange={(value) => {
          if (value === 'custom') {
            if (mm == null)
              setFontSizeMm(TEXT_HEIGHT_MM);
            return;
          }
          applyMm(Number(value));
        }}
      />
      {showCustom ? (
        <InputNumber
          className={COMPACT_NUMBER_CLASS}
          size="mini"
          controls={false}
          min={0.1}
          precision={2}
          step={0.1}
          value={mm ?? TEXT_HEIGHT_MM}
          onChange={(value) => {
            if (value !== null)
              applyMm(value);
          }}
        />
      ) : null}
      <Button
        className={STEP_BTN_CLASS}
        size="mini"
        title="增大字高"
        onClick={() => applyMm(stepFontSizeMm(mm, 1))}
      >
        +
      </Button>
    </div>
  );
}
