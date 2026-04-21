import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes, Navigate, useParams } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LOWCODE_APP_KEY } from '@atlas/app-shell-shared';
import { LowcodeStudioApp } from '@atlas/lowcode-studio-react';
import { AppListPage } from './pages/app-list-page';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, refetchOnWindowFocus: false }
  }
});

// 与 @atlas/app-shell-shared LOWCODE_ROUTES 对齐：/apps/lowcode/...
const ROOT = `/apps/${LOWCODE_APP_KEY}`;

const root = createRoot(document.getElementById('root') as HTMLElement);

function LowcodeStudioRoute() {
  const { appId = '' } = useParams();
  return <LowcodeStudioApp appId={appId} />;
}

root.render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Navigate to={ROOT} replace />} />
          <Route path={ROOT} element={<AppListPage />} />
          <Route path={`${ROOT}/:appId/studio`} element={<LowcodeStudioRoute />} />
          <Route path="*" element={<Navigate to={ROOT} replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);
