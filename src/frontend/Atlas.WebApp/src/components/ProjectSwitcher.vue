<template>
  <div v-if="enabled" class="project-switcher">
    <a-space>
      <span class="project-label">当前项目</span>
      <a-select
        v-model:value="selectedProjectId"
        :options="options"
        :loading="loading"
        show-search
        allow-clear
        placeholder="选择项目"
        option-filter-prop="label"
        style="min-width: 200px"
        @change="handleChange"
      />
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { message } from "ant-design-vue";
import { getCurrentAppConfig, getMyProjects } from "@/services/api";
import type { ProjectListItem } from "@/types/api";
import { clearProjectId, getProjectId, setProjectId, setProjectScopeEnabled } from "@/utils/auth";

const enabled = ref(false);
const loading = ref(false);
const options = ref<{ label: string; value: string }[]>([]);
const selectedProjectId = ref<string | undefined>(undefined);

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

    const projects = await getMyProjects();
    options.value = projects.map((item: ProjectListItem) => ({
      label: `${item.name}（${item.code}）`,
      value: item.id
    }));

    const stored = getProjectId();
    const hasStored = stored && options.value.some((item) => item.value === stored);
    if (hasStored) {
      selectedProjectId.value = stored ?? undefined;
    } else if (options.value.length > 0) {
      selectedProjectId.value = options.value[0].value;
      setProjectId(options.value[0].value);
      message.success("已默认选择项目，可在此切换");
    } else {
      clearProjectId();
      selectedProjectId.value = undefined;
      message.warning("当前账号未分配项目");
    }
  } catch (error) {
    message.error((error as Error).message || "加载项目失败");
  } finally {
    loading.value = false;
  }
};

const handleChange = (value?: string) => {
  if (!value) {
    clearProjectId();
    message.warning("项目为空，部分数据将无法加载");
    window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: null } }));
    return;
  }

  if (value === selectedProjectId.value) {
    return;
  }

  setProjectId(value);
  message.success("项目已切换");
  window.dispatchEvent(new CustomEvent("project-changed", { detail: { projectId: value } }));
};

onMounted(loadProjectContext);

onMounted(() => {
  window.addEventListener("app-config-changed", loadProjectContext);
});

onUnmounted(() => {
  window.removeEventListener("app-config-changed", loadProjectContext);
});
</script>

<style scoped>
.project-switcher {
  display: flex;
  align-items: center;
}

.project-label {
  color: #fff;
  opacity: 0.85;
}
</style>
