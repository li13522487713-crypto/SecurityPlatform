<template>
  <div class="app-org-workspace">
    <aside class="org-sidebar">
      <div class="sidebar-brand">
        <span class="sidebar-brand-icon" aria-hidden="true">
          <TeamOutlined />
        </span>
        <span class="sidebar-brand-text">{{ t("appOrg.pageTitle") }}</span>
      </div>

      <div class="org-nav">
        <button
          type="button"
          class="nav-row"
          :class="{ active: sidebarKey === 'members' }"
          @click="selectSidebar('members')"
        >
          <UserOutlined class="nav-row-icon" />
          <span class="nav-row-label">{{ t("appOrg.navAllEmployees") }}</span>
          <RightOutlined v-if="sidebarKey === 'members'" class="nav-row-chevron" />
        </button>

        <div class="nav-section">
          <div class="nav-section-head">
            <ApartmentOutlined class="nav-section-icon" />
            <button type="button" class="nav-section-switch" @click="selectSidebar('departments')">
              {{ t("appOrg.sectionDepartments") }}
            </button>
            <a-button type="text" size="small" class="nav-section-gear" @click.stop="openEntityDrawer('departments')">
              <template #icon><SettingOutlined /></template>
            </a-button>
          </div>
          <a-tree
            v-if="departmentTreeData.length > 0"
            class="dept-tree"
            block-node
            :tree-data="departmentTreeData"
            :selected-keys="departmentSelectedKeys"
            :expanded-keys="departmentExpandedKeys"
            @select="onDepartmentTreeSelect"
            @expand="onDepartmentTreeExpand"
          />
          <div v-else class="nav-empty">{{ t("common.noData") }}</div>
        </div>

        <div class="nav-section">
          <div class="nav-section-head">
            <SafetyOutlined class="nav-section-icon" />
            <button type="button" class="nav-section-switch" @click="selectSidebar('roles')">
              {{ t("appOrg.sectionRoles") }}
            </button>
            <a-button type="text" size="small" class="nav-section-gear" @click.stop="openEntityDrawer('roles')">
              <template #icon><SettingOutlined /></template>
            </a-button>
          </div>
          <div class="nav-section-items">
            <button
              v-for="r in sortedRoles"
              :key="r.id"
              type="button"
              class="nav-row nav-row-sub"
              :class="{ active: sidebarKey === `role:${r.id}` }"
              @click="selectSidebar(`role:${r.id}`)"
            >
              <span class="nav-row-label">{{ r.name }}</span>
              <RightOutlined v-if="sidebarKey === `role:${r.id}`" class="nav-row-chevron" />
            </button>
            <div v-if="sortedRoles.length === 0" class="nav-empty">{{ t("common.noData") }}</div>
          </div>
        </div>

        <div class="nav-section">
          <div class="nav-section-head">
            <SolutionOutlined class="nav-section-icon" />
            <button type="button" class="nav-section-switch" @click="selectSidebar('positions')">
              {{ t("appOrg.sectionPositions") }}
            </button>
            <a-button type="text" size="small" class="nav-section-gear" @click.stop="openEntityDrawer('positions')">
              <template #icon><SettingOutlined /></template>
            </a-button>
          </div>
          <div class="nav-section-items">
            <button
              v-for="p in sortedPositions"
              :key="p.id"
              type="button"
              class="nav-row nav-row-sub"
              :class="{ active: sidebarKey === `pos:${p.id}` }"
              @click="selectSidebar(`pos:${p.id}`)"
            >
              <span class="nav-row-label">{{ p.name }}</span>
              <RightOutlined v-if="sidebarKey === `pos:${p.id}`" class="nav-row-chevron" />
            </button>
            <div v-if="sortedPositions.length === 0" class="nav-empty">{{ t("common.noData") }}</div>
          </div>
        </div>

        <div class="nav-section">
          <div class="nav-section-head">
            <FolderOutlined class="nav-section-icon" />
            <button type="button" class="nav-section-switch" @click="selectSidebar('projects')">
              {{ t("appOrg.sectionProjects") }}
            </button>
            <a-button type="text" size="small" class="nav-section-gear" @click.stop="openEntityDrawer('projects')">
              <template #icon><SettingOutlined /></template>
            </a-button>
          </div>
          <div class="nav-section-items">
            <button
              v-for="p in sortedProjects"
              :key="p.id"
              type="button"
              class="nav-row nav-row-sub"
              :class="{ active: sidebarKey === `proj:${p.id}` }"
              @click="selectSidebar(`proj:${p.id}`)"
            >
              <span class="nav-row-label">{{ p.name }}</span>
              <RightOutlined v-if="sidebarKey === `proj:${p.id}`" class="nav-row-chevron" />
            </button>
            <div v-if="sortedProjects.length === 0" class="nav-empty">{{ t("common.noData") }}</div>
          </div>
        </div>
      </div>
    </aside>

    <main class="org-main">
      <div class="main-header">
        <div>
          <div class="main-title">{{ currentMainTitle }}</div>
          <div class="main-subtitle">{{ currentMainSubtitle }}</div>
        </div>
        <a-space>
          <a-button v-if="mainPanel === 'members' && canManageRoles" @click="openEntityDrawer('roles')">
            <template #icon><SettingOutlined /></template>
            {{ t("appOrg.manageRoles") }}
          </a-button>
          <a-button v-if="mainPanel === 'members' && canManageMembers" type="primary" @click="openAddMemberModal">
            <template #icon><UserAddOutlined /></template>
            {{ t("appsUsers.addMember") }}
          </a-button>
          <a-button v-if="mainPanel === 'departments'" type="primary" @click="openEntityModal('departments')">
            {{ t("appsDepartments.newDept") }}
          </a-button>
          <a-button v-if="mainPanel === 'roles'" type="primary" @click="openEntityModal('roles')">
            {{ t("appsRoles.newRole") }}
          </a-button>
          <a-button v-if="mainPanel === 'positions'" type="primary" @click="openEntityModal('positions')">
            {{ t("appsPositions.newPosition") }}
          </a-button>
          <a-button v-if="mainPanel === 'projects'" type="primary" @click="openEntityModal('projects')">
            {{ t("appsProjects.newProject") }}
          </a-button>
        </a-space>
      </div>

      <div v-if="mainPanel === 'members'" class="toolbar-row">
        <a-input
          v-model:value="keyword"
          class="org-search"
          size="large"
          allow-clear
          :placeholder="t('appOrg.searchPlaceholder')"
          @press-enter="onMemberSearch"
        >
          <template #prefix>
            <SearchOutlined class="org-search-icon" />
          </template>
        </a-input>
        <span class="found-count">{{ t("appOrg.foundMembers", { count: memberFoundCount }) }}</span>
      </div>

      <div class="org-table-wrap">
        <a-table
          :row-key="mainRowKey"
          :columns="mainColumns"
          :data-source="mainDataSource"
          :loading="loading"
          :pagination="mainPagination"
          :bordered="false"
          class="org-table"
          @change="handleMainTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="mainPanel === 'members' && column.key === 'basic'">
              <div class="cell-basic">
                <a-avatar :size="36" class="org-avatar">{{ avatarText(record.displayName) }}</a-avatar>
                <div class="cell-basic-text">
                  <div class="cell-name">{{ record.displayName }}</div>
                  <div class="cell-handle">@{{ record.username }}</div>
                </div>
              </div>
            </template>
            <template v-else-if="mainPanel === 'members' && column.key === 'department'">
              <span class="cell-muted-tag">{{ t("appOrg.departmentPending") }}</span>
            </template>
            <template v-else-if="mainPanel === 'members' && column.key === 'position'">
              <span class="cell-muted-tag">{{ t("appOrg.positionPending") }}</span>
            </template>
            <template v-else-if="mainPanel === 'members' && column.key === 'project'">
              <a-space wrap :size="6">
                <span v-for="name in record.projectNames" :key="name" class="role-tag-outline">{{ name }}</span>
                <span v-if="record.projectNames.length === 0" class="cell-placeholder">{{ t("appOrg.projectPending") }}</span>
              </a-space>
            </template>
            <template v-else-if="mainPanel === 'members' && column.key === 'roles'">
              <a-space wrap :size="6">
                <span v-for="name in record.roleNames" :key="name" class="role-tag-outline">{{ name }}</span>
                <span v-if="record.roleNames.length === 0" class="cell-placeholder">{{ t("appsUsers.noRoles") }}</span>
              </a-space>
            </template>
            <template v-else-if="mainPanel === 'members' && column.key === 'status'">
              <span class="status-cell">
                <span :class="['status-dot', record.isActive ? 'on' : 'off']" />
                {{ record.isActive ? t("appOrg.statusNormal") : t("appOrg.statusDisabled") }}
              </span>
            </template>
            <template v-else-if="mainPanel === 'departments' && column.key === 'parentId'">
              {{ record.parentId === record.id ? "-" : getDepartmentParentLabel(departments, record.parentId) }}
            </template>
            <template v-else-if="mainPanel === 'positions' && column.key === 'isActive'">
              <a-tag :color="record.isActive ? 'green' : 'default'">
                {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
              </a-tag>
            </template>
            <template v-else-if="mainPanel === 'projects' && column.key === 'isActive'">
              <a-tag :color="record.isActive ? 'blue' : 'default'">
                {{ record.isActive ? t("appsProjects.active") : t("appsProjects.disabled") }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button
                  v-if="mainPanel === 'members' && canManageMembers && (canViewAppRoles || canViewAppProjects)"
                  type="link"
                  size="small"
                  @click="openEditMemberRoles(record)"
                >
                  {{ t("common.edit") }}
                </a-button>
                <a-button
                  v-else-if="mainPanel !== 'members'"
                  type="link"
                  size="small"
                  @click="openCurrentEntityModal(record)"
                >
                  {{ t("common.edit") }}
                </a-button>
                <a-popconfirm
                  v-if="mainPanel !== 'members' || canManageMembers"
                  :title="mainDeleteConfirmText"
                  :ok-text="t('common.delete')"
                  :cancel-text="t('common.cancel')"
                  @confirm="handleMainDelete(record)"
                >
                  <a-button type="link" size="small" danger>{{ t("common.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </div>
    </main>

    <a-drawer
      v-model:open="entityDrawerOpen"
      :title="entityDrawerTitle"
      placement="right"
      :width="720"
      destroy-on-close
      @close="entityDrawerOpen = false"
    >
      <a-table
        row-key="id"
        :columns="entityDrawerColumns"
        :data-source="entityDrawerDataSource"
        :loading="loading"
        :pagination="false"
        :bordered="entityDrawerKind === 'departments'"
        :scroll="entityDrawerScroll"
        :indent-size="entityDrawerKind === 'departments' ? 18 : 0"
        size="small"
        class="entity-drawer-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="entityDrawerKind === 'departments' && column.key === 'name'">
            <span class="dept-name-cell">
              <span>{{ record.name }}</span>
              <a-tag v-if="isRootDepartmentRecord(record)" color="blue">{{ t("appOrg.rootNodeTag") }}</a-tag>
            </span>
          </template>
          <template v-else-if="entityDrawerKind === 'departments' && column.key === 'code'">
            <span>{{ isRootDepartmentRecord(record) ? "-" : record.code }}</span>
          </template>
          <template v-if="entityDrawerKind === 'roles' && column.key === 'isSystem'">
            <a-tag :color="record.isSystem ? 'purple' : 'blue'">
              {{ record.isSystem ? t("appsRoles.typeSystem") : t("appsRoles.typeCustom") }}
            </a-tag>
          </template>
          <template v-else-if="entityDrawerKind === 'departments' && column.key === 'parentId'">
            {{ record.parentId === record.id ? "-" : getDepartmentParentLabel(departments, record.parentId) }}
          </template>
          <template v-else-if="entityDrawerKind === 'positions' && column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'green' : 'default'">
              {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-else-if="entityDrawerKind === 'projects' && column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'blue' : 'default'">
              {{ record.isActive ? t("appsProjects.active") : t("appsProjects.disabled") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="openEntityModal(entityDrawerKind, record)">{{ t("common.edit") }}</a-button>
              <a-popconfirm
                :title="entityDeleteConfirm(entityDrawerKind)"
                :ok-text="t('common.delete')"
                :cancel-text="t('common.cancel')"
                @confirm="handleDeleteEntity(entityDrawerKind, record)"
              >
                <a-button type="link" size="small" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
      <div class="drawer-footer-actions">
        <a-button type="primary" @click="openEntityModal(entityDrawerKind)">{{ entityDrawerCreateLabel }}</a-button>
      </div>
    </a-drawer>

    <a-modal
      v-model:open="addMemberOpen"
      :title="t('appOrg.createUserTitle')"
      :confirm-loading="submitting"
      :ok-text="t('common.create')"
      :cancel-text="t('common.cancel')"
      @ok="submitAddMember"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('systemUsers.username')" required>
          <a-input v-model:value="addMemberForm.username" />
        </a-form-item>
        <a-form-item :label="t('systemUsers.password')" required>
          <a-input-password v-model:value="addMemberForm.password" />
        </a-form-item>
        <a-form-item :label="t('systemUsers.displayName')" required>
          <a-input v-model:value="addMemberForm.displayName" />
        </a-form-item>
        <a-form-item :label="t('systemUsers.email')">
          <a-input v-model:value="addMemberForm.email" />
        </a-form-item>
        <a-form-item :label="t('systemUsers.phoneNumber')">
          <a-input v-model:value="addMemberForm.phoneNumber" />
        </a-form-item>
        <a-form-item :label="t('systemUsers.status')">
          <a-switch v-model:checked="addMemberForm.isActive" />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" :label="t('appsUsers.labelAppRoles')">
          <a-select
            v-model:value="addMemberForm.roleIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="addMemberRoleOptions"
            :loading="addMemberRoleOptionsLoading"
            :placeholder="t('appsUsers.rolePlaceholder')"
            @search="handleAddMemberRoleSearch"
            @focus="loadAddMemberRoleOptions()"
          />
        </a-form-item>
        <a-form-item v-if="canViewAppProjects" :label="t('appOrg.sectionProjects')">
          <a-select
            v-model:value="addMemberForm.projectIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="addMemberProjectOptions"
            :loading="addMemberProjectOptionsLoading"
            :placeholder="t('appOrg.projectPlaceholder')"
            @search="handleAddMemberProjectSearch"
            @focus="loadAddMemberProjectOptions()"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="editMemberRolesOpen"
      :title="t('appsUsers.modalRolesTitle')"
      :confirm-loading="submitting"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="submitEditMemberRoles"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('appsUsers.labelMemberUser')">
          <a-input :value="editingMemberDisplayName" disabled />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" :label="t('appsUsers.labelRoles')">
          <a-select v-model:value="editMemberRoleIds" mode="multiple" :options="roleOptions" />
        </a-form-item>
        <a-form-item v-if="canViewAppProjects" :label="t('appOrg.sectionProjects')">
          <a-select
            v-model:value="editMemberProjectIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="editMemberProjectOptions"
            :loading="editMemberProjectOptionsLoading"
            :placeholder="t('appOrg.projectPlaceholder')"
            @search="handleEditMemberProjectSearch"
            @focus="loadEditMemberProjectOptions()"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="entityModalOpen"
      :title="entityModalTitle"
      :confirm-loading="submitting"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="submitEntity"
    >
      <a-form layout="vertical">
        <template v-if="entityModalKind === 'roles'">
          <a-form-item :label="t('appsRoles.labelCode')" required>
            <a-input v-model:value="entityForm.code" :disabled="!!editingEntityId" />
          </a-form-item>
          <a-form-item :label="t('appsRoles.labelName')" required>
            <a-input v-model:value="entityForm.name" />
          </a-form-item>
          <a-form-item :label="t('appsRoles.labelDesc')">
            <a-input v-model:value="entityForm.description" />
          </a-form-item>
        </template>

        <template v-if="entityModalKind === 'departments'">
          <a-form-item :label="t('appsDepartments.labelName')" required>
            <a-input v-model:value="entityForm.name" />
          </a-form-item>
          <a-form-item :label="t('appsDepartments.labelCode')" required>
            <a-input v-model:value="entityForm.code" />
          </a-form-item>
          <a-form-item :label="t('appsDepartments.labelParent')" :extra="t('appOrg.parentAutoRootHint')">
            <a-select
              v-model:value="entityForm.parentId"
              allow-clear
              :placeholder="t('appOrg.parentPlaceholder')"
              :options="departmentParentOptions"
            />
          </a-form-item>
          <a-form-item :label="t('appsDepartments.labelSort')">
            <a-input-number v-model:value="entityForm.sortOrder" :min="0" :max="9999" style="width: 100%" />
          </a-form-item>
        </template>

        <template v-if="entityModalKind === 'positions'">
          <a-form-item :label="t('appsPositions.labelName')" required>
            <a-input v-model:value="entityForm.name" />
          </a-form-item>
          <a-form-item :label="t('appsPositions.labelCode')" required>
            <a-input v-model:value="entityForm.code" :disabled="!!editingEntityId" />
          </a-form-item>
          <a-form-item :label="t('appsPositions.labelDesc')">
            <a-textarea v-model:value="entityForm.description" :rows="2" />
          </a-form-item>
          <a-form-item :label="t('appsPositions.labelStatus')">
            <a-switch v-model:checked="entityForm.isActive" />
          </a-form-item>
          <a-form-item :label="t('appsPositions.labelSort')">
            <a-input-number v-model:value="entityForm.sortOrder" :min="0" :max="9999" style="width: 100%" />
          </a-form-item>
        </template>

        <template v-if="entityModalKind === 'projects'">
          <a-form-item :label="t('appsProjects.labelName')" required>
            <a-input v-model:value="entityForm.name" />
          </a-form-item>
          <a-form-item :label="t('appsProjects.labelCode')" required>
            <a-input v-model:value="entityForm.code" :disabled="!!editingEntityId" />
          </a-form-item>
          <a-form-item :label="t('appsProjects.labelDesc')">
            <a-textarea v-model:value="entityForm.description" :rows="2" />
          </a-form-item>
          <a-form-item :label="t('appsProjects.labelStatus')">
            <a-switch v-model:checked="entityForm.isActive" />
          </a-form-item>
        </template>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { useRoute } from "vue-router";
import {
  ApartmentOutlined,
  SolutionOutlined,
  FolderOutlined,
  RightOutlined,
  SafetyOutlined,
  SearchOutlined,
  SettingOutlined,
  TeamOutlined,
  UserAddOutlined,
  UserOutlined
} from "@ant-design/icons-vue";
import { debounce } from "@/utils/common";
import { isAdminRole } from "@/utils/auth";
import { useUserStore } from "@/stores/user";
import { getAppProjectsPaged, getTenantAppRolesPaged } from "@/services/api-app-members";
import {
  createOrganizationMemberUser,
  createOrganizationDepartment,
  createOrganizationPosition,
  createOrganizationProject,
  createOrganizationRole,
  deleteOrganizationDepartment,
  deleteOrganizationPosition,
  deleteOrganizationProject,
  deleteOrganizationRole,
  getAppOrganizationWorkspace,
  getDepartmentParentLabel,
  removeOrganizationMember,
  updateOrganizationDepartment,
  updateOrganizationMemberRoles,
  updateOrganizationPosition,
  updateOrganizationProject,
  updateOrganizationRole
} from "@/services/api-app-organization";
import type {
  AppDepartmentListItem,
  AppPositionListItem,
  AppProjectListItem,
  TenantAppMemberListItem,
  TenantAppRoleListItem
} from "@/types/platform-v2";

type EntityKind = "roles" | "departments" | "positions" | "projects";
type SelectOption = { label: string; value: string };
type DepartmentTreeRow = AppDepartmentListItem & {
  children?: DepartmentTreeRow[];
  childCount: number;
};

const { t } = useI18n();
const route = useRoute();
const userStore = useUserStore();
const appId = computed(() => String(route.params.appId ?? ""));

const sidebarKey = ref("members");
const loading = ref(false);
const submitting = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true
});

const members = ref<TenantAppMemberListItem[]>([]);
const roles = ref<TenantAppRoleListItem[]>([]);
const departments = ref<AppDepartmentListItem[]>([]);
const positions = ref<AppPositionListItem[]>([]);
const projects = ref<AppProjectListItem[]>([]);

const entityDrawerOpen = ref(false);
const entityDrawerKind = ref<EntityKind>("roles");

const addMemberOpen = ref(false);
const editMemberRolesOpen = ref(false);
const entityModalOpen = ref(false);
const entityModalKind = ref<EntityKind>("roles");
const editingMemberUserId = ref("");
const editingMemberDisplayName = ref("");
const editMemberRoleIds = ref<string[]>([]);
const editMemberProjectIds = ref<string[]>([]);
const editingEntityId = ref<string | null>(null);

const addMemberForm = reactive({
  username: "",
  password: "",
  displayName: "",
  email: "",
  phoneNumber: "",
  isActive: true,
  roleIds: [] as string[],
  projectIds: [] as string[]
});

const entityForm = reactive({
  code: "",
  name: "",
  description: "",
  parentId: undefined as string | undefined,
  sortOrder: 0,
  isActive: true
});

const roleOptions = computed<SelectOption[]>(() =>
  roles.value.map((role) => ({ label: `${role.name} (${role.code})`, value: role.id }))
);
const addMemberRoleOptions = ref<SelectOption[]>([]);
const addMemberRoleOptionsLoading = ref(false);
const addMemberProjectOptions = ref<SelectOption[]>([]);
const addMemberProjectOptionsLoading = ref(false);
const editMemberProjectOptions = ref<SelectOption[]>([]);
const editMemberProjectOptionsLoading = ref(false);

const departmentParentOptions = computed<SelectOption[]>(() =>
  departments.value
    .filter((item) => item.id !== editingEntityId.value)
    .map((item) => ({ value: item.id, label: item.name }))
);

const canManageMembers = computed(
  () => userStore.permissions.includes("apps:members:update") || isAdminRole(userStore.profile)
);
const canViewAppRoles = computed(
  () =>
    userStore.permissions.includes("apps:roles:view") ||
    userStore.permissions.includes("apps:roles:update") ||
    isAdminRole(userStore.profile)
);
const canViewAppProjects = computed(
  () =>
    userStore.permissions.includes("apps:projects:view") ||
    userStore.permissions.includes("apps:projects:update") ||
    isAdminRole(userStore.profile)
);
const canManageRoles = computed(
  () => userStore.permissions.includes("apps:roles:update") || isAdminRole(userStore.profile)
);

const sortedDepartments = computed(() =>
  [...departments.value].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
);
const sortedRoles = computed(() => [...roles.value].sort((a, b) => a.name.localeCompare(b.name)));
const sortedPositions = computed(() =>
  [...positions.value].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
);
const sortedProjects = computed(() => [...projects.value].sort((a, b) => a.name.localeCompare(b.name)));
const departmentExpandedKeys = ref<string[]>([]);

function toDeptKey(id: string) {
  return `dept:${id}`;
}

type OrgTreeNode = {
  key: string;
  title: string;
  children?: OrgTreeNode[];
};

const departmentSelectedKeys = computed(() => {
  const m = /^dept:(.+)$/.exec(sidebarKey.value);
  return m ? [m[1]] : [];
});

const departmentTreeData = computed<OrgTreeNode[]>(() => {
  const byParent = new Map<string, AppDepartmentListItem[]>();
  const allIds = new Set<string>();
  departments.value.forEach((d) => allIds.add(d.id));

  for (const d of departments.value) {
    if (!d.parentId || d.parentId === d.id || !allIds.has(d.parentId)) {
      continue;
    }
    const arr = byParent.get(d.parentId) ?? [];
    arr.push(d);
    byParent.set(d.parentId, arr);
  }

  const directChildrenCount = new Map<string, number>();
  byParent.forEach((children, parentId) => {
    directChildrenCount.set(parentId, children.length);
  });

  const roots = sortedDepartments.value.filter((d) => !d.parentId || d.parentId === d.id || !allIds.has(d.parentId));
  const visiting = new Set<string>();

  const buildNode = (dept: AppDepartmentListItem): OrgTreeNode => {
    if (visiting.has(dept.id)) {
      return { key: dept.id, title: dept.name };
    }
    visiting.add(dept.id);
    const children = (byParent.get(dept.id) ?? [])
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
      .map((child) => buildNode(child));
    visiting.delete(dept.id);

    const childCount = directChildrenCount.get(dept.id) ?? 0;
    return {
      key: dept.id,
      title: childCount > 0 ? `${dept.name} (${childCount})` : dept.name,
      children: children.length > 0 ? children : undefined
    };
  };

  return roots.map((root) => buildNode(root));
});

const departmentChildrenMap = computed(() => {
  const map = new Map<string, string[]>();
  const allIds = new Set<string>(departments.value.map((d) => d.id));
  for (const d of departments.value) {
    if (!d.parentId || d.parentId === d.id || !allIds.has(d.parentId)) {
      continue;
    }
    const list = map.get(d.parentId) ?? [];
    list.push(d.id);
    map.set(d.parentId, list);
  }
  return map;
});

const departmentDirectChildrenCount = computed(() => {
  const map = new Map<string, number>();
  departmentChildrenMap.value.forEach((children, parentId) => map.set(parentId, children.length));
  return map;
});

const departmentTreeRows = computed<DepartmentTreeRow[]>(() => {
  const byParent = new Map<string, AppDepartmentListItem[]>();
  const allIds = new Set<string>();
  departments.value.forEach((d) => allIds.add(d.id));

  for (const d of departments.value) {
    if (!d.parentId || d.parentId === d.id || !allIds.has(d.parentId)) {
      continue;
    }
    const arr = byParent.get(d.parentId) ?? [];
    arr.push(d);
    byParent.set(d.parentId, arr);
  }

  const roots = sortedDepartments.value.filter((d) => !d.parentId || d.parentId === d.id || !allIds.has(d.parentId));
  const visiting = new Set<string>();

  const buildRow = (dept: AppDepartmentListItem): DepartmentTreeRow => {
    if (visiting.has(dept.id)) {
      return { ...dept, childCount: 0 };
    }
    visiting.add(dept.id);
    const children = (byParent.get(dept.id) ?? [])
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
      .map((child) => buildRow(child));
    visiting.delete(dept.id);
    return {
      ...dept,
      childCount: departmentDirectChildrenCount.value.get(dept.id) ?? 0,
      children: children.length > 0 ? children : undefined
    };
  };

  return roots.map((root) => buildRow(root));
});

type MainPanel = "members" | EntityKind;

const mainPanel = computed<MainPanel>(() => {
  if (sidebarKey.value === "departments" || sidebarKey.value.startsWith("dept:")) return "departments";
  if (sidebarKey.value === "roles" || sidebarKey.value.startsWith("role:")) return "roles";
  if (sidebarKey.value === "positions" || sidebarKey.value.startsWith("pos:")) return "positions";
  if (sidebarKey.value === "projects" || sidebarKey.value.startsWith("proj:")) return "projects";
  return "members";
});

const selectedRoleId = computed(() => {
  const m = /^role:(.+)$/.exec(sidebarKey.value);
  return m ? m[1] : null;
});
const selectedDeptId = computed(() => {
  const m = /^dept:(.+)$/.exec(sidebarKey.value);
  return m ? m[1] : null;
});
const selectedDept = computed(() => {
  if (!selectedDeptId.value) return undefined;
  return sortedDepartments.value.find((d) => d.id === selectedDeptId.value);
});
const selectedPosition = computed(() => {
  const m = /^pos:(.+)$/.exec(sidebarKey.value);
  return m ? sortedPositions.value.find((p) => p.id === m[1]) : undefined;
});
const selectedProject = computed(() => {
  const m = /^proj:(.+)$/.exec(sidebarKey.value);
  return m ? projects.value.find((p) => p.id === m[1]) : undefined;
});

const selectedDepartmentSubtreeIds = computed(() => {
  if (!selectedDeptId.value) return null;
  const result = new Set<string>();
  const stack: string[] = [selectedDeptId.value];
  while (stack.length > 0) {
    const id = stack.pop() as string;
    if (result.has(id)) continue;
    result.add(id);
    const children = departmentChildrenMap.value.get(id) ?? [];
    for (const childId of children) stack.push(childId);
  }
  return result;
});

const displayDepartments = computed(() => {
  if (!selectedDepartmentSubtreeIds.value) return sortedDepartments.value;
  return sortedDepartments.value.filter((dept) => selectedDepartmentSubtreeIds.value?.has(dept.id));
});

const displayRoles = computed(() => {
  if (!selectedRoleId.value) return sortedRoles.value;
  return sortedRoles.value.filter((role) => role.id === selectedRoleId.value);
});

const displayPositions = computed(() => {
  if (!selectedPosition.value) return sortedPositions.value;
  return sortedPositions.value.filter((position) => position.id === selectedPosition.value?.id);
});

const displayProjects = computed(() => {
  if (!selectedProject.value) return sortedProjects.value;
  return sortedProjects.value.filter((project) => project.id === selectedProject.value?.id);
});

const memberFoundCount = computed(() => {
  return Number(pagination.total ?? 0);
});

const displayedMembers = computed(() => members.value);

const memberTablePagination = computed(() => pagination);

const memberColumns = computed<TableColumnsType<TenantAppMemberListItem>>(() => [
  { title: t("appOrg.colBasicInfo"), key: "basic", width: 260 },
  { title: t("appOrg.colDepartment"), key: "department", width: 140 },
  { title: t("appOrg.colPosition"), key: "position", width: 120 },
  { title: t("appOrg.colProject"), key: "project", minWidth: 180 },
  { title: t("appOrg.colRolePermissions"), key: "roles", minWidth: 200 },
  { title: t("appOrg.colStatus"), key: "status", width: 120 },
  { title: t("appsUsers.colActions"), key: "actions", width: 200, fixed: "right" }
]);

const departmentColumns = computed<TableColumnsType<AppDepartmentListItem>>(() => [
  { title: t("appsDepartments.colName"), dataIndex: "name", key: "name" },
  { title: t("appsDepartments.colCode"), dataIndex: "code", key: "code", width: 160 },
  { title: t("appsDepartments.colParent"), key: "parentId", width: 180 },
  { title: t("appOrg.childCount"), key: "childCount", width: 100 },
  { title: t("appsDepartments.colSort"), dataIndex: "sortOrder", key: "sortOrder", width: 90 },
  { title: t("appsDepartments.colActions"), key: "actions", width: 140, fixed: "right" }
]);

const roleColumns = computed<TableColumnsType<TenantAppRoleListItem>>(() => [
  { title: t("appsRoles.colCode"), dataIndex: "code", key: "code", width: 150 },
  { title: t("appsRoles.colName"), dataIndex: "name", key: "name", width: 180 },
  { title: t("appsRoles.colType"), key: "isSystem", width: 100 },
  { title: t("appsRoles.colMembers"), dataIndex: "memberCount", key: "memberCount", width: 100 },
  { title: t("appsRoles.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("appsRoles.colActions"), key: "actions", width: 140, fixed: "right" }
]);

const positionColumns = computed<TableColumnsType<AppPositionListItem>>(() => [
  { title: t("appsPositions.colName"), dataIndex: "name", key: "name" },
  { title: t("appsPositions.colCode"), dataIndex: "code", key: "code", width: 150 },
  { title: t("appsPositions.colDesc"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("appsPositions.colStatus"), key: "isActive", width: 100 },
  { title: t("appsPositions.colSort"), dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: t("appsPositions.colActions"), key: "actions", width: 140, fixed: "right" }
]);

const projectColumns = computed<TableColumnsType<AppProjectListItem>>(() => [
  { title: t("appsProjects.colName"), dataIndex: "name", key: "name" },
  { title: t("appsProjects.colCode"), dataIndex: "code", key: "code", width: 150 },
  { title: t("appsProjects.colDesc"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("appsProjects.colStatus"), key: "isActive", width: 100 },
  { title: t("appsProjects.colActions"), key: "actions", width: 140, fixed: "right" }
]);

const currentMainTitle = computed(() => {
  if (mainPanel.value === "members") return t("appOrg.navAllEmployees");
  if (mainPanel.value === "departments") return selectedDept.value?.name ?? t("appOrg.sectionDepartments");
  if (mainPanel.value === "roles") {
    const selected = selectedRoleId.value ? roles.value.find((r) => r.id === selectedRoleId.value) : undefined;
    return selected?.name ?? t("appOrg.sectionRoles");
  }
  if (mainPanel.value === "positions") return selectedPosition.value?.name ?? t("appOrg.sectionPositions");
  return selectedProject.value?.name ?? t("appOrg.sectionProjects");
});

const currentMainSubtitle = computed(() => {
  if (mainPanel.value === "members") return t("appOrg.subtitleAll");
  if (mainPanel.value === "departments") {
    return selectedDept.value ? t("appOrg.subtitleByDepartment", { name: selectedDept.value.name }) : t("appsDepartments.pageSubtitle");
  }
  if (mainPanel.value === "roles") {
    const selected = selectedRoleId.value ? roles.value.find((r) => r.id === selectedRoleId.value) : undefined;
    return selected ? t("appOrg.subtitleByRole", { name: selected.name }) : t("appsRoles.pageSubtitle");
  }
  if (mainPanel.value === "positions") {
    return selectedPosition.value ? t("appOrg.subtitleByPosition", { name: selectedPosition.value.name }) : t("appsPositions.pageSubtitle");
  }
  return selectedProject.value ? t("appOrg.subtitleProject", { name: selectedProject.value.name }) : t("appsProjects.pageSubtitle");
});

const mainRowKey = computed(() => (mainPanel.value === "members" ? "userId" : "id"));
const mainColumns = computed<TableColumnsType<any>>(() => {
  if (mainPanel.value === "members") return memberColumns.value;
  if (mainPanel.value === "departments") return departmentColumns.value;
  if (mainPanel.value === "roles") return roleColumns.value;
  if (mainPanel.value === "positions") return positionColumns.value;
  return projectColumns.value;
});
const mainDataSource = computed(() => {
  if (mainPanel.value === "members") return displayedMembers.value;
  if (mainPanel.value === "departments") {
    return displayDepartments.value.map((dept) => ({
      ...dept,
      childCount: departmentDirectChildrenCount.value.get(dept.id) ?? 0
    }));
  }
  if (mainPanel.value === "roles") return displayRoles.value;
  if (mainPanel.value === "positions") return displayPositions.value;
  return displayProjects.value;
});
const mainPagination = computed(() => (mainPanel.value === "members" ? memberTablePagination.value : false));
const mainDeleteConfirmText = computed(() => {
  if (mainPanel.value === "members") return t("appsUsers.removeConfirm");
  if (mainPanel.value === "departments") return t("appsDepartments.deleteConfirm");
  if (mainPanel.value === "roles") return t("appsRoles.deleteConfirm");
  if (mainPanel.value === "positions") return t("appsPositions.deleteConfirm");
  return t("appsProjects.deleteConfirm");
});

const entityDrawerTitle = computed(() => {
  if (entityDrawerKind.value === "roles") return t("appsRoles.pageTitle");
  if (entityDrawerKind.value === "departments") return t("appsDepartments.pageTitle");
  if (entityDrawerKind.value === "positions") return t("appsPositions.pageTitle");
  return t("appsProjects.pageTitle");
});

const entityDrawerCreateLabel = computed(() => {
  if (entityDrawerKind.value === "roles") return t("appsRoles.newRole");
  if (entityDrawerKind.value === "departments") return t("appsDepartments.newDept");
  if (entityDrawerKind.value === "positions") return t("appsPositions.newPosition");
  return t("appsProjects.newProject");
});

const entityDrawerDataSource = computed(() => {
  const k = entityDrawerKind.value;
  if (k === "roles") return roles.value;
  if (k === "departments") return departmentTreeRows.value;
  if (k === "positions") return positions.value;
  return projects.value;
});

const entityDrawerScroll = computed(() => (entityDrawerKind.value === "departments" ? { x: 860 } : undefined));

const entityDrawerColumns = computed<TableColumnsType<Record<string, unknown>>>(() => {
  const k = entityDrawerKind.value;
  if (k === "roles") {
    return [
      { title: t("appsRoles.colCode"), dataIndex: "code", key: "code", width: 120 },
      { title: t("appsRoles.colName"), dataIndex: "name", key: "name", width: 140 },
      { title: t("appsRoles.colType"), key: "isSystem", width: 90 },
      { title: t("appsRoles.colMembers"), dataIndex: "memberCount", key: "memberCount", width: 80 },
      { title: t("appsRoles.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
      { title: t("appsRoles.colActions"), key: "actions", width: 120, fixed: "right" }
    ];
  }
  if (k === "departments") {
    return [
      { title: t("appsDepartments.colName"), dataIndex: "name", key: "name", width: 220 },
      { title: t("appsDepartments.colCode"), dataIndex: "code", key: "code", width: 260 },
      { title: t("appsDepartments.colParent"), key: "parentId", width: 140 },
      { title: t("appOrg.childCount"), dataIndex: "childCount", key: "childCount", width: 90 },
      { title: t("appsDepartments.colSort"), dataIndex: "sortOrder", key: "sortOrder", width: 72 },
      { title: t("appsDepartments.colActions"), key: "actions", width: 120, fixed: "right" }
    ];
  }
  if (k === "positions") {
    return [
      { title: t("appsPositions.colName"), dataIndex: "name", key: "name" },
      { title: t("appsPositions.colCode"), dataIndex: "code", key: "code", width: 120 },
      { title: t("appsPositions.colDesc"), dataIndex: "description", key: "description", ellipsis: true },
      { title: t("appsPositions.colStatus"), key: "isActive", width: 88 },
      { title: t("appsPositions.colSort"), dataIndex: "sortOrder", key: "sortOrder", width: 72 },
      { title: t("appsPositions.colActions"), key: "actions", width: 120, fixed: "right" }
    ];
  }
  return [
    { title: t("appsProjects.colName"), dataIndex: "name", key: "name" },
    { title: t("appsProjects.colCode"), dataIndex: "code", key: "code", width: 120 },
    { title: t("appsProjects.colDesc"), dataIndex: "description", key: "description", ellipsis: true },
    { title: t("appsProjects.colStatus"), key: "isActive", width: 88 },
    { title: t("appsProjects.colActions"), key: "actions", width: 120, fixed: "right" }
  ];
});

function entityDeleteConfirm(kind: EntityKind) {
  if (kind === "roles") return t("appsRoles.deleteConfirm");
  if (kind === "departments") return t("appsDepartments.deleteConfirm");
  if (kind === "positions") return t("appsPositions.deleteConfirm");
  return t("appsProjects.deleteConfirm");
}

function isRootDepartmentRecord(record: { id?: string; parentId?: string; code?: string }) {
  if (!record?.id) {
    return false;
  }
  return record.parentId === record.id || (record.code ?? "").startsWith("SYS_ROOT_");
}

const entityModalTitle = computed(() => {
  const edit = Boolean(editingEntityId.value);
  const k = entityModalKind.value;
  if (k === "roles") return edit ? t("appsRoles.modalEditTitle") : t("appsRoles.modalCreateTitle");
  if (k === "departments") return edit ? t("appsDepartments.modalEdit") : t("appsDepartments.modalCreate");
  if (k === "positions") return edit ? t("appsPositions.modalEdit") : t("appsPositions.modalCreate");
  return edit ? t("appsProjects.modalEdit") : t("appsProjects.modalCreate");
});

function avatarText(name: string) {
  const s = name?.trim();
  if (!s) return "?";
  return s.length > 1 ? s.slice(0, 1) : s;
}

function selectSidebar(key: string) {
  sidebarKey.value = key;
  pagination.current = 1;
  void loadWorkspace();
}

function onDepartmentTreeSelect(keys: Array<string | number>) {
  const selected = keys.length > 0 ? String(keys[0]) : "";
  if (!selected) {
    return;
  }
  selectSidebar(toDeptKey(selected));
}

function onDepartmentTreeExpand(keys: Array<string | number>) {
  departmentExpandedKeys.value = keys.map((key) => String(key));
}

function openEntityDrawer(kind: EntityKind) {
  entityDrawerKind.value = kind;
  entityDrawerOpen.value = true;
}

function onMemberSearch() {
  pagination.current = 1;
  void loadWorkspace();
}

async function loadWorkspace() {
  if (!appId.value) return;
  loading.value = true;
  try {
    const result = await getAppOrganizationWorkspace(appId.value, {
      pageIndex: Number(pagination.current ?? 1),
      pageSize: Number(pagination.pageSize ?? 20),
      keyword: keyword.value.trim() || undefined
    });

    members.value = result.members.items;
    pagination.total = result.members.total;
    pagination.current = result.members.pageIndex;
    pagination.pageSize = result.members.pageSize;
    roles.value = result.roles;
    departments.value = result.departments;
    positions.value = result.positions;
    projects.value = result.projects;
    if (departmentExpandedKeys.value.length === 0) {
      departmentExpandedKeys.value = departments.value.map((dept) => dept.id);
    }
  } catch (error) {
    message.error((error as Error).message || t("crud.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleMainTableChange(page: TablePaginationConfig) {
  pagination.current = page.current ?? 1;
  pagination.pageSize = page.pageSize ?? 20;
  if (mainPanel.value === "members") {
    void loadWorkspace();
  }
}

function openAddMemberModal() {
  addMemberForm.username = "";
  addMemberForm.password = "";
  addMemberForm.displayName = "";
  addMemberForm.email = "";
  addMemberForm.phoneNumber = "";
  addMemberForm.isActive = true;
  addMemberForm.roleIds = [];
  addMemberForm.projectIds = [];
  addMemberOpen.value = true;
  void loadAddMemberRoleOptions();
  void loadAddMemberProjectOptions();
}

async function loadAddMemberRoleOptions(keywordText?: string) {
  if (!appId.value || !canViewAppRoles.value) {
    addMemberRoleOptions.value = [];
    return;
  }
  addMemberRoleOptionsLoading.value = true;
  try {
    const page = await getTenantAppRolesPaged(appId.value, {
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });
    addMemberRoleOptions.value = page.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: item.id
    }));
  } finally {
    addMemberRoleOptionsLoading.value = false;
  }
}

const handleAddMemberRoleSearch = debounce((value?: string) => {
  void loadAddMemberRoleOptions(value);
}, 300);

async function loadAddMemberProjectOptions(keywordText?: string) {
  if (!appId.value || !canViewAppProjects.value) {
    addMemberProjectOptions.value = [];
    return;
  }
  addMemberProjectOptionsLoading.value = true;
  try {
    const page = await getAppProjectsPaged(appId.value, {
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });
    addMemberProjectOptions.value = page.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: item.id
    }));
  } finally {
    addMemberProjectOptionsLoading.value = false;
  }
}

const handleAddMemberProjectSearch = debounce((value?: string) => {
  void loadAddMemberProjectOptions(value);
}, 300);

async function loadEditMemberProjectOptions(keywordText?: string) {
  if (!appId.value || !canViewAppProjects.value) {
    editMemberProjectOptions.value = [];
    return;
  }
  editMemberProjectOptionsLoading.value = true;
  try {
    const page = await getAppProjectsPaged(appId.value, {
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });
    editMemberProjectOptions.value = page.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: item.id
    }));
  } finally {
    editMemberProjectOptionsLoading.value = false;
  }
}

const handleEditMemberProjectSearch = debounce((value?: string) => {
  void loadEditMemberProjectOptions(value);
}, 300);

function openEditMemberRoles(record: TenantAppMemberListItem) {
  editingMemberUserId.value = record.userId;
  editingMemberDisplayName.value = `${record.displayName} (${record.username})`;
  editMemberRoleIds.value = [...record.roleIds];
  editMemberProjectIds.value = [...record.projectIds];
  editMemberRolesOpen.value = true;
  void loadEditMemberProjectOptions();
}

async function submitAddMember() {
  if (!appId.value) return;
  if (!addMemberForm.username.trim()) {
    message.warning(t("systemUsers.usernameRequired"));
    return;
  }
  if (!addMemberForm.password.trim()) {
    message.warning(t("systemUsers.passwordRequired"));
    return;
  }
  if (!addMemberForm.displayName.trim()) {
    message.warning(t("systemUsers.displayNameRequired"));
    return;
  }
  submitting.value = true;
  try {
    await createOrganizationMemberUser(appId.value, {
      username: addMemberForm.username.trim(),
      password: addMemberForm.password,
      displayName: addMemberForm.displayName.trim(),
      email: addMemberForm.email.trim() || undefined,
      phoneNumber: addMemberForm.phoneNumber.trim() || undefined,
      isActive: addMemberForm.isActive,
      roleIds: canViewAppRoles.value ? addMemberForm.roleIds : [],
      projectIds: canViewAppProjects.value ? addMemberForm.projectIds : []
    });
    addMemberOpen.value = false;
    message.success(t("systemUsers.createSuccess"));
    await loadWorkspace();
  } catch (error) {
    message.error((error as Error).message || t("systemUsers.createFailed"));
  } finally {
    submitting.value = false;
  }
}

async function submitEditMemberRoles() {
  if (!appId.value || !editingMemberUserId.value) return;
  submitting.value = true;
  try {
    await updateOrganizationMemberRoles(appId.value, editingMemberUserId.value, {
      roleIds: editMemberRoleIds.value,
      projectIds: canViewAppProjects.value ? editMemberProjectIds.value : []
    });
    editMemberRolesOpen.value = false;
    message.success(t("appsUsers.rolesUpdated"));
    await loadWorkspace();
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.rolesUpdateFailed"));
  } finally {
    submitting.value = false;
  }
}

function resetEntityForm() {
  entityForm.code = "";
  entityForm.name = "";
  entityForm.description = "";
  entityForm.parentId = undefined;
  entityForm.sortOrder = 0;
  entityForm.isActive = true;
}

function openEntityModal(kind: EntityKind, record?: AppProjectListItem | TenantAppRoleListItem | AppDepartmentListItem | AppPositionListItem) {
  entityModalKind.value = kind;
  editingEntityId.value = record && "id" in record ? String(record.id) : null;
  resetEntityForm();
  if (record && "code" in record) {
    entityForm.code = record.code ?? "";
    entityForm.name = record.name ?? "";
    entityForm.description = "description" in record ? (record.description ?? "") : "";
    entityForm.parentId = "parentId" in record ? record.parentId : undefined;
    entityForm.sortOrder = "sortOrder" in record ? record.sortOrder ?? 0 : 0;
    entityForm.isActive = "isActive" in record ? record.isActive ?? true : true;
  }
  entityModalOpen.value = true;
}

function openCurrentEntityModal(record?: AppProjectListItem | TenantAppRoleListItem | AppDepartmentListItem | AppPositionListItem) {
  if (mainPanel.value === "members") {
    return;
  }
  openEntityModal(mainPanel.value, record);
}

async function submitEntity() {
  if (!appId.value) return;
  submitting.value = true;
  try {
    const k = entityModalKind.value;
    if (k === "roles") {
      if (!entityForm.code.trim() || !entityForm.name.trim()) {
        message.warning(t("appsRoles.fillRequired"));
        return;
      }
      if (editingEntityId.value) {
        await updateOrganizationRole(appId.value, editingEntityId.value, {
          name: entityForm.name.trim(),
          description: entityForm.description.trim() || undefined
        });
      } else {
        await createOrganizationRole(appId.value, {
          code: entityForm.code.trim(),
          name: entityForm.name.trim(),
          description: entityForm.description.trim() || undefined,
          permissionCodes: []
        });
      }
    } else if (k === "departments") {
      if (!entityForm.code.trim() || !entityForm.name.trim()) {
        message.warning(t("appsDepartments.fillNameCode"));
        return;
      }
      if (editingEntityId.value && entityForm.parentId === editingEntityId.value) {
        message.warning(t("appOrg.parentCannotSelf"));
        return;
      }
      const payload = {
        name: entityForm.name.trim(),
        code: entityForm.code.trim(),
        parentId: entityForm.parentId || undefined,
        sortOrder: entityForm.sortOrder
      };
      if (editingEntityId.value) {
        await updateOrganizationDepartment(appId.value, editingEntityId.value, payload);
      } else {
        await createOrganizationDepartment(appId.value, payload);
      }
    } else if (k === "positions") {
      if (!entityForm.code.trim() || !entityForm.name.trim()) {
        message.warning(t("appsPositions.fillNameCode"));
        return;
      }
      const payload = {
        name: entityForm.name.trim(),
        code: entityForm.code.trim(),
        description: entityForm.description.trim() || undefined,
        isActive: entityForm.isActive,
        sortOrder: entityForm.sortOrder
      };
      if (editingEntityId.value) {
        await updateOrganizationPosition(appId.value, editingEntityId.value, payload);
      } else {
        await createOrganizationPosition(appId.value, payload);
      }
    } else {
      if (!entityForm.code.trim() || !entityForm.name.trim()) {
        message.warning(t("appsProjects.fillNameCode"));
        return;
      }
      const payload = {
        code: entityForm.code.trim(),
        name: entityForm.name.trim(),
        description: entityForm.description.trim() || undefined,
        isActive: entityForm.isActive
      };
      if (editingEntityId.value) {
        await updateOrganizationProject(appId.value, editingEntityId.value, payload);
      } else {
        await createOrganizationProject(appId.value, payload);
      }
    }

    entityModalOpen.value = false;
    message.success(t("crud.saveSuccess"));
    await loadWorkspace();
  } catch (error) {
    message.error((error as Error).message || t("crud.saveFailed"));
  } finally {
    submitting.value = false;
  }
}

async function handleDeleteMember(record: TenantAppMemberListItem) {
  if (!appId.value) return;
  try {
    await removeOrganizationMember(appId.value, record.userId);
    message.success(t("appsUsers.removed"));
    await loadWorkspace();
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.removeFailed"));
  }
}

async function handleMainDelete(record: any) {
  if (mainPanel.value === "members") {
    await handleDeleteMember(record as TenantAppMemberListItem);
    return;
  }
  await handleDeleteEntity(mainPanel.value as EntityKind, record as { id: string });
}

async function handleDeleteEntity(kind: EntityKind, record: { id: string }) {
  if (!appId.value) return;
  try {
    if (kind === "roles") await deleteOrganizationRole(appId.value, record.id);
    else if (kind === "departments") await deleteOrganizationDepartment(appId.value, record.id);
    else if (kind === "positions") await deleteOrganizationPosition(appId.value, record.id);
    else await deleteOrganizationProject(appId.value, record.id);
    message.success(t("crud.deleteSuccess"));
    await loadWorkspace();
  } catch (error) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

onMounted(() => {
  void loadWorkspace();
});
</script>

<style scoped>
.app-org-workspace {
  display: flex;
  gap: 0;
  min-height: calc(100vh - 140px);
  background: #f5f5f5;
}

.org-sidebar {
  width: 268px;
  flex-shrink: 0;
  background: #f7f8fa;
  border-right: 1px solid #e8e8e8;
  padding: 16px 12px 24px;
  overflow-y: auto;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 20px;
  padding: 0 4px;
}

.sidebar-brand-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #e6f4ff;
  color: #1677ff;
  font-size: 18px;
}

.sidebar-brand-text {
  font-size: 16px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.88);
}

.org-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.nav-section {
  margin-top: 8px;
}

.nav-section-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px 4px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.nav-section-icon {
  font-size: 14px;
  color: #1677ff;
}

.nav-section-title {
  flex: 1;
  font-weight: 500;
  text-transform: none;
}

.nav-section-switch {
  flex: 1;
  border: none;
  padding: 0;
  margin: 0;
  background: transparent;
  text-align: left;
  color: inherit;
  cursor: pointer;
  font-size: 12px;
  font-weight: 500;
}

.nav-section-gear {
  color: rgba(0, 0, 0, 0.45) !important;
}

.nav-section-items {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav-row {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 10px;
  margin: 0;
  border: none;
  border-radius: 6px;
  background: transparent;
  text-align: left;
  cursor: pointer;
  color: rgba(0, 0, 0, 0.88);
  font-size: 14px;
  transition: background 0.15s ease;
}

.nav-row:hover {
  background: rgba(0, 0, 0, 0.04);
}

.nav-row.active {
  background: #e6f4ff;
  color: #1677ff;
  font-weight: 500;
}

.nav-row-sub {
  padding-left: 28px;
}

.nav-row-icon {
  color: #1677ff;
  font-size: 16px;
}

.nav-row-label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nav-row-chevron {
  font-size: 11px;
  color: #1677ff;
  flex-shrink: 0;
}

.nav-empty {
  padding: 8px 12px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.35);
}

.dept-tree {
  margin-top: 2px;
}

.dept-tree :deep(.ant-tree-node-content-wrapper) {
  min-height: 34px;
  line-height: 34px;
  border-radius: 6px;
}

.dept-tree :deep(.ant-tree-title) {
  font-size: 14px;
}

.org-main {
  flex: 1;
  min-width: 0;
  padding: 20px 24px 32px;
  background: #fff;
}

.main-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}

.main-title {
  font-size: 22px;
  font-weight: 600;
  line-height: 1.3;
  color: rgba(0, 0, 0, 0.88);
}

.main-subtitle {
  margin-top: 6px;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  line-height: 1.5;
}

.toolbar-row {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
}

.org-search {
  flex: 1;
  max-width: 520px;
  border-radius: 8px;
}

.org-search-icon {
  color: rgba(0, 0, 0, 0.35);
}

.found-count {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  white-space: nowrap;
}

.org-table-wrap {
  background: #fff;
  border-radius: 8px;
  border: 1px solid #f0f0f0;
  overflow: hidden;
}

.org-table :deep(.ant-table-thead > tr > th) {
  background: #fafafa !important;
  font-weight: 500;
  border-bottom: 1px solid #f0f0f0;
}

.org-table :deep(.ant-table-tbody > tr > td) {
  border-bottom: 1px solid #f5f5f5;
}

.org-table :deep(.ant-table-tbody > tr:last-child > td) {
  border-bottom: none;
}

.cell-basic {
  display: flex;
  align-items: center;
  gap: 12px;
}

.org-avatar {
  background: #1677ff !important;
  flex-shrink: 0;
}

.cell-basic-text {
  min-width: 0;
}

.cell-name {
  font-weight: 500;
  color: rgba(0, 0, 0, 0.88);
}

.cell-handle {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.cell-muted-tag {
  display: inline-block;
  padding: 2px 8px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  background: #f5f5f5;
  border-radius: 4px;
}

.role-tag-outline {
  display: inline-block;
  padding: 2px 8px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.75);
  background: #fff;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
}

.cell-placeholder {
  color: #bfbfbf;
  font-size: 13px;
}

.status-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.on {
  background: #52c41a;
}

.status-dot.off {
  background: #bfbfbf;
}

:deep(.org-row-selected > td) {
  background: #f6ffed !important;
}

.drawer-footer-actions {
  margin-top: 16px;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}

.entity-drawer-table :deep(.ant-table-cell) {
  white-space: nowrap;
}

.entity-drawer-table :deep(.ant-table-tbody > tr > td) {
  vertical-align: middle;
}

.dept-name-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
</style>
