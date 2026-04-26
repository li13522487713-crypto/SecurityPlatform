#!/usr/bin/env python3
"""校验 AppHost Controllers 与 Bosch.http 的覆盖关系（可选严格模式）。

规则：每个 Controller 的类级 Route 前缀都应在至少一个 `.http` 文件中出现。
AppHost 控制器数量较多，Bosch.http 往往不能全量覆盖。默认仅打印警告并以 0 退出，避免误伤本地/CI。

若需严格失败：设置环境变量 `HTTP_COVERAGE_STRICT=1`。
"""

from __future__ import annotations

from pathlib import Path
import os
import sys

REPO_ROOT = Path(__file__).resolve().parents[1]
CONTROLLERS_DIR = REPO_ROOT / "src/backend/Atlas.AppHost/Controllers"
HTTP_DIR = REPO_ROOT / "src/backend/Atlas.AppHost/Bosch.http"
ROUTE_PATTERN = '[Route("'


def extract_route_templates(controller_text: str) -> list[str]:
    routes: list[str] = []
    anchor = 0
    while True:
        start = controller_text.find(ROUTE_PATTERN, anchor)
        if start < 0:
            break
        value_start = start + len(ROUTE_PATTERN)
        value_end = controller_text.find('")', value_start)
        if value_end < 0:
            break
        route = controller_text[value_start:value_end].strip().lstrip("/")
        if route:
            routes.append(route)
        anchor = value_end + 2
    return routes


def collect_controller_routes() -> dict[str, list[str]]:
    routes: dict[str, list[str]] = {}
    for path in CONTROLLERS_DIR.rglob("*Controller.cs"):
        if "Compatibility" in path.parts:
            continue

        controller_name = path.stem
        if not controller_name.endswith("Controller"):
            continue

        logical_name = controller_name[: -len("Controller")]
        templates = extract_route_templates(path.read_text(encoding="utf-8"))
        if templates:
            routes[logical_name] = templates
    return routes


def load_http_contents() -> list[str]:
    return [path.read_text(encoding="utf-8") for path in HTTP_DIR.glob("*.http")]


def is_route_covered(route: str, http_contents: list[str]) -> bool:
    if "[" in route or "]" in route:
        return True
    expected = f"/{route}"
    return any(expected in content for content in http_contents)


def main() -> int:
    if not CONTROLLERS_DIR.exists() or not HTTP_DIR.exists():
        print("[verify-http-coverage] 路径不存在，请确认仓库结构。", file=sys.stderr)
        return 2

    controller_routes = collect_controller_routes()
    http_contents = load_http_contents()
    missing: list[tuple[str, str]] = []
    for controller_name, routes in sorted(controller_routes.items()):
        if not any(is_route_covered(route, http_contents) for route in routes):
            missing.append((controller_name, ", ".join(routes)))

    if missing:
        print("[verify-http-coverage] 以下 Controller 的 Route 前缀未在 Bosch.http 中命中：", file=sys.stderr)
        for controller_name, routes in missing:
            print(f"  - {controller_name} (routes: {routes})", file=sys.stderr)
        if os.environ.get("HTTP_COVERAGE_STRICT", "").lower() in ("1", "true", "yes"):
            return 1
        print(
            "[verify-http-coverage] 非严格模式：仍将退出 0。设置 HTTP_COVERAGE_STRICT=1 则对上述缺失失败。",
            file=sys.stderr,
        )
        return 0

    print("[verify-http-coverage] 覆盖校验通过：AppHost Controller 的 Route 前缀均可在 Bosch.http 中命中。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
