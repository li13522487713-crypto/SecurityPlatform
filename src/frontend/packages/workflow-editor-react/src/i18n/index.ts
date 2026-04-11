import i18next, { type i18n } from "i18next";
import { initReactI18next } from "react-i18next";
import { workflowEnUS, workflowZhCN } from "./messages";

const resources = {
  "zh-CN": { translation: workflowZhCN },
  "en-US": { translation: workflowEnUS }
};

let initialized = false;

export function ensureWorkflowI18n(locale: string): i18n {
  if (!initialized) {
    void i18next.use(initReactI18next).init({
      resources,
      lng: locale,
      fallbackLng: "zh-CN",
      interpolation: { escapeValue: false }
    });
    initialized = true;
  } else if (i18next.language !== locale) {
    void i18next.changeLanguage(locale);
  }
  return i18next;
}

