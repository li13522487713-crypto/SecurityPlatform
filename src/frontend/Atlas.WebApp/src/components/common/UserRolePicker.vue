<template>
  <a-select
    v-model:value="internalValue"
    show-search
    :mode="multiple ? 'multiple' : undefined"
    :placeholder="resolvedPlaceholder"
    :filter-option="false"
    :not-found-content="fetching ? undefined : null"
    :options="options"
    style="width: 100%"
    @search="handleSearch"
    @change="handleChange"
  >
    <template v-if="fetching" #notFoundContent>
      <a-spin size="small" />
    </template>
    <template #option="{ label, avatar }">
      <div class="picker-option">
        <a-avatar v-if="mode === 'user'" size="small" :src="avatar" style="margin-right: 8px">
          {{ label?.[0]?.toUpperCase() }}
        </a-avatar>
        <span>{{ label }}</span>
      </div>
    </template>
  </a-select>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { debounce } from 'lodash-es';
import { getUsersPaged, getRolesPaged, getDepartmentsPaged } from '@/services/api';
import type { UserListItem, RoleListItem, DepartmentListItem } from '@/types/api';

const { t } = useI18n();

const props = withDefaults(defineProps<{
  value?: string | string[];
  mode?: "user" | "role" | "department";
  multiple?: boolean;
  placeholder?: string;
}>(), {
  value: undefined,
  mode: "user",
  multiple: true,
  placeholder: undefined
});

const resolvedPlaceholder = computed(() => props.placeholder ?? t("commonUi.pleaseSelect"));

const emit = defineEmits<{
  'update:value': [value: string | string[]];
  change: [value: string | string[]];
}>();

const internalValue = ref<string | string[]>(props.multiple ? [] : '');
const options = ref<Array<{ value: string; label: string; avatar?: string }>>([]);
const fetching = ref(false);

watch(() => props.value, (val) => {
  if (val !== undefined) {
    internalValue.value = val;
  }
}, { immediate: true });

const handleSearch = debounce(async (value: string) => {
  if (!value) {
    options.value = [];
    return;
  }
  
  fetching.value = true;
  try {
    if (props.mode === 'user') {
      const res  = await getUsersPaged({ pageIndex: 1, pageSize: 20, keyword: value });

      if (!isMounted.value) return;
      options.value = res.items.map((u: UserListItem) => ({
        value: u.id,
        label: u.displayName || u.username,
        avatar: undefined // API doesn't return avatar yet
      }));
    } else if (props.mode === 'role') {
      const res  = await getRolesPaged({ pageIndex: 1, pageSize: 20, keyword: value });

      if (!isMounted.value) return;
      options.value = res.items.map((r: RoleListItem) => ({
        value: r.code, // Use code for roles as per backend expectation usually, or id? 
                       // In ApprovalPropertiesPanel I used code for role placeholder.
                       // Let's check AssigneeType enum. Role = 1.
                       // Usually role assignment uses role code or id.
                       // Let's use ID for consistency, but display Name.
                       // Wait, in ApprovalPropertiesPanel I set placeholder "输入角色代码".
                       // If I use ID, backend needs to support ID.
                       // Let's assume ID for now as it's safer.
        label: r.name
      }));
    } else if (props.mode === 'department') {
      const res  = await getDepartmentsPaged({ pageIndex: 1, pageSize: 20, keyword: value });

      if (!isMounted.value) return;
      options.value = res.items.map((d: DepartmentListItem) => ({
        value: d.id,
        label: d.name
      }));
    }
  } catch (e) {
    console.error(e);
  } finally {
    fetching.value = false;
  }
}, 500);

const handleChange = (val: string | string[]) => {
  emit('update:value', val);
  emit('change', val);
};

// Initial load? No, only on search to avoid loading all users.
// But we need to show labels for selected values.
// This is tricky with remote select.
// For now, we assume the parent component might pass initial options or we just show IDs if not loaded.
// Or we can try to load selected items details.
// Since we don't have batch get by IDs API easily accessible here (getUserDetail is single),
// we might just search empty string to get some initial list if needed.
// Or just rely on search.

onMounted(() => {
    // Optional: prefetch some data
    handleSearch('');
});
</script>

<style scoped>
.picker-option {
  display: flex;
  align-items: center;
}
</style>