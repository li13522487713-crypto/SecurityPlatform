/**
 * Setup Console Mock 切换开关（M10/D6）。
 *
 * - 默认走 mock（保证 Playwright E2E 与 dev 流畅体验）。
 * - 用户在浏览器 Console 执行：
 *     localStorage.setItem("atlas_setup_console_real", "1")
 *   后刷新页面，即切到真实后端 setupConsoleApi。
 * - SSR 与单测环境下永远返回 false，避免破坏。
 */
const SWITCH_KEY = "atlas_setup_console_real";

export function shouldUseRealConsoleApi(): boolean {
  if (typeof window === "undefined") {
    return false;
  }
  try {
    return window.localStorage.getItem(SWITCH_KEY) === "1";
  } catch {
    return false;
  }
}

export function setUseRealConsoleApi(enabled: boolean): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    if (enabled) {
      window.localStorage.setItem(SWITCH_KEY, "1");
    } else {
      window.localStorage.removeItem(SWITCH_KEY);
    }
  } catch {
    // ignore
  }
}
