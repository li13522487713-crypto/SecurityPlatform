/**
 * 【高级 VII-7.3 国际化】
 * useAmisLocale composable：与宿主 Vue I18n 的 locale 联动
 */
import { ref, computed, watch, type Ref, type ComputedRef } from "vue";

/** AMIS 支持的 locale 值 */
export type AmisLocaleValue = "zh-CN" | "en-US" | "de-DE" | "ja-JP";

/** 应用 locale 到 AMIS locale 的映射 */
const LOCALE_MAP: Record<string, AmisLocaleValue> = {
  "zh-CN": "zh-CN",
  "zh": "zh-CN",
  "zh-Hans": "zh-CN",
  "en-US": "en-US",
  "en": "en-US",
  "de-DE": "de-DE",
  "de": "de-DE",
  "ja-JP": "ja-JP",
  "ja": "ja-JP",
};

/**
 * 将应用 locale 标准化为 AMIS 支持的 locale
 */
export function normalizeAmisLocale(appLocale: string): AmisLocaleValue {
  return LOCALE_MAP[appLocale] ?? "zh-CN";
}

/**
 * useAmisLocale — 响应式 AMIS locale composable
 *
 * @description
 * 跟踪宿主应用的 locale 变化，自动转换为 AMIS 支持的 locale 值。
 * 可以传入 vue-i18n 的 locale ref，或手动传入一个 Ref。
 *
 * @example
 * ```ts
 * // 与 vue-i18n 联动
 * import { useI18n } from 'vue-i18n';
 * const { locale } = useI18n();
 * const { amisLocale } = useAmisLocale(locale);
 *
 * // 手动使用
 * const { amisLocale, setLocale } = useAmisLocale();
 * setLocale('en-US');
 * ```
 */
export function useAmisLocale(appLocale?: Ref<string>): {
  amisLocale: ComputedRef<AmisLocaleValue>;
  setLocale: (locale: string) => void;
  currentLocale: Ref<string>;
} {
  const currentLocale = ref(appLocale?.value ?? "zh-CN");

  // 如果传入了外部 locale ref，保持同步
  if (appLocale) {
    watch(appLocale, (newVal) => {
      currentLocale.value = newVal;
    });
  }

  const amisLocale = computed(() => normalizeAmisLocale(currentLocale.value));

  function setLocale(locale: string): void {
    currentLocale.value = locale;
  }

  return {
    amisLocale,
    setLocale,
    currentLocale,
  };
}
