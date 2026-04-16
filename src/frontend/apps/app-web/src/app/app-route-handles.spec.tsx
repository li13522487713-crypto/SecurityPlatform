// @vitest-environment jsdom

import { act } from "react-dom/test-utils";
import ReactDOM from "react-dom/client";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { useRouteConfig } from "../../../../packages/arch/bot-hooks-base/src/use-route-config";
import {
  ROOT_ROUTE_HANDLE,
  WORKSPACE_DASHBOARD_ROUTE_HANDLE,
  WORKSPACE_SHELL_ROUTE_HANDLE,
} from "./route-handles";

function RouteConfigProbe() {
  const config = useRouteConfig();

  return (
    <div>
      <span data-testid="require-auth">{String(Boolean(config.requireAuth))}</span>
      <span data-testid="has-sider">{String(Boolean(config.hasSider))}</span>
      <span data-testid="menu-key">{config.menuKey ?? ""}</span>
    </div>
  );
}

describe("app route handles", () => {
  it("通过 data router 的 handle 向 useRouteConfig 暴露元数据", () => {
    const container = document.createElement("div");
    const root = ReactDOM.createRoot(container);
    const router = createMemoryRouter(
      [
        {
          path: "/",
          element: <Outlet />,
          handle: ROOT_ROUTE_HANDLE,
          children: [
            {
              path: "org/:orgId/workspaces/:workspaceId",
              element: <Outlet />,
              handle: WORKSPACE_SHELL_ROUTE_HANDLE,
              children: [
                {
                  path: "dashboard",
                  element: <RouteConfigProbe />,
                  handle: WORKSPACE_DASHBOARD_ROUTE_HANDLE,
                },
              ],
            },
          ],
        },
      ],
      {
        initialEntries: ["/org/org-1/workspaces/ws-1/dashboard"],
      }
    );

    act(() => {
      root.render(<RouterProvider router={router} />);
    });

    expect(container.querySelector('[data-testid="require-auth"]')?.textContent).toBe("true");
    expect(container.querySelector('[data-testid="has-sider"]')?.textContent).toBe("true");
    expect(container.querySelector('[data-testid="menu-key"]')?.textContent).toBe("dashboard");
  });
});
