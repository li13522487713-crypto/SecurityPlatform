import { useEffect, useRef } from 'react';
import { Toast } from '@douyinfe/semi-ui';
import type { AppSchema } from '@atlas/lowcode-schema';
import { lowcodeApi } from '../services/api-core';
import { t } from '../i18n';

/**
 * P1-3 修复（PLAN §M04 S04-1 + S04-2）：
 *  - 30s 去抖 autosave：拉当前 draft → POST autosave 写回（保持服务端 latest 与本地一致）
 *  - draftLock 心跳：每 30s 调用 renew，与服务端 60s TTL 配合保持当前会话独占
 *  - sessionId 每个浏览器 tab 一个；卸载时 release
 *
 * 实际编辑流由各 Inspector / Canvas 组件直接 POST autosave；本 hook 用 fallback 兜底，
 * 避免长时间无操作导致版本与本地状态漂移、锁过期。
 */
export function useDraftAutosave(appId: string | undefined): void {
  const sessionIdRef = useRef<string | null>(null);
  const lastDraftJsonRef = useRef<string | null>(null);

  useEffect(() => {
    if (!appId) return;
    const sessionId = resolveDraftSessionId(appId);
    sessionIdRef.current = sessionId;
    let cancelled = false;

    // 启动时尝试获取锁；忽略冲突（多 tab 共存仍可只读）
    void lowcodeApi.draftLock.acquire(appId, sessionId).catch(() => undefined);

    // 30s 去抖 autosave 兜底（用于"无操作期间也保持 latest"），此处用 setInterval 保守实现；
    // 复杂的局部 schema 变更去抖由各编辑组件实时调用 autosave 实现。
    const autosaveTimer = window.setInterval(async () => {
      if (cancelled) {
        return;
      }
      try {
        const draft = await lowcodeApi.apps.getDraft(appId);
        // 仅当 schemaJson 与上次不同才写回（避免无意义重复写库 + 审计噪音）
        if (lastDraftJsonRef.current !== draft.schemaJson) {
          // 校验 JSON 合法性（防止设计器的本地缓存写入坏 JSON）
          try {
            JSON.parse(draft.schemaJson) as AppSchema;
          } catch {
            return;
          }
          await lowcodeApi.apps.autosave(appId, draft.schemaJson);
          lastDraftJsonRef.current = draft.schemaJson;
        }
      } catch {
        // autosave 兜底失败不打断编辑
      }
    }, 30_000);

    // 心跳 30s 一次（与 60s TTL 配合，保留 30s 容错窗口）
    const renewTimer = window.setInterval(() => {
      if (cancelled) {
        return;
      }
      void lowcodeApi.draftLock.renew(appId, sessionId).catch(() => undefined);
    }, 30_000);

    // 离开页面时释放锁
    const onBeforeUnload = () => {
      try {
        void lowcodeApi.draftLock.release(appId, sessionId);
      } catch {
        // ignore
      }
    };
    window.addEventListener('beforeunload', onBeforeUnload);

    return () => {
      cancelled = true;
      window.clearInterval(autosaveTimer);
      window.clearInterval(renewTimer);
      window.removeEventListener('beforeunload', onBeforeUnload);
      void lowcodeApi.draftLock.release(appId, sessionId).catch(() => undefined);
    };
  }, [appId]);
}

/**
 * 显式 autosave 帮助函数：供编辑组件在每次 schema 变更后调用（如属性面板 onChange）。
 * 使用 lodash debounce 模式：500ms 内多次调用合并；失败 Toast.error 但不打断流。
 */
let inflightTimer: number | null = null;
let inflightPayload: { appId: string; schemaJson: string } | null = null;

export function scheduleAutosave(appId: string, schemaJson: string): void {
  inflightPayload = { appId, schemaJson };
  if (inflightTimer !== null) {
    window.clearTimeout(inflightTimer);
  }
  inflightTimer = window.setTimeout(async () => {
    inflightTimer = null;
    const p = inflightPayload;
    inflightPayload = null;
    if (!p) return;
    try {
      await lowcodeApi.apps.autosave(p.appId, p.schemaJson);
    } catch (err) {
      Toast.error(t('lowcode_studio.common.autosaveFailed'));
      console.warn('[lowcode-studio] autosave failed', err);
    }
  }, 500);
}

function resolveDraftSessionId(appId: string): string {
  const storageKey = `atlas_lowcode_draft_session:${appId}`;
  if (typeof sessionStorage !== 'undefined') {
    const existing = sessionStorage.getItem(storageKey);
    if (existing) return existing;
  }
  const next = `studio-${Math.random().toString(36).slice(2)}-${Date.now()}`;
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.setItem(storageKey, next);
  }
  return next;
}
