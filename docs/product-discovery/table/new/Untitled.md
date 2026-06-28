@ ```browser_element The user selected this node in the browser preview (blue outline in the screenshot). tag: canvas dom_path: div#hycad-editor-root > div#app > div.univer-flex.univer-h-full.univer-min-h-0.univer-flex-col.univer-bg-white.dark:!univer-bg-gray-800 > section.univer-relative.univer-flex.univer-min-h-0.univer-flex-1.univer-flex-col > div.univer-grid.univer-h-full.univer-grid-cols-[auto_1fr_auto].univer-grid-rows-[100%].univer-overflow-hidden.hycad-page-grid-host > section.univer-relative.univer-grid.univer-flex-1.univer-grid-rows-[auto_1fr].univer-overflow-hidden.univer-bg-white.dark:!univer-bg-gray-800.univer-border-gray-200.dark:!univer-border-gray-600.univer-border-solid.univer-border-l-0.univer-border-b.univer-border-t-0.univer-border-r-0.hycad-u7-section.hycad-page-sheet-box > section.univer-relative.univer-overflow-hidden.dark:!univer-bg-gray-900 > canvas#univer-sheet-main-canvas_hycad-univer-demo id: univer-sheet-main-canvas_hycad-univer-demo bounds_css_px: top=183 left=79 width=749 height=505 attributes:  data-u-comp=render-canvas  tabindex=1  width=1123  height=758  id=univer-sheet-main-canvas_hycad-univer-demo  style=padding: 0px; margin: 0px; border: 0px; background: transparent; position: absolute; top: 0px; left: 0px; z-index: 8;... ``` 网格的数量，是根据网格区域的大小，和网格的默认尺寸决定的，默认网格尺寸是6.5X25。

去掉区域内的滚动条，缩放的时候，图纸和网格整体缩放

行的数量，由  网格宽度/25，取整的到，同理列

确定了行列数量，再绘制网格

用户也可自由输入网格行列数量，空间内网格随之调整