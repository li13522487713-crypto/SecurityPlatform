import type { RendererType } from '../shared/enums';

/**
 * PublishedArtifact —— 发布产物元数据（docx §10.7，M17 完整使用）。
 */
export interface PublishedArtifact {
  id: string;
  appId: string;
  versionId: string;
  /** 三类产物。*/
  kind: 'hosted' | 'embedded-sdk' | 'preview';
  status: 'pending' | 'building' | 'ready' | 'failed' | 'revoked';
  /** SHA256 指纹（与版本绑定，PLAN.md §M17 S17-2）。*/
  fingerprint: string;
  publicUrl?: string;
  rendererMatrix: Partial<Record<RendererType, boolean>>;
  publishedByUserId: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}
