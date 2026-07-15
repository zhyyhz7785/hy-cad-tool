"""
HYFEA result viewer sidecar (PyVista + trame).

Reads a VTU written by HYFEA.Viz and serves an interactive browser UI:
scalar colormap, undeformed overlay, warp scale, edges, point probe, warp animation.
"""

from __future__ import annotations

import argparse
import math
import os
import threading
import time
from pathlib import Path

import pyvista as pv
from pyvista.trame.ui import plotter_ui
from trame.app import get_server
from trame.ui.vuetify3 import SinglePageLayout
from trame.widgets import vuetify3 as vuetify


def _default_warp_factor(mesh: pv.DataSet) -> float:
    """Scale so peak |U| is about 10% of model length."""
    bounds = mesh.bounds
    size = max(
        abs(bounds[1] - bounds[0]),
        abs(bounds[3] - bounds[2]),
        abs(bounds[5] - bounds[4]),
        1.0,
    )
    if "U" not in mesh.point_data:
        return 1.0
    mags = (mesh.point_data["U"] ** 2).sum(axis=1) ** 0.5
    peak = float(mags.max()) if len(mags) else 0.0
    if peak <= 0:
        return 1.0
    return max(1.0, 0.1 * size / peak)


def build_app(vtu_path: Path, port: int) -> None:
    server = get_server(client_type="vue3")
    state, ctrl = server.state, server.controller

    pl = pv.Plotter(off_screen=True)
    pl.set_background("#1e1e1e")

    actors: dict[str, object] = {"warped": None, "undeformed": None}
    mesh_ref: dict[str, pv.DataSet | None] = {"mesh": None}
    mtime_ref = {"mtime": 0.0}
    anim_ref = {"running": False, "thread": None}
    flags = {"suppress_change": False, "first_load": True}
    scene_lock = threading.Lock()

    state.trame__title = "HYFEA Viewer"
    state.scalar = "U_mag"
    state.warp_factor = 1.0
    state.show_edges = True
    state.show_undeformed = True
    state.probe_text = "点击网格点查看位移"
    state.animate = False
    state.status = "等待 VTU…"
    state.scalar_options = ["U_mag", "UX", "UY"]
    state.reload_nonce = 0

    def clear_actors() -> None:
        for key in ("warped", "undeformed"):
            actor = actors.get(key)
            if actor is not None:
                try:
                    pl.remove_actor(actor)
                except Exception:
                    pass
                actors[key] = None
        try:
            pl.remove_scalar_bar()
        except Exception:
            pass

    def read_vtu_with_retry(path: Path, attempts: int = 8) -> pv.DataSet | None:
        """C# may still be writing the file; retry briefly."""
        last_err: Exception | None = None
        for i in range(attempts):
            try:
                if not path.is_file() or path.stat().st_size < 64:
                    time.sleep(0.05 * (i + 1))
                    continue
                return pv.read(str(path))
            except Exception as ex:  # noqa: BLE001
                last_err = ex
                time.sleep(0.05 * (i + 1))
        if last_err is not None:
            state.status = f"读取失败: {last_err}"
        return None

    def load_mesh() -> bool:
        if not vtu_path.is_file():
            state.status = f"未找到 {vtu_path}"
            return False

        mesh = read_vtu_with_retry(vtu_path)
        if mesh is None:
            return False

        options = [n for n in ("U_mag", "UX", "UY") if n in mesh.point_data]
        if not options:
            options = [n for n in mesh.point_data.keys() if getattr(mesh.point_data[n], "ndim", 1) == 1][:5]
        if not options:
            options = ["U_mag"]

        flags["suppress_change"] = True
        try:
            state.scalar_options = options
            if state.scalar not in options:
                state.scalar = options[0]
            # Only auto-scale warp on first successful load; keep user slider later
            if flags["first_load"]:
                state.warp_factor = round(_default_warp_factor(mesh), 3)
                flags["first_load"] = False
        finally:
            flags["suppress_change"] = False

        mesh_ref["mesh"] = mesh
        try:
            mtime_ref["mtime"] = vtu_path.stat().st_mtime
        except OSError:
            pass
        state.status = f"已加载 {vtu_path.name} · 点 {mesh.n_points}"
        return True

    def rebuild_scene(*, reset_camera: bool = False) -> None:
        mesh = mesh_ref["mesh"]
        if mesh is None:
            return

        with scene_lock:
            try:
                clear_actors()

                factor = float(state.warp_factor or 0.0)
                scalar = state.scalar if state.scalar in mesh.point_data else None

                if "U" in mesh.point_data and factor != 0:
                    warped = mesh.warp_by_vector("U", factor=factor)
                else:
                    warped = mesh

                actors["warped"] = pl.add_mesh(
                    warped,
                    scalars=scalar,
                    show_edges=bool(state.show_edges),
                    line_width=3,
                    cmap="coolwarm",
                    scalar_bar_args={"title": scalar or "", "color": "white"},
                    name="warped",
                )

                if state.show_undeformed:
                    actors["undeformed"] = pl.add_mesh(
                        mesh,
                        style="wireframe",
                        color="#888888",
                        opacity=0.45,
                        line_width=2,
                        name="undeformed",
                    )

                if reset_camera:
                    pl.reset_camera()
                pl.render()
                if hasattr(ctrl, "view_update") and ctrl.view_update:
                    ctrl.view_update()
            except Exception as ex:  # noqa: BLE001
                state.status = f"刷新场景失败: {ex}"

    def on_pick(point):
        mesh = mesh_ref["mesh"]
        if mesh is None or point is None:
            return
        try:
            idx = mesh.find_closest_point(point)
            ux = float(mesh.point_data["UX"][idx]) if "UX" in mesh.point_data else 0.0
            uy = float(mesh.point_data["UY"][idx]) if "UY" in mesh.point_data else 0.0
            um = (
                float(mesh.point_data["U_mag"][idx])
                if "U_mag" in mesh.point_data
                else math.hypot(ux, uy)
            )
            xyz = mesh.points[idx]
            state.probe_text = (
                f"点 {idx}  XYZ=({xyz[0]:.4g},{xyz[1]:.4g},{xyz[2]:.4g})  "
                f"UX={ux:.6g}  UY={uy:.6g}  |U|={um:.6g}"
            )
        except Exception as ex:  # noqa: BLE001
            state.probe_text = f"探针失败: {ex}"

    def enable_picking() -> None:
        try:
            pl.enable_point_picking(
                callback=on_pick,
                show_message=False,
                use_picker=True,
                left_clicking=True,
            )
        except Exception:
            state.probe_text = "当前后端不支持点选探针"

    def watch_file() -> None:
        """Detect mtime change; bump state.reload_nonce so trame main thread reloads."""
        while True:
            time.sleep(0.5)
            try:
                if not vtu_path.is_file():
                    continue
                mt = vtu_path.stat().st_mtime
                if mt == mtime_ref["mtime"]:
                    continue
                # Debounce incomplete writes from C#
                time.sleep(0.2)
                try:
                    mt2 = vtu_path.stat().st_mtime
                except OSError:
                    continue
                if mt2 == mtime_ref["mtime"]:
                    continue
                mtime_ref["mtime"] = mt2
                # State mutation is marshaled by trame/wslink onto the server loop
                state.reload_nonce = int(state.reload_nonce or 0) + 1
            except Exception:
                pass

    def animation_loop() -> None:
        base = float(state.warp_factor or 1.0)
        t0 = time.time()
        while anim_ref["running"]:
            phase = 0.5 * (1.0 + math.sin(2.0 * math.pi * (time.time() - t0) / 2.5))
            flags["suppress_change"] = True
            try:
                state.warp_factor = round(base * phase, 4)
            finally:
                flags["suppress_change"] = False
            rebuild_scene(reset_camera=False)
            time.sleep(0.08)
        flags["suppress_change"] = True
        try:
            state.warp_factor = base
        finally:
            flags["suppress_change"] = False
        rebuild_scene(reset_camera=False)

    @state.change("reload_nonce")
    def _on_reload_nonce(reload_nonce, **_):
        if not reload_nonce:
            return
        if load_mesh():
            rebuild_scene(reset_camera=True)

    @state.change("scalar", "warp_factor", "show_edges", "show_undeformed")
    def _on_viz_change(**_):
        if flags["suppress_change"]:
            return
        rebuild_scene(reset_camera=False)

    @state.change("animate")
    def _on_animate(animate, **_):
        if animate and not anim_ref["running"]:
            anim_ref["running"] = True
            th = threading.Thread(target=animation_loop, daemon=True)
            anim_ref["thread"] = th
            th.start()
        elif not animate:
            anim_ref["running"] = False

    with SinglePageLayout(server) as layout:
        layout.title.set_text("HYFEA Viewer")
        with layout.toolbar:
            vuetify.VSpacer()
            vuetify.VSelect(
                v_model=("scalar", "U_mag"),
                items=("scalar_options",),
                label="标量",
                density="compact",
                hide_details=True,
                style="max-width: 140px",
            )
            vuetify.VSlider(
                v_model=("warp_factor", 1.0),
                min=0,
                max=1000,
                step=0.1,
                label="变形比例",
                density="compact",
                hide_details=True,
                style="max-width: 220px; margin-left: 12px",
            )
            vuetify.VCheckbox(
                v_model=("show_edges", True),
                label="网格边",
                density="compact",
                hide_details=True,
                class_="ml-2",
            )
            vuetify.VCheckbox(
                v_model=("show_undeformed", True),
                label="原网格",
                density="compact",
                hide_details=True,
            )
            vuetify.VCheckbox(
                v_model=("animate", False),
                label="动画",
                density="compact",
                hide_details=True,
            )
        with layout.content:
            with vuetify.VContainer(fluid=True, classes="pa-0 fill-height"):
                view = plotter_ui(pl)
                ctrl.view_update = view.update
            vuetify.VChip(v_text=("probe_text",), class_="ma-2", variant="tonal")
            vuetify.VChip(v_text=("status",), class_="ma-2", color="primary", variant="tonal")

    if load_mesh():
        rebuild_scene(reset_camera=True)
    enable_picking()

    threading.Thread(target=watch_file, daemon=True).start()

    server.start(
        port=port,
        host="127.0.0.1",
        open_browser=False,
        show_connection_info=False,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="HYFEA PyVista/trame result sidecar")
    parser.add_argument(
        "--vtu",
        default=os.path.join(os.environ.get("TEMP", "/tmp"), "hyfea-last.vtu"),
        help="Path to VTU file to watch",
    )
    parser.add_argument("--port", type=int, default=8765, help="HTTP port")
    args = parser.parse_args()
    build_app(Path(args.vtu), args.port)


if __name__ == "__main__":
    main()
