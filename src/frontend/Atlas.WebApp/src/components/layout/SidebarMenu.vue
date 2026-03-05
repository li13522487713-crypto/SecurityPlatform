<template>
  <a-menu
    theme="dark"
    mode="inline"
    :selected-keys="selectedKeys"
    :open-keys="openKeys"
    @openChange="onOpenChange"
  >
    <SidebarItem
      v-for="item in menuTree"
      :key="item.path"
      :item="item"
      :base-path="item.path"
    />
  </a-menu>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { usePermissionStore } from "@/stores/permission";
import type { RouterVo } from "@/types/api";
import SidebarItem from "./SidebarItem.vue";

const permissionStore = usePermissionStore();
const route = useRoute();
const openKeys = ref<string[]>([]);

const sidebarRouters = computed<RouterVo[]>(() =>
  Array.isArray(permissionStore.sidebarRouters) ? permissionStore.sidebarRouters : []
);

const menuTree = computed(() =>
  sidebarRouters.value.filter((item: RouterVo) => item && !(item.hidden ?? false))
);

const selectedKeys = computed(() => [route.path]);

watch(
  () => route.path,
  () => {
    const first = route.path.split("/").filter(Boolean)[0];
    openKeys.value = first ? [`/${first}`] : [];
  },
  { immediate: true }
);

function onOpenChange(keys: string[]) {
  openKeys.value = keys;
}
</script>
