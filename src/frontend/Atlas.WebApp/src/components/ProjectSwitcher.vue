<template>
  <div v-if="enabled" class="project-switcher" data-testid="e2e-project-switcher">
    <a-space :size="4">
      <span class="project-icon">
        <svg viewBox="0 0 1024 1024" width="16" height="16" fill="currentColor">
          <path
            d="M880 312H512l-51.2-56.888a72 72 0 0 0-53.52-23.112H144c-39.768 0-72 32.232-72 72v536c0 39.768 32.232 72 72 72h736c39.768 0 72-32.232 72-72V384c0-39.768-32.232-72-72-72zM144 304h263.28l51.2 56.888c14.248 15.824 34.624 23.112 53.52 23.112H880v456H144V304z"
          />
        </svg>
      </span>
      <span data-testid="e2e-project-switcher-select">
        <a-select
          v-model:value="selectedProjectId"
          :options="options"
          :loading="loading"
          show-search
          :bordered="false"
          :placeholder="t('projectSwitcher.placeholder')"
          :filter-option="false"
          style="min-width: 160px; font-weight: 500"
          @search="handleSearch"
          @focus="handleFocus"
          @change="handleChange"
        />
      </span>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { getCurrentAppConfig, getMyProjectsPaged } from "@/services/api";
import type { ProjectListItem } from "@/types/api";
import { clearProjectId, getProjectId, setProjectId, setProjectScopeEnabled } from "@/utils/auth";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

const enabled = ref(false);
const loading = ref(false);
const options = ref<{ label: string; value: string }[]>([]);
const selectedProjectId = ref<string | undefined>(undefined);
const route = useRoute();
let searchTimer: number | undefined;
const PROJECT_CONTEXT_CACHE_TTL = 60_000;
let projectContextCache:
  | {
    expiresAt: number;
    enabled: boolean;
    options: { label: string; value: string }[];
  }
  | null = null;

const loadProjects = async (keyword?: string) => {
  const result  = await getMyProjectsPaged({
    pageIndex: 1,
    pageSize: 20,
    keyword: keyword?.trim() || undefined
  });

  if (!isMounted.value) return;

  options.value = result.items.map((item: ProjectListItem) => ({
    label: `${item.name} (${item.code})`,
    value: item.id
  }));
};

const emitProjectChanged = (projectId: string | null) => {
  window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId } }));
};

const resolvedAppId = computed(() => {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  if (routeAppId) {
    return routeAppId;
  }

  return getCurrentAppIdFromStorage() || "";
});

