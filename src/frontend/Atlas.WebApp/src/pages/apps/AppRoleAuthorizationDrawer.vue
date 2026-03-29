<template>
  <a-drawer
    :open="open"
    :title="t('appOrg.roleAuthCenterTitle', '角色授权中心')"
    placement="right"
    :width="1080"
    destroy-on-close
    @close="emit('close')"
  >
    <a-empty v-if="!role" :description="t('appsRoles.emptySelectRole')" />
    <RoleAssignPanel
      v-else
      :role-id="role.id"
      :role-code="role.code"
      :role-name="role.name"
      :members="members"
      :can-assign-permissions="canManageRoles"
      :can-assign-menus="canManageRoles"
      :can-manage-data-scope="canManageRoles"
      scope="app"
      :app-id="appId"
      @success="emit('success')"
    />
  </a-drawer>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import RoleAssignPanel from "@/components/system/roles/RoleAssignPanel.vue";
import type { TenantAppMemberListItem, TenantAppRoleListItem } from "@/types/platform-v2";

defineProps<{
  open: boolean;
  appId: string;
  role: TenantAppRoleListItem | null;
  members: TenantAppMemberListItem[];
  canManageRoles: boolean;
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "success"): void;
}>();

const { t } = useI18n();
</script>
