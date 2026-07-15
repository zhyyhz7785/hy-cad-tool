import '@kitware/vtk.js/Rendering/Profiles/Geometry';

import vtkFullScreenRenderWindow from '@kitware/vtk.js/Rendering/Misc/FullScreenRenderWindow';
import vtkActor from '@kitware/vtk.js/Rendering/Core/Actor';
import vtkMapper from '@kitware/vtk.js/Rendering/Core/Mapper';
import vtkPolyData from '@kitware/vtk.js/Common/DataModel/PolyData';
import vtkDataArray from '@kitware/vtk.js/Common/Core/DataArray';
import vtkColorTransferFunction from '@kitware/vtk.js/Rendering/Core/ColorTransferFunction';
import vtkScalarBarActor from '@kitware/vtk.js/Rendering/Core/ScalarBarActor';

const emptyEl = document.getElementById('empty');
const container = document.getElementById('viewer');

const fullScreenRenderer = vtkFullScreenRenderWindow.newInstance({
  rootContainer: container,
  background: [0.12, 0.12, 0.12],
});
const renderer = fullScreenRenderer.getRenderer();
const renderWindow = fullScreenRenderer.getRenderWindow();

const polyData = vtkPolyData.newInstance();
const mapper = vtkMapper.newInstance({
  interpolateScalarsBeforeMapping: true,
  useLookupTableScalarRange: true,
});
mapper.setInputData(polyData);

const lut = vtkColorTransferFunction.newInstance();
lut.addRGBPoint(0, 0.15, 0.3, 0.9);
lut.addRGBPoint(0.5, 0.2, 0.85, 0.4);
lut.addRGBPoint(1, 0.95, 0.2, 0.15);
mapper.setLookupTable(lut);

const actor = vtkActor.newInstance();
actor.setMapper(mapper);
actor.getProperty().setLineWidth(4);
renderer.addActor(actor);

const scalarBar = vtkScalarBarActor.newInstance();
scalarBar.setScalarsToColors(lut);
scalarBar.setAxisLabel('U_mag');
renderer.addActor(scalarBar);

function setEmpty(visible, text) {
  if (!emptyEl) return;
  emptyEl.style.display = visible ? 'flex' : 'none';
  if (text) emptyEl.textContent = text;
}

function setMesh(payload) {
  try {
    const data = typeof payload === 'string' ? JSON.parse(payload) : payload;
    const points = data.points || [];
    const lines = data.lines || [];
    const active = data.activeScalar || 'U_mag';
    const scalars = (data.scalars && data.scalars[active]) || [];
    const nPts = points.length / 3;
    if (nPts < 2 || lines.length < 2) {
      setEmpty(true, '网格为空');
      return;
    }

    const lineCells = new Uint32Array((lines.length / 2) * 3);
    for (let i = 0, c = 0; i < lines.length; i += 2, c += 3) {
      lineCells[c] = 2;
      lineCells[c + 1] = lines[i];
      lineCells[c + 2] = lines[i + 1];
    }

    polyData.getPoints().setData(Float32Array.from(points), 3);
    polyData.getLines().setData(lineCells);

    const sc = Float32Array.from(scalars.length === nPts ? scalars : new Float32Array(nPts));
    let min = sc[0];
    let max = sc[0];
    for (let i = 1; i < sc.length; i++) {
      if (sc[i] < min) min = sc[i];
      if (sc[i] > max) max = sc[i];
    }
    if (!(max > min)) {
      max = min + 1e-12;
    }

    polyData.getPointData().setScalars(
      vtkDataArray.newInstance({
        name: active,
        values: sc,
        numberOfComponents: 1,
      })
    );

    lut.removeAllPoints();
    lut.addRGBPoint(min, 0.15, 0.3, 0.9);
    lut.addRGBPoint(min + 0.5 * (max - min), 0.2, 0.85, 0.4);
    lut.addRGBPoint(max, 0.95, 0.2, 0.15);
    mapper.setScalarRange(min, max);
    scalarBar.setAxisLabel(active);
    scalarBar.setScalarsToColors(lut);

    renderer.resetCamera();
    renderWindow.render();
    setEmpty(false);
  } catch (e) {
    setEmpty(true, '渲染失败: ' + (e && e.message ? e.message : e));
  }
}

window.hyfea = {
  setMesh,
  ready: true,
};

setEmpty(true, '求解后此处显示位移云图（U_mag）');
renderWindow.render();
