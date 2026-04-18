import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes, useParams } from 'react-router-dom';
import { PreviewShell } from './preview-shell';
import { t } from './i18n';

const PreviewRoute: React.FC = () => {
  const { appId } = useParams();
  return <PreviewShell appId={appId ?? ''} />;
};

const root = createRoot(document.getElementById('root') as HTMLElement);
root.render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/preview/:appId" element={<PreviewRoute />} />
        <Route path="*" element={<div style={{ padding: 24 }}>{t('preview.routerFallback')}</div>} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);
