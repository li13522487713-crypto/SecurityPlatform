import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { appSignPath, workspaceDevelopPath } from "../app-paths";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { useAuth } from "../auth-context";
import { rememberConfiguredAppKey } from "@/services/api-core";

function HomeLogoMark() {
  return (
    <div className="atlas-home-brand__icon" aria-hidden="true">
      <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.08)" stroke="#1677ff" strokeWidth="1.5" />
        <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.12)" stroke="#1677ff" strokeWidth="1.5" />
        <circle cx="16" cy="16" r="3" fill="#1677ff" />
      </svg>
    </div>
  );
}

export function HomePage() {
  const navigate = useNavigate();
  const auth = useAuth();
  const { t } = useAppI18n();
  const { loading, platformReady, appReady, appKey, spaceId } = useBootstrap();
  const [value, setValue] = useState(appKey);

  if (loading || auth.loading) {
    return (
      <div className="atlas-home-gateway">
        <div className="atlas-home-loading">{t("loading")}</div>
      </div>
    );
  }

  if (!platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (appKey) {
    return (
      <Navigate
        to={auth.isAuthenticated ? workspaceDevelopPath(appKey, spaceId) : appSignPath(appKey)}
        replace
      />
    );
  }

  return (
    <div className="atlas-home-gateway">
      <div className="atlas-home-gateway__content">
        <div className="atlas-home-brand">
          <HomeLogoMark />
          <h1 className="atlas-home-brand__title">Atlas AppWeb</h1>
          <p className="atlas-home-brand__subtitle">{t("homeSubtitle")}</p>
        </div>

        {!auth.isAuthenticated ? (
          <div className="atlas-home-form">
            <label className="atlas-home-form__label" htmlFor="atlas-home-appkey">
              {t("homeAppKey")}
            </label>
            <input
              id="atlas-home-appkey"
              className="atlas-input atlas-home-form__input"
              placeholder={t("homeAppKeyPlaceholder")}
              value={value}
              onChange={(event) => setValue(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && value.trim()) {
                  const nextAppKey = value.trim();
                  rememberConfiguredAppKey(nextAppKey);
                  navigate(appSignPath(nextAppKey), { replace: true });
                }
              }}
            />
            <button
              type="button"
              className="atlas-button atlas-button--primary atlas-button--block"
              disabled={!value.trim()}
              onClick={() => {
                const nextAppKey = value.trim();
                if (!nextAppKey) {
                  return;
                }
                rememberConfiguredAppKey(nextAppKey);
                navigate(appSignPath(nextAppKey), { replace: true });
              }}
            >
              {t("homeEnter")}
            </button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
