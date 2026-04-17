/**
 * 6 类内容参数（M05 C05-5）。
 *
 * 与 BindingSchema 区分：
 *  - text / image / data / link / media / ai 6 个分支，由 ContentParamSchemaZod 校验。
 *
 * 本模块提供：
 *  - 默认配置工厂
 *  - 字段类型矩阵（用于 propertyPanels 渲染时按 kind 切换）
 *  - 与 webview 白名单 / chatflow 流式联动的 hook 接口（仅类型定义）
 *
 * 详见 docs/lowcode-content-params-spec.md。
 */

import type { ContentParamKind, ContentParamSchema } from '@atlas/lowcode-schema';
import { CONTENT_PARAM_KINDS } from '@atlas/lowcode-schema';

export const KIND_LABEL: Readonly<Record<ContentParamKind, string>> = {
  text: '文案',
  image: '图片',
  data: '数据',
  link: '链接',
  media: '媒体',
  ai: 'AI 内容'
};

export function listKinds(): ReadonlyArray<ContentParamKind> {
  return CONTENT_PARAM_KINDS;
}

export function defaultContentParam(kind: ContentParamKind, code: string): ContentParamSchema {
  switch (kind) {
    case 'text':
      return { kind, code, mode: 'static', source: '' };
    case 'image':
      return { kind, code, mode: 'url', source: '' };
    case 'data':
      return {
        kind,
        code,
        source: { sourceType: 'static', valueType: 'array', value: [] }
      };
    case 'link':
      return { kind, code, linkType: 'internal', href: '/' };
    case 'media':
      return { kind, code, mediaType: 'video', url: '' };
    case 'ai':
      return { kind, code, mode: 'chatflow_stream' };
  }
}

/** 切换 kind 时构造新默认配置，保留 code 与 description。*/
export function switchKind(prev: ContentParamSchema, nextKind: ContentParamKind): ContentParamSchema {
  const next = defaultContentParam(nextKind, prev.code);
  return { ...next, description: prev.description };
}

/** link 类型的 webview 白名单校验 hook（M12 webview-policy 适配器最终落地）。*/
export interface LinkPolicyChecker {
  isAllowed(url: string): boolean;
}

const PERMISSIVE_CHECKER: LinkPolicyChecker = { isAllowed: () => true };
let currentChecker: LinkPolicyChecker = PERMISSIVE_CHECKER;

export function setLinkPolicyChecker(checker: LinkPolicyChecker): void {
  currentChecker = checker;
}

export function ensureExternalLinkAllowed(href: string): { allowed: boolean; reason?: string } {
  const allowed = currentChecker.isAllowed(href);
  return allowed ? { allowed: true } : { allowed: false, reason: 'webview 白名单未允许该域名' };
}
