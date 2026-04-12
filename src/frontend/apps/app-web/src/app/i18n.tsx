import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { APP_MESSAGES, type AppLocale, type AppMessageKey } from "./messages";

const LOCALE_STORAGE_KEY = "atlas_locale";

interface AppI18nContextValue {
  locale: AppLocale;
  setLocale: (locale: AppLocale) => void;
  t: (key: AppMessageKey) => string;
}

const AppI18nContext = createContext<AppI18nContextValue | null>(null);

function getInitialLocale(): AppLocale {
  if (typeof window === "undefined") {
    return "zh-CN";
  }

  const saved = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  return saved === "en-US" ? "en-US" : "zh-CN";
}

export function AppI18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<AppLocale>(getInitialLocale);

  const value = useMemo<AppI18nContextValue>(() => ({
    locale,
    setLocale: (nextLocale) => {
      setLocaleState(nextLocale);
      if (typeof window !== "undefined") {
        window.localStorage.setItem(LOCALE_STORAGE_KEY, nextLocale);
      }
    },
    t: key => APP_MESSAGES[locale][key]
  }), [locale]);

  return (
    <AppI18nContext.Provider value={value}>
      {children}
    </AppI18nContext.Provider>
  );
}

export function useAppI18n() {
  const context = useContext(AppI18nContext);
  if (!context) {
    throw new Error("AppI18nProvider is missing.");
  }

  return context;
}