const loadProjectContext = async (skipEvent = false) => {
  const cached = projectContextCache;
  if (cached && cached.expiresAt > Date.now()) {
    enabled.value = cached.enabled;
    options.value = cached.options;
    setProjectScopeEnabled(cached.enabled);
    if (!cached.enabled) {
      clearProjectId();
      selectedProjectId.value = undefined;
      if (!skipEvent) {
        emitProjectChanged(null);
      }
      return;
    }
  }

  loading.value = true;
  try {
    const [appConfigResult, projectsResult] = await Promise.allSettled([
      getCurrentAppConfig(),
      getMyProjectsPaged({
        pageIndex: 1,
        pageSize: 20
      })
    ]);

    if (!isMounted.value) return;
    const appConfig = appConfigResult.status === "fulfilled" ? appConfigResult.value : null;
    const isEnabled = Boolean(appConfig?.enableProjectScope);
    setProjectScopeEnabled(isEnabled);
    enabled.value = isEnabled;

    if (!isEnabled) {
      clearProjectId();
      selectedProjectId.value = undefined;
      options.value = [];
      if (!skipEvent) emitProjectChanged(null);
      projectContextCache = {
        expiresAt: Date.now() + PROJECT_CONTEXT_CACHE_TTL,
        enabled: false,
        options: []
      };
      return;
    }

    if (projectsResult.status === "fulfilled") {
      options.value = projectsResult.value.items.map((item: ProjectListItem) => ({
        label: `${item.name} (${item.code})`,
        value: item.id
      }));
    } else {
      options.value = [];
      message.error((projectsResult.reason as Error).message || t("projectSwitcher.loadFailed"));
    }

    if (!isMounted.value) return;

    const stored = getProjectId();
    const hasStored = stored && options.value.some((item) => item.value === stored);
    if (hasStored) {
      selectedProjectId.value = stored ?? undefined;
      if (!skipEvent) emitProjectChanged(stored ?? null);
    } else if (options.value.length > 0) {
      selectedProjectId.value = options.value[0].value;
      setProjectId(options.value[0].value);
      if (!skipEvent) emitProjectChanged(options.value[0].value);
    } else {
      clearProjectId();
      selectedProjectId.value = undefined;
      if (!skipEvent) emitProjectChanged(null);
    }

    projectContextCache = {
      expiresAt: Date.now() + PROJECT_CONTEXT_CACHE_TTL,
      enabled: isEnabled,
      options: [...options.value]
    };
  } catch (error) {
    message.error((error as Error).message || t("projectSwitcher.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = (value: string) => {
  if (!enabled.value) {
    return;
  }

  if (searchTimer) {
    window.clearTimeout(searchTimer);
  }

  searchTimer = window.setTimeout(() => {
    loading.value = true;
    void loadProjects(value)
      .catch((error) => {
        message.error((error as Error).message || t("projectSwitcher.loadFailed"));
      })
      .finally(() => {
        loading.value = false;
      });
  }, 250);
};

const handleFocus = () => {
  if (!enabled.value || options.value.length > 0) {
    return;
  }

  loading.value = true;
  void loadProjects()
    .catch((error) => {
      message.error((error as Error).message || t("projectSwitcher.loadFailed"));
    })
    .finally(() => {
      loading.value = false;
    });
};

const handleChange = (value?: string) => {
  const previous = getProjectId();

  if (!value) {
    clearProjectId();
    message.warning(t("projectSwitcher.cleared"));
    emitProjectChanged(null);
    return;
  }

  if (value === previous) {
    return;
  }

  setProjectId(value);
  selectedProjectId.value = value;
  message.success(t("projectSwitcher.switched"));
  emitProjectChanged(value);
};

const handleAppConfigChanged = () => {
  projectContextCache = null;
  void loadProjectContext();
};

const tryLoadProjectContext = () => {
  if (document.visibilityState !== "visible") {
    return;
  }
  void loadProjectContext();
};

const handleVisibilityChange = () => {
  if (document.visibilityState !== "visible") {
    return;
  }
  if (!projectContextCache || projectContextCache.expiresAt <= Date.now()) {
    void loadProjectContext();
  }
};

let isFirstAppIdLoad = true;
watch(
  () => resolvedAppId.value,
  () => {
    const skipEvent = isFirstAppIdLoad;
    isFirstAppIdLoad = false;
    if (document.visibilityState === "visible") {
      void loadProjectContext(skipEvent);
    }
  }
);

onMounted(() => {
  window.addEventListener("app-config-changed", handleAppConfigChanged);
  document.addEventListener("visibilitychange", handleVisibilityChange);
  tryLoadProjectContext();
});

onUnmounted(() => {
  window.removeEventListener("app-config-changed", handleAppConfigChanged);
  document.removeEventListener("visibilitychange", handleVisibilityChange);
  if (searchTimer) {
    window.clearTimeout(searchTimer);
    searchTimer = undefined;
  }
});
</script>

<style scoped>
.project-switcher {
  display: flex;
  align-items: center;
  background: var(--color-bg-hover);
  padding: 0 8px 0 12px;
  border-radius: var(--border-radius-md);
  margin-left: 8px;
}

.project-icon {
  color: var(--color-primary);
  display: flex;
  align-items: center;
}
</style>
