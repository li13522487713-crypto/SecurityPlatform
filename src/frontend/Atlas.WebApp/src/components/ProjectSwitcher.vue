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
          placeholder="Switch project"
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
import { onMounted, onUnmounted, ref } from "vue";
import { message } from "ant-design-vue";
import { getCurrentAppConfig, getMyProjectsPaged } from "@/services/api";
import type { ProjectListItem } from "@/types/api";
import { clearProjectId, getProjectId, setProjectId, setProjectScopeEnabled } from "@/utils/auth";

const enabled = ref(false);
const loading = ref(false);
const options = ref<{ label: string; value: string }[]>([]);
const selectedProjectId = ref<string | undefined>(undefined);
let searchTimer: number | undefined;

const loadProjects = async (keyword?: string) => {
  const result = await getMyProjectsPaged({
    pageIndex: 1,
    pageSize: 20,
    keyword: keyword?.trim() || undefined
  });

  options.value = result.items.map((item: ProjectListItem) => ({
    label: `${item.name} (${item.code})`,
    value: item.id
  }));
};

const loadProjectContext = async () => {
  loading.value = true;
  try {
    const appConfig = await getCurrentAppConfig();
    const isEnabled = Boolean(appConfig?.enableProjectScope);
    setProjectScopeEnabled(isEnabled);
    enabled.value = isEnabled;

    if (!isEnabled) {
      clearProjectId();
      selectedProjectId.value = undefined;
      options.value = [];
      return;
    }

    await loadProjects();

    const stored = getProjectId();
    const hasStored = stored && options.value.some((item) => item.value === stored);
    if (hasStored) {
      selectedProjectId.value = stored ?? undefined;
      window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: stored } }));
    } else if (options.value.length > 0) {
      selectedProjectId.value = options.value[0].value;
      setProjectId(options.value[0].value);
      window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: options.value[0].value } }));
      message.success("Default project selected");
    } else {
      clearProjectId();
      selectedProjectId.value = undefined;
      window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: null } }));
      message.warning("No project assigned");
    }
  } catch (error) {
    message.error((error as Error).message || "Failed to load projects");
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
        message.error((error as Error).message || "Failed to load projects");
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
      message.error((error as Error).message || "Failed to load projects");
    })
    .finally(() => {
      loading.value = false;
    });
};

const handleChange = (value?: string) => {
  const previous = getProjectId();

  if (!value) {
    clearProjectId();
    message.warning("Project cleared");
    window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: null } }));
    return;
  }

  if (value === previous) {
    return;
  }

  setProjectId(value);
  selectedProjectId.value = value;
  message.success("Project switched");
  window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: value } }));
};

onMounted(loadProjectContext);

onMounted(() => {
  window.addEventListener("app-config-changed", loadProjectContext);
});

onUnmounted(() => {
  window.removeEventListener("app-config-changed", loadProjectContext);
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
