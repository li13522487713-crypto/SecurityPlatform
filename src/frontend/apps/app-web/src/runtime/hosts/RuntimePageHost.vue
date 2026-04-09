<template>
  <a-card :title="pageTitle" data-testid="app-runtime-page">
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else :description="t('runtimePage.emptyNoPage')" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import { getTenantId, getAuthProfile } from "@atlas/shared-core";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { provideRuntimeContext } from "@/runtime/context/runtime-context-provider";
import { useRuntimeContextStore } from "@/runtime/context/runtime-context-store";
import { bootstrapRuntime } from "@/runtime/bootstrap/bootstrap-runtime";
import { resolveBindings } from "@/runtime/bindings/binding-resolver";
import { applyAmisBindings } from "@/runtime/adapters/amis-binding-adapter";
import { runLifecycleHook } from "@/runtime/lifecycle/page-lifecycle-runner";
import type { PageLifecycleHooks } from "@/runtime/lifecycle/lifecycle-types";
import {
  createExecution,
  completeExecution,
  removeExecution,
} from "@/runtime/release/runtime-execution-tracker";
import { reportAuditEvent, startAuditReporter, stopAuditReporter } from "@/runtime/audit/runtime-audit-reporter";
import type { AmisSchema } from "@/types/amis";

const { t } = useI18n();
const route = useRoute();
provideRuntimeContext();
const contextStore = useRuntimeContextStore();

const isMounted = ref(false);
const currentExecutionId = ref<string | null>(null);
const lifecycleHooks = ref<PageLifecycleHooks | null>(null);
const isInitializingRuntime = ref(false);

onMounted(() => {
  isMounted.value = true;
  startAuditReporter();
});
onUnmounted(() => {
  isMounted.value = false;
  void runLifecycleHookSafe("onPageLeave", lifecycleHooks.value);
  if (currentExecutionId.value) {
    completeExecution(currentExecutionId.value, "success");
    reportAuditEvent({
      executionId: currentExecutionId.value,
      eventType: "page.leave",
    });
    removeExecution(currentExecutionId.value);
  }
  contextStore.resetContext();
  stopAuditReporter();
});

const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref(t("runtimePage.defaultTitle"));

const appKey = computed(() => String(route.params.appKey ?? ""));
const pageKey = computed(() => String(route.params.pageKey ?? ""));

async function runLifecycleHookSafe(
  hookName: keyof PageLifecycleHooks,
  hooks: PageLifecycleHooks | null,
) {
  if (!hooks) return;
  try {
    const result = await runLifecycleHook(hooks, hookName);
    if (!result.success && currentExecutionId.value) {
      reportAuditEvent({
        executionId: currentExecutionId.value,
        eventType: "runtime.error",
        payload: {
          hook: hookName,
          results: result.results,
        },
      });
    }
  } catch (error) {
    if (currentExecutionId.value) {
      reportAuditEvent({
        executionId: currentExecutionId.value,
        eventType: "runtime.error",
        payload: {
          hook: hookName,
          error: error instanceof Error ? error.message : "Runtime lifecycle hook failed",
        },
      });
    }
  }
}

async function loadRuntime() {
  if (!appKey.value || !pageKey.value) {
    schema.value = null;
    lifecycleHooks.value = null;
    return;
  }

  if (currentExecutionId.value) {
    if (!isInitializingRuntime.value) {
      await runLifecycleHookSafe("onRouteChanged", lifecycleHooks.value);
    }
    completeExecution(currentExecutionId.value, "success");
    removeExecution(currentExecutionId.value);
  }

  loading.value = true;
  isInitializingRuntime.value = true;
  lifecycleHooks.value = null;
  try {
    const { manifest, executionId } = await bootstrapRuntime(route);
    currentExecutionId.value = executionId;
    lifecycleHooks.value = manifest.lifecycle ?? null;

    const profile = getAuthProfile();
    createExecution({
      executionId,
      appKey: appKey.value,
      pageKey: pageKey.value,
      releaseId: manifest.releaseId,
      releaseVersion: manifest.releaseVersion,
      userId: profile?.id,
      tenantId: getTenantId() ?? undefined,
      traceId: executionId,
    });
    reportAuditEvent({
      executionId,
      eventType: "page.enter",
      payload: { appKey: appKey.value, pageKey: pageKey.value },
    });

    if (!isMounted.value) return;

    pageTitle.value = manifest.pageTitle ?? manifest.title ?? `${appKey.value} / ${pageKey.value}`;
    await runLifecycleHookSafe("onPageInit", lifecycleHooks.value);

    const parsedSchema = JSON.parse(manifest.schemaJson) as AmisSchema;
    const bindings = resolveBindings(parsedSchema, pageKey.value, appKey.value);
    applyAmisBindings(parsedSchema, bindings);
    schema.value = parsedSchema;
  } catch (error) {
    schema.value = null;
    await runLifecycleHookSafe("onError", lifecycleHooks.value);
    if (currentExecutionId.value) {
      completeExecution(currentExecutionId.value, "failed", {
        message: error instanceof Error ? error.message : "Unknown error",
      });
      reportAuditEvent({
        executionId: currentExecutionId.value,
        eventType: "runtime.error",
        payload: { error: error instanceof Error ? error.message : "Unknown error" },
      });
    }
    message.error(
      error instanceof Error ? error.message : t("runtimePage.loadFailed"),
    );
  } finally {
    loading.value = false;
    isInitializingRuntime.value = false;
  }
}

onMounted(() => {
  void loadRuntime();
});

watch([appKey, pageKey], () => {
  contextStore.resetContext();
  void loadRuntime();
});
</script>
