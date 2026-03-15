import { createI18n } from "vue-i18n";
import type { Locale as AntdLocale } from "ant-design-vue/es/locale";
import antdEnUS from "ant-design-vue/es/locale/en_US";
import antdZhCN from "ant-design-vue/es/locale/zh_CN";
import legacyEnUS from "../locales/en";
import legacyZhCN from "../locales/zh";
import { extraMessages } from "./extra-messages";
import platformEnUS from "./en-US";
import platformZhCN from "./zh-CN";

export type SupportedLocale = "zh-CN" | "en-US";

type MessageTree = Record<string, unknown>;

export const DEFAULT_LOCALE: SupportedLocale = "zh-CN";
export const SUPPORTED_LOCALES: readonly SupportedLocale[] = ["zh-CN", "en-US"] as const;

const LOCALE_STORAGE_KEY = "atlas_locale";

function deepMergeMessages(target: MessageTree, source: MessageTree): MessageTree {
  const merged: MessageTree = { ...target };
  for (const [key, value] of Object.entries(source)) {
    const existing = merged[key];
    if (isPlainObject(existing) && isPlainObject(value)) {
      merged[key] = deepMergeMessages(existing, value);
      continue;
    }
    merged[key] = value;
  }
  return merged;
}

function isPlainObject(value: unknown): value is MessageTree {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function detectBrowserLocale(): SupportedLocale {
  if (typeof navigator === "undefined") {
    return DEFAULT_LOCALE;
  }
  return navigator.language.toLowerCase().startsWith("zh") ? "zh-CN" : "en-US";
}

function normalizeLocale(value: string | null | undefined): SupportedLocale {
  if (value === "zh-CN" || value === "en-US") {
    return value;
  }
  return detectBrowserLocale();
}

function applyDocumentLocale(locale: SupportedLocale): void {
  if (typeof document === "undefined") {
    return;
  }
  document.documentElement.lang = locale;
}

export function getLocale(): SupportedLocale {
  if (typeof localStorage === "undefined") {
    return detectBrowserLocale();
  }
  return normalizeLocale(localStorage.getItem(LOCALE_STORAGE_KEY));
}

export function getActiveLocale(): SupportedLocale {
  const localeSource = i18n.global.locale as unknown as string | { value?: SupportedLocale };
  return typeof localeSource === "string"
    ? normalizeLocale(localeSource)
    : normalizeLocale(localeSource.value);
}

export function setLocale(locale: SupportedLocale): void {
  const nextLocale = normalizeLocale(locale);
  if (typeof localStorage !== "undefined") {
    localStorage.setItem(LOCALE_STORAGE_KEY, nextLocale);
  }
  (i18n.global.locale as unknown as { value: SupportedLocale }).value = nextLocale;
  applyDocumentLocale(nextLocale);
}

export function getLocaleLabel(locale: SupportedLocale): string {
  return locale === "zh-CN" ? "简体中文" : "English";
}

export function getAntdLocale(locale: SupportedLocale): AntdLocale {
  return locale === "en-US" ? antdEnUS : antdZhCN;
}

const messages = {
  "zh-CN": deepMergeMessages(
    deepMergeMessages(legacyZhCN as MessageTree, platformZhCN as MessageTree),
    extraMessages["zh-CN"]
  ),
  "en-US": deepMergeMessages(
    deepMergeMessages(legacyEnUS as MessageTree, platformEnUS as MessageTree),
    extraMessages["en-US"]
  )
};

export const i18n = createI18n({
  legacy: false,
  locale: getLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  messages: messages as never
});

applyDocumentLocale(getActiveLocale());

export default i18n;
