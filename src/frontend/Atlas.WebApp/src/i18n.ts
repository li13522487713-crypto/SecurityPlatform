import { createI18n } from "vue-i18n";
import zh from "./locales/zh";
import en from "./locales/en";

const LOCALE_KEY = "atlas-locale";

export type MessageSchema = typeof zh;

export const i18n = createI18n<[MessageSchema], "zh-CN" | "en-US">({
  legacy: false,
  locale: (localStorage.getItem(LOCALE_KEY) as "zh-CN" | "en-US") ?? "zh-CN",
  fallbackLocale: "zh-CN",
  messages: {
    "zh-CN": zh,
    "en-US": en
  }
});

export function setLocale(locale: "zh-CN" | "en-US") {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (i18n.global.locale as any).value = locale;
  localStorage.setItem(LOCALE_KEY, locale);
  document.documentElement.lang = locale;
}

export function getLocale(): string {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return (i18n.global.locale as any).value as string;
}
