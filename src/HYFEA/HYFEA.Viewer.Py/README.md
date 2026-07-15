# HYFEA Viewer Python sidecar (PyVista + trame)

```powershell
cd src\HYFEA\HYFEA.Viewer.Py
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

手动启动（浏览器打开提示的地址）：

```powershell
.\.venv\Scripts\python.exe viewer.py --port 8765 --vtu $env:TEMP\hyfea-last.vtu
```

Shell 会优先使用本目录 `.venv\Scripts\python.exe`；失败时回落 vtk.js 视口。
