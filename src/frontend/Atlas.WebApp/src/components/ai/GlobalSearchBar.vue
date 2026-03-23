<template>
  <a-auto-complete
    v-model:value="keyword"
    :options="options"
    :style="{ width }"
    :placeholder="placeholderText"
    @search="handleSearchInput"
    @select="handleSelect"
  >
    <a-input-search :loading="loading" allow-clear @search="emitSearch" />
  </a-auto-complete>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { recordAiRecentEdit, searchAiGlobal } from "@/services/api-ai-search";

interface OptionItemMeta {
  key: string;
  label: string;
  path: string;
  resourceType: string;
  resourceId: number;
}

const { t } = useI18n();

const props = withDefaults(
  defineProps<{
    placeholder?: string;
    width?: string;
  }>(),
  {
    width: "420px",
    placeholder: undefined
  }
);

const placeholderText = computed(() => props.placeholder ?? t("ai.globalSearch.placeholder"));

const emit = defineEmits<{
  (event: "search", keyword: string): void;
}>();

const router = useRouter();
const keyword = ref("");
const options = ref<Array<{ value: string; label: string }>>([]);
const loading = ref(false);
let timer: number | undefined;
const optionMetaMap = ref<Record<string, OptionItemMeta>>({});

function emitSearch() {
  emit("search", keyword.value.trim());
}

async function fetchOptions(searchKeyword: string) {
  if (!searchKeyword) {
    options.value = [];
    optionMetaMap.value = {};
    return;
  }

  loading.value = true;
  try {
    const response = await searchAiGlobal(searchKeyword, 20);
    const map: Record<string, OptionItemMeta> = {};
    options.value = response.items.map((item) => {
      const optionKey = `${item.resourceType}-${item.resourceId}`;
      map[optionKey] = {
        key: optionKey,
        label: item.title,
        path: item.path,
        resourceType: item.resourceType,
        resourceId: item.resourceId
      };
      return {
        value: optionKey,
        label: `[${item.resourceType}] ${item.title}`
      };
    });
    optionMetaMap.value = map;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.globalSearch.searchFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearchInput(value: string) {
  keyword.value = value;
  if (timer) {
    window.clearTimeout(timer);
  }

  timer = window.setTimeout(() => {
    void fetchOptions(value.trim());
  }, 280);
}

async function handleSelect(value: string) {
  const target = optionMetaMap.value[value];
  if (!target) {
    return;
  }

  try {
    await recordAiRecentEdit({
      resourceType: target.resourceType,
      resourceId: target.resourceId,
      title: target.label,
      path: target.path
    });
  } catch {
    // ignore record failures to avoid blocking navigation
  }

  await router.push(target.path);
}

onBeforeUnmount(() => {
  if (timer) {
    window.clearTimeout(timer);
  }
});
</script>
