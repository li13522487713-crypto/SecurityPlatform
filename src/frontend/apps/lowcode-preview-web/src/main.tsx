import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes, useParams } from 'react-router-dom';
import { Empty } from '@douyinfe/semi-ui';
import { PreviewShell } from './preview-shell';
import { t } from './i18n';

const PreviewRoute: React.FC = () => {
  const { appId } = useParams();
  return <PreviewShell appId={appId ?? ''} />;
};

const RouterFallback: React.FC = () => (
  <div style={{ padding: 24 }}>
    <Empty description={t('preview.routerFallback')} />
  </div>
);

const root = createRoot(document.getElementById('root') as HTMLElement);
root.render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/preview/:appId" element={<PreviewRoute />} />
        <Route path="*" element={<RouterFallback />} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);
