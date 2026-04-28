import { LowcodeStudioApp as SharedLowcodeStudioApp } from '@atlas/lowcode-studio-react';
import type { ReactElement } from 'react';
import { useParams } from 'react-router-dom';

export function StudioApp(): ReactElement {
  const { appId = '' } = useParams();
  return <SharedLowcodeStudioApp appId={appId} />;
}
