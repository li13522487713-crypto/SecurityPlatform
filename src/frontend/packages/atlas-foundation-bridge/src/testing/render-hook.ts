/*
 * 极简 renderHook：仅用于桥接包内的 vitest 测试，避免引入 @testing-library/react。
 * 通过 React 18 的 act + createRoot 在 jsdom 环境里模拟一次组件渲染，
 * 收集 hook 的当前返回值。
 */

import { act } from "react-dom/test-utils";
import { createElement, useEffect } from "react";
import ReactDOM from "react-dom/client";

interface RenderHookResult<T> {
  result: { current: T };
}

export function renderHook<T>(callback: () => T): RenderHookResult<T> {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const result = { current: undefined as unknown as T };

  function HookHost() {
    const value = callback();
    useEffect(() => {
      result.current = value;
    });
    return null;
  }

  act(() => {
    const root = ReactDOM.createRoot(container);
    root.render(createElement(HookHost));
  });

  // 同步收集 effect 中的 current 值
  if (result.current === (undefined as unknown as T)) {
    act(() => {
      // 触发一次空 effect flush
    });
  }

  return { result };
}
