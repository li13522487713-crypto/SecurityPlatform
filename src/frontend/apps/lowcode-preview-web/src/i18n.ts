/**
 * Preview app i18n 词条（中英对齐）。
 *
 * 与 lowcode-studio-web 共用 atlas_locale 持久化键，确保用户在 Studio 切换语言后预览页同步。
 */

export type Locale = 'zh-CN' | 'en-US';

export const PREVIEW_MESSAGES = {
  'zh-CN': {
    'preview.title': '预览',
    'preview.empty': '该应用暂无页面',
    'preview.loadFailed': '加载失败',
    'preview.deviceDesktop': '桌面 1280x800',
    'preview.deviceTablet': 'iPad 1024x768',
    'preview.deviceMobile': 'iPhone 375x812',
    'preview.routerFallback': 'Atlas Lowcode Preview — 请通过 /preview/:appId 进入'
  },
  'en-US': {
    'preview.title': 'Preview',
    'preview.empty': 'This app has no pages',
    'preview.loadFailed': 'Load failed',
    'preview.deviceDesktop': 'Desktop 1280x800',
    'preview.deviceTablet': 'iPad 1024x768',
    'preview.deviceMobile': 'iPhone 375x812',
    'preview.routerFallback': 'Atlas Lowcode Preview — open via /preview/:appId'
  }
} as const satisfies Record<Locale, Record<string, string>>;

const currentLocale: Locale = (typeof localStorage !== 'undefined' && (localStorage.getItem('atlas_locale') as Locale)) || 'zh-CN';

export function t(key: keyof typeof PREVIEW_MESSAGES['zh-CN']): string {
  return PREVIEW_MESSAGES[currentLocale][key] ?? key;
}
