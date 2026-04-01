import { createI18n } from "vue-i18n";
import type { Locale as AntdLocale } from "ant-design-vue/es/locale";
import antdEnUS from "ant-design-vue/es/locale/en_US";
import antdZhCN from "ant-design-vue/es/locale/zh_CN";
import { extraMessages } from "./extra-messages";
import type { MessageTree } from "./runtime-message-types";

export type SupportedLocale = "zh-CN" | "en-US";

export const DEFAULT_LOCALE: SupportedLocale = "zh-CN";
export const SUPPORTED_LOCALES: readonly SupportedLocale[] = ["zh-CN", "en-US"] as const;

const LOCALE_STORAGE_KEY = "atlas_locale";
const loadedLocaleMessages = new Map<SupportedLocale, MessageTree>();

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

async function loadLocaleMessages(locale: SupportedLocale): Promise<MessageTree> {
  const cached = loadedLocaleMessages.get(locale);
  if (cached) {
    return cached;
  }

  const [legacyModule, platformModule, runtimeModule] = await Promise.all(
    locale === "en-US"
      ? [
        import("../locales/en"),
        import("./en-US"),
        import("./runtime-messages.en-US")
      ]
      : [
        import("../locales/zh"),
        import("./zh-CN"),
        import("./runtime-messages.zh-CN")
      ]
  );

  const legacyMessages = legacyModule.default as MessageTree;
  const platformMessages = platformModule.default as MessageTree;
  const runtimeMessages = runtimeModule.default as MessageTree;
  const merged = deepMergeMessages(
    deepMergeMessages(
      deepMergeMessages(legacyMessages, platformMessages),
      extraMessages[locale]
    ),
    runtimeMessages
  );

  loadedLocaleMessages.set(locale, merged);
  return merged;
}

export async function ensureLocaleMessages(locale: SupportedLocale): Promise<void> {
  const nextLocale = normalizeLocale(locale);
  if (i18n.global.availableLocales.includes(nextLocale)) {
    return;
  }

  const messages = await loadLocaleMessages(nextLocale);
  i18n.global.setLocaleMessage(nextLocale, messages as never);
}

export async function setLocale(locale: SupportedLocale): Promise<void> {
  const nextLocale = normalizeLocale(locale);
  await ensureLocaleMessages(nextLocale);
  if (typeof localStorage !== "undefined") {
    localStorage.setItem(LOCALE_STORAGE_KEY, nextLocale);
  }
  (i18n.global.locale as unknown as { value: SupportedLocale }).value = nextLocale;
  applyDocumentLocale(nextLocale);
  if (nextLocale !== DEFAULT_LOCALE) {
    void ensureLocaleMessages(DEFAULT_LOCALE);
  }
}

export function getLocaleLabel(locale: SupportedLocale): string {
  return locale === "zh-CN" ? "简体中文" : "English";
}

export function getAntdLocale(locale: SupportedLocale): AntdLocale {
  return locale === "en-US" ? antdEnUS : antdZhCN;
}

export function translate(key: string, params?: Record<string, unknown>): string {
  const translator = i18n.global as unknown as {
    t: (messageKey: string, values?: Record<string, unknown>) => string;
  };
  return translator.t(key, params);
}

export const i18n = createI18n({
  legacy: false,
  locale: getLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  messages: {} as never
});

applyDocumentLocale(getLocale());

export default i18n;
