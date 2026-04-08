import { createI18n } from "vue-i18n";
import type { Locale as AntdLocale } from "ant-design-vue/es/locale";
import antdEnUS from "ant-design-vue/es/locale/en_US";
import antdZhCN from "ant-design-vue/es/locale/zh_CN";
import zhCN from "./zh-CN";
import enUS from "./en-US";
import { formatMissingI18nKey } from "./missing";

export type SupportedLocale = "zh-CN" | "en-US";

export const DEFAULT_LOCALE: SupportedLocale = "zh-CN";
export const SUPPORTED_LOCALES: readonly SupportedLocale[] = ["zh-CN", "en-US"] as const;

const LOCALE_STORAGE_KEY = "atlas_locale";

type MessageTree = Record<string, unknown>;

function detectBrowserLocale(): SupportedLocale {
  if (typeof navigator === "undefined") return DEFAULT_LOCALE;
  return navigator.language.toLowerCase().startsWith("zh") ? "zh-CN" : "en-US";
}

function normalizeLocale(value: string | null | undefined): SupportedLocale {
  if (value === "zh-CN" || value === "en-US") return value;
  return detectBrowserLocale();
}

function applyDocumentLocale(locale: SupportedLocale): void {
  if (typeof document === "undefined") return;
  document.documentElement.lang = locale;
}

export function getLocale(): SupportedLocale {
  if (typeof localStorage === "undefined") return detectBrowserLocale();
  return normalizeLocale(localStorage.getItem(LOCALE_STORAGE_KEY));
}

export function getActiveLocale(): SupportedLocale {
  const localeSource = i18n.global.locale as unknown as string | { value?: SupportedLocale };
  return typeof localeSource === "string"
    ? normalizeLocale(localeSource)
    : normalizeLocale(localeSource.value);
}

export function setLocale(locale: SupportedLocale): void {
  const next = normalizeLocale(locale);
  if (typeof localStorage !== "undefined") {
    localStorage.setItem(LOCALE_STORAGE_KEY, next);
  }
  (i18n.global.locale as unknown as { value: SupportedLocale }).value = next;
  applyDocumentLocale(next);
}

export function getLocaleLabel(locale: SupportedLocale): string {
  return locale === "zh-CN" ? "简体中文" : "English";
}

export function getAntdLocale(locale: SupportedLocale): AntdLocale {
  return locale === "en-US" ? antdEnUS : antdZhCN;
}

export const i18n = createI18n({
  legacy: false,
  locale: getLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  missing: (locale, key) => formatMissingI18nKey(String(locale), key),
  messages: {
    "zh-CN": zhCN as MessageTree,
    "en-US": enUS as MessageTree
  } as never
});

applyDocumentLocale(getLocale());

export default i18n;
