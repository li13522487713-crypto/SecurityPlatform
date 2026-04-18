/**
 * Mini Host app i18n 词条（中英对齐）。
 * 共用 atlas_locale 持久化键，与 lowcode-studio-web / lowcode-preview-web 一致。
 */

export type Locale = 'zh-CN' | 'en-US';

export const MINI_HOST_MESSAGES = {
  'zh-CN': {
    'mini.title': 'Atlas Lowcode Mini Host',
    'mini.currentRenderer': '当前渲染器',
    'mini.tip': '切换以查看降级提示',
    'mini.componentTree': '组件树',
    'mini.degraded': '降级（不支持）',
    'mini.supported': '支持',
    'mini.h5OnlyNote': '本壳层为 H5 预览；微信 / 抖音小程序 build 由 Taro CLI 在运维流水线执行：'
  },
  'en-US': {
    'mini.title': 'Atlas Lowcode Mini Host',
    'mini.currentRenderer': 'Renderer',
    'mini.tip': 'Switch to see fallback hints',
    'mini.componentTree': 'Component tree',
    'mini.degraded': 'Degraded (unsupported)',
    'mini.supported': 'Supported',
    'mini.h5OnlyNote': 'This shell is the H5 preview; WeChat / Douyin mini-program builds are run by Taro CLI in the DevOps pipeline:'
  }
} as const satisfies Record<Locale, Record<string, string>>;

const currentLocale: Locale = (typeof localStorage !== 'undefined' && (localStorage.getItem('atlas_locale') as Locale)) || 'zh-CN';

export function t(key: keyof typeof MINI_HOST_MESSAGES['zh-CN']): string {
  return MINI_HOST_MESSAGES[currentLocale][key] ?? key;
}
