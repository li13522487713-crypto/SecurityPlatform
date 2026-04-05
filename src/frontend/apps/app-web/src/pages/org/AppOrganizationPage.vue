<template>
  <div class="app-organization">
    <a-page-header :title="t('org.pageTitle')" />

    <a-spin :spinning="initialLoading">
      <a-alert
        v-if="loadError"
        type="error"
        show-icon
        :message="loadError"
        closable
        class="org-error"
      />

      <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
        <!-- ==================== Members Tab ==================== -->
        <a-tab-pane key="members" :tab="t('org.tabMembers')">
          <div class="tab-toolbar">
            <a-space>
              <a-input-search
                v-model:value="memberKeyword"
                :placeholder="t('org.member.searchPlaceholder')"
                style="width: 260px"
                allow-clear
                @search="handleMemberSearch"
              />
              <a-select
                v-model:value="memberRoleFilter"
                :placeholder="t('org.member.roles')"
                allow-clear
                style="width: 180px"
                @change="handleMemberSearch"
              >
                <a-select-option
                  v-for="role in workspaceData?.roles ?? []"
                  :key="role.id"
                  :value="role.id"
                >
                  {{ role.name }}
                </a-select-option>
              </a-select>
            </a-space>
            <a-space>
              <a-button type="primary" @click="openAddMemberModal">
                {{ t('org.member.addTitle') }}
              </a-button>
            </a-space>
          </div>

          <a-table
            :columns="memberColumns"
            :data-source="memberList"
            :loading="memberLoading"
            :pagination="memberPagination"
            row-key="userId"
            size="middle"
            @change="handleMemberTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isActive'">
                <a-tag :color="record.isActive ? 'green' : 'default'">
                  {{ record.isActive ? t('org.member.active') : t('org.member.inactive') }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'roleNames'">
                <a-tag v-for="name in record.roleNames" :key="name" color="blue">{{ name }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-space>
                  <a-button type="link" size="small" @click="openEditRolesDrawer(record)">
                    {{ t('org.member.editRoles') }}
                  </a-button>
                  <a-button type="link" size="small" @click="openEditProfileDrawer(record)">
                    {{ t('org.member.editProfile') }}
                  </a-button>
                  <a-button type="link" size="small" @click="openResetPasswordModal(record)">
                    {{ t('org.member.resetPassword') }}
                  </a-button>
                  <a-popconfirm
                    :title="t('org.member.removeConfirm')"
                    @confirm="handleRemoveMember(record)"
                  >
                    <a-button type="link" size="small" danger>
                      {{ t('common.delete') }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- ==================== Roles Tab ==================== -->
        <a-tab-pane key="roles" :tab="t('org.tabRoles')">
          <div class="tab-toolbar">
            <span />
            <a-button type="primary" @click="openRoleDrawer()">
              {{ t('common.create') }}
            </a-button>
          </div>

          <a-table
            :columns="roleColumns"
            :data-source="workspaceData?.roles ?? []"
            row-key="id"
            size="middle"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isSystem'">
                {{ record.isSystem ? t('org.role.yes') : t('org.role.no') }}
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-space>
                  <a-button type="link" size="small" @click="openRoleDrawer(record)">
                    {{ t('common.edit') }}
                  </a-button>
                  <a-popconfirm
                    v-if="!record.isSystem"
                    :title="t('org.role.deleteConfirm')"
                    @confirm="handleDeleteRole(record)"
                  >
                    <a-button type="link" size="small" danger>
                      {{ t('common.delete') }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- ==================== Departments Tab ==================== -->
        <a-tab-pane key="departments" :tab="t('org.tabDepartments')">
          <div class="tab-toolbar">
            <span />
            <a-button type="primary" @click="openDeptDrawer()">
              {{ t('common.create') }}
            </a-button>
          </div>

          <a-table
            :columns="deptColumns"
            :data-source="workspaceData?.departments ?? []"
            row-key="id"
            size="middle"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'parentId'">
                {{ resolveDeptParentName(record.parentId) }}
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-space>
                  <a-button type="link" size="small" @click="openDeptDrawer(record)">
                    {{ t('common.edit') }}
                  </a-button>
                  <a-popconfirm
                    :title="t('org.department.deleteConfirm')"
                    @confirm="handleDeleteDept(record)"
                  >
                    <a-button type="link" size="small" danger>
                      {{ t('common.delete') }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- ==================== Positions Tab ==================== -->
        <a-tab-pane key="positions" :tab="t('org.tabPositions')">
          <div class="tab-toolbar">
            <span />
            <a-button type="primary" @click="openPosDrawer()">
              {{ t('common.create') }}
            </a-button>
          </div>

          <a-table
            :columns="posColumns"
            :data-source="workspaceData?.positions ?? []"
            row-key="id"
            size="middle"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isActive'">
                <a-tag :color="record.isActive ? 'green' : 'default'">
                  {{ record.isActive ? t('org.position.active') : t('org.position.inactive') }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-space>
                  <a-button type="link" size="small" @click="openPosDrawer(record)">
                    {{ t('common.edit') }}
                  </a-button>
                  <a-popconfirm
                    :title="t('org.position.deleteConfirm')"
                    @confirm="handleDeletePos(record)"
                  >
                    <a-button type="link" size="small" danger>
                      {{ t('common.delete') }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- ==================== Projects Tab ==================== -->
        <a-tab-pane key="projects" :tab="t('org.tabProjects')">
          <div class="tab-toolbar">
            <span />
            <a-button type="primary" @click="openProjDrawer()">
              {{ t('common.create') }}
            </a-button>
          </div>

          <a-table
            :columns="projColumns"
            :data-source="workspaceData?.projects ?? []"
            row-key="id"
            size="middle"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isActive'">
                <a-tag :color="record.isActive ? 'green' : 'default'">
                  {{ record.isActive ? t('org.project.active') : t('org.project.inactive') }}
                </a-tag>
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-space>
                  <a-button type="link" size="small" @click="openProjDrawer(record)">
                    {{ t('common.edit') }}
                  </a-button>
                  <a-popconfirm
                    :title="t('org.project.deleteConfirm')"
                    @confirm="handleDeleteProj(record)"
                  >
                    <a-button type="link" size="small" danger>
                      {{ t('common.delete') }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-spin>

    <!-- ==================== Add Member Modal ==================== -->
    <a-modal
      v-model:open="addMemberVisible"
      :title="t('org.member.addTitle')"
      :confirm-loading="addMemberSubmitting"
      @ok="handleAddMemberSubmit"
    >
      <a-tabs v-model:activeKey="addMemberMode">
        <a-tab-pane key="existing" :tab="t('org.member.addExisting')">
          <a-form layout="vertical">
            <a-form-item :label="t('org.member.selectRoles')">
              <a-select
                v-model:value="addMemberForm.roleIds"
                mode="multiple"
                :placeholder="t('org.member.selectRoles')"
              >
                <a-select-option
                  v-for="role in workspaceData?.roles ?? []"
                  :key="role.id"
                  :value="role.id"
                >
                  {{ role.name }}
                </a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('org.member.selectDepartments')">
              <a-select
                v-model:value="addMemberForm.departmentIds"
                mode="multiple"
                :placeholder="t('org.member.selectDepartments')"
              >
                <a-select-option
                  v-for="dept in workspaceData?.departments ?? []"
                  :key="dept.id"
                  :value="dept.id"
                >
                  {{ dept.name }}
                </a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('org.member.selectPositions')">
              <a-select
                v-model:value="addMemberForm.positionIds"
                mode="multiple"
                :placeholder="t('org.member.selectPositions')"
              >
                <a-select-option
                  v-for="pos in workspaceData?.positions ?? []"
                  :key="pos.id"
                  :value="pos.id"
                >
                  {{ pos.name }}
                </a-select-option>
              </a-select>
            </a-form-item>
          </a-form>
        </a-tab-pane>
        <a-tab-pane key="create" :tab="t('org.member.createNew')">
          <a-form layout="vertical" ref="createMemberFormRef" :model="createMemberForm" :rules="createMemberRules">
            <a-form-item :label="t('org.member.username')" name="username">
              <a-input v-model:value="createMemberForm.username" />
            </a-form-item>
            <a-form-item :label="t('auth.password')" name="password">
              <a-input-password v-model:value="createMemberForm.password" />
            </a-form-item>
            <a-form-item :label="t('org.member.displayName')" name="displayName">
              <a-input v-model:value="createMemberForm.displayName" />
            </a-form-item>
            <a-form-item :label="t('org.member.email')">
              <a-input v-model:value="createMemberForm.email" />
            </a-form-item>
            <a-form-item :label="t('org.member.phone')">
              <a-input v-model:value="createMemberForm.phoneNumber" />
            </a-form-item>
            <a-form-item :label="t('org.member.selectRoles')">
              <a-select
                v-model:value="createMemberForm.roleIds"
                mode="multiple"
                :placeholder="t('org.member.selectRoles')"
              >
                <a-select-option
                  v-for="role in workspaceData?.roles ?? []"
                  :key="role.id"
                  :value="role.id"
                >
                  {{ role.name }}
                </a-select-option>
              </a-select>
            </a-form-item>
          </a-form>
        </a-tab-pane>
      </a-tabs>
    </a-modal>

    <!-- ==================== Edit Roles Drawer ==================== -->
    <a-drawer
      v-model:open="editRolesVisible"
      :title="t('org.member.editRoles')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical">
        <a-form-item :label="t('org.member.selectRoles')">
          <a-select
            v-model:value="editRolesForm.roleIds"
            mode="multiple"
            :placeholder="t('org.member.selectRoles')"
          >
            <a-select-option
              v-for="role in workspaceData?.roles ?? []"
              :key="role.id"
              :value="role.id"
            >
              {{ role.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('org.member.selectDepartments')">
          <a-select
            v-model:value="editRolesForm.departmentIds"
            mode="multiple"
            :placeholder="t('org.member.selectDepartments')"
          >
            <a-select-option
              v-for="dept in workspaceData?.departments ?? []"
              :key="dept.id"
              :value="dept.id"
            >
              {{ dept.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('org.member.selectPositions')">
          <a-select
            v-model:value="editRolesForm.positionIds"
            mode="multiple"
            :placeholder="t('org.member.selectPositions')"
          >
            <a-select-option
              v-for="pos in workspaceData?.positions ?? []"
              :key="pos.id"
              :value="pos.id"
            >
              {{ pos.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('org.member.selectProjects')">
          <a-select
            v-model:value="editRolesForm.projectIds"
            mode="multiple"
            :placeholder="t('org.member.selectProjects')"
          >
            <a-select-option
              v-for="proj in workspaceData?.projects ?? []"
              :key="proj.id"
              :value="proj.id"
            >
              {{ proj.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="editRolesVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="editRolesSubmitting" @click="handleEditRolesSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- ==================== Edit Profile Drawer ==================== -->
    <a-drawer
      v-model:open="editProfileVisible"
      :title="t('org.member.editProfile')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical" :model="editProfileForm">
        <a-form-item :label="t('org.member.displayName')">
          <a-input v-model:value="editProfileForm.displayName" />
        </a-form-item>
        <a-form-item :label="t('org.member.email')">
          <a-input v-model:value="editProfileForm.email" />
        </a-form-item>
        <a-form-item :label="t('org.member.phone')">
          <a-input v-model:value="editProfileForm.phoneNumber" />
        </a-form-item>
        <a-form-item :label="t('org.member.status')">
          <a-switch v-model:checked="editProfileForm.isActive" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="editProfileVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="editProfileSubmitting" @click="handleEditProfileSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- ==================== Reset Password Modal ==================== -->
    <a-modal
      v-model:open="resetPwdVisible"
      :title="t('org.member.resetPassword')"
      :confirm-loading="resetPwdSubmitting"
      @ok="handleResetPasswordSubmit"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('org.member.newPassword')">
          <a-input-password v-model:value="resetPwdForm.newPassword" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- ==================== Role Drawer ==================== -->
    <a-drawer
      v-model:open="roleDrawerVisible"
      :title="roleEditingId ? t('org.role.editTitle') : t('org.role.createTitle')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical" ref="roleFormRef" :model="roleForm" :rules="roleFormRules">
        <a-form-item :label="t('org.role.name')" name="name">
          <a-input v-model:value="roleForm.name" />
        </a-form-item>
        <a-form-item v-if="!roleEditingId" :label="t('org.role.code')" name="code">
          <a-input v-model:value="roleForm.code" />
        </a-form-item>
        <a-form-item :label="t('org.role.description')">
          <a-textarea v-model:value="roleForm.description" :rows="3" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="roleDrawerVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="roleSubmitting" @click="handleRoleSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- ==================== Department Drawer ==================== -->
    <a-drawer
      v-model:open="deptDrawerVisible"
      :title="deptEditingId ? t('org.department.editTitle') : t('org.department.createTitle')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical" ref="deptFormRef" :model="deptForm" :rules="deptFormRules">
        <a-form-item :label="t('org.department.name')" name="name">
          <a-input v-model:value="deptForm.name" />
        </a-form-item>
        <a-form-item :label="t('org.department.code')" name="code">
          <a-input v-model:value="deptForm.code" />
        </a-form-item>
        <a-form-item :label="t('org.department.parent')">
          <a-select
            v-model:value="deptForm.parentId"
            :placeholder="t('org.department.none')"
            allow-clear
          >
            <a-select-option
              v-for="d in (workspaceData?.departments ?? []).filter(x => x.id !== deptEditingId)"
              :key="d.id"
              :value="d.id"
            >
              {{ d.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('org.department.sortOrder')">
          <a-input-number v-model:value="deptForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="deptDrawerVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="deptSubmitting" @click="handleDeptSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- ==================== Position Drawer ==================== -->
    <a-drawer
      v-model:open="posDrawerVisible"
      :title="posEditingId ? t('org.position.editTitle') : t('org.position.createTitle')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical" ref="posFormRef" :model="posForm" :rules="posFormRules">
        <a-form-item :label="t('org.position.name')" name="name">
          <a-input v-model:value="posForm.name" />
        </a-form-item>
        <a-form-item v-if="!posEditingId" :label="t('org.position.code')" name="code">
          <a-input v-model:value="posForm.code" />
        </a-form-item>
        <a-form-item :label="t('org.position.description')">
          <a-textarea v-model:value="posForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('org.position.isActive')">
          <a-switch v-model:checked="posForm.isActive" />
        </a-form-item>
        <a-form-item :label="t('org.position.sortOrder')">
          <a-input-number v-model:value="posForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="posDrawerVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="posSubmitting" @click="handlePosSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- ==================== Project Drawer ==================== -->
    <a-drawer
      v-model:open="projDrawerVisible"
      :title="projEditingId ? t('org.project.editTitle') : t('org.project.createTitle')"
      :width="480"
      destroy-on-close
    >
      <a-form layout="vertical" ref="projFormRef" :model="projForm" :rules="projFormRules">
        <a-form-item v-if="!projEditingId" :label="t('org.project.code')" name="code">
          <a-input v-model:value="projForm.code" />
        </a-form-item>
        <a-form-item :label="t('org.project.name')" name="name">
          <a-input v-model:value="projForm.name" />
        </a-form-item>
        <a-form-item :label="t('org.project.description')">
          <a-textarea v-model:value="projForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('org.project.isActive')">
          <a-switch v-model:checked="projForm.isActive" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="projDrawerVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="projSubmitting" @click="handleProjSubmit">
            {{ t('common.ok') }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue/es/form";
import { useAppContext } from "@/composables/useAppContext";
import {
  getOrganizationWorkspace,
  addMembers,
  createMemberUser,
  updateMemberRoles,
  removeMember,
  resetMemberPassword,
  updateMemberProfile,
  createRole,
  updateRole,
  deleteRole as deleteRoleApi,
  createDepartment,
  updateDepartment,
  deleteDepartment as deleteDeptApi,
  createPosition,
  updatePosition,
  deletePosition as deletePosApi,
  createProject,
  updateProject,
  deleteProject as deleteProjApi
} from "@/services/api-organization";
import type {
  AppOrganizationWorkspaceResponse,
  TenantAppMemberListItem,
  TenantAppRoleListItem,
  AppDepartmentListItem,
  AppPositionListItem,
  AppProjectListItem
} from "@/types/organization";

const { t } = useI18n();
const { appId } = useAppContext();

const activeTab = ref("members");
const initialLoading = ref(false);
const loadError = ref("");
const workspaceData = ref<AppOrganizationWorkspaceResponse | null>(null);

const memberKeyword = ref("");
const memberRoleFilter = ref<string | undefined>(undefined);
const memberLoading = ref(false);
const memberPagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `${total}`
});

const memberList = computed<TenantAppMemberListItem[]>(
  () => workspaceData.value?.members?.items ?? []
);

const memberColumns = computed(() => [
  { title: t("org.member.username"), dataIndex: "username", width: 120 },
  { title: t("org.member.displayName"), dataIndex: "displayName", width: 120 },
  { title: t("org.member.email"), dataIndex: "email", width: 160 },
  { title: t("org.member.phone"), dataIndex: "phoneNumber", width: 130 },
  { title: t("org.member.status"), dataIndex: "isActive", width: 80 },
  { title: t("org.member.roles"), dataIndex: "roleNames", width: 200 },
  { title: t("org.member.joinedAt"), dataIndex: "joinedAt", width: 160 },
  { title: t("common.actions"), dataIndex: "actions", width: 280, fixed: "right" as const }
]);

const roleColumns = computed(() => [
  { title: t("org.role.name"), dataIndex: "name", width: 150 },
  { title: t("org.role.code"), dataIndex: "code", width: 130 },
  { title: t("org.role.description"), dataIndex: "description", ellipsis: true },
  { title: t("org.role.isSystem"), dataIndex: "isSystem", width: 100 },
  { title: t("org.role.memberCount"), dataIndex: "memberCount", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 150, fixed: "right" as const }
]);

const deptColumns = computed(() => [
  { title: t("org.department.name"), dataIndex: "name", width: 180 },
  { title: t("org.department.code"), dataIndex: "code", width: 150 },
  { title: t("org.department.parent"), dataIndex: "parentId", width: 180 },
  { title: t("org.department.sortOrder"), dataIndex: "sortOrder", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 150, fixed: "right" as const }
]);

const posColumns = computed(() => [
  { title: t("org.position.name"), dataIndex: "name", width: 180 },
  { title: t("org.position.code"), dataIndex: "code", width: 150 },
  { title: t("org.position.description"), dataIndex: "description", ellipsis: true },
  { title: t("org.position.isActive"), dataIndex: "isActive", width: 100 },
  { title: t("org.position.sortOrder"), dataIndex: "sortOrder", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 150, fixed: "right" as const }
]);

const projColumns = computed(() => [
  { title: t("org.project.code"), dataIndex: "code", width: 150 },
  { title: t("org.project.name"), dataIndex: "name", width: 180 },
  { title: t("org.project.description"), dataIndex: "description", ellipsis: true },
  { title: t("org.project.isActive"), dataIndex: "isActive", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 150, fixed: "right" as const }
]);

async function loadWorkspace() {
  const id = appId.value;
  if (!id) return;

  initialLoading.value = true;
  loadError.value = "";
  try {
    workspaceData.value = await getOrganizationWorkspace(
      id,
      {
        pageIndex: memberPagination.current ?? 1,
        pageSize: memberPagination.pageSize ?? 20,
        keyword: memberKeyword.value || undefined,
        sortBy: undefined,
        sortDesc: false
      },
      memberRoleFilter.value
    );
    memberPagination.total = workspaceData.value.members.total;
    memberPagination.current = workspaceData.value.members.pageIndex;
  } catch (e) {
    loadError.value = e instanceof Error ? e.message : t("org.loadFailed");
  } finally {
    initialLoading.value = false;
  }
}

async function refreshMembers() {
  const id = appId.value;
  if (!id) return;

  memberLoading.value = true;
  try {
    workspaceData.value = await getOrganizationWorkspace(
      id,
      {
        pageIndex: memberPagination.current ?? 1,
        pageSize: memberPagination.pageSize ?? 20,
        keyword: memberKeyword.value || undefined,
        sortBy: undefined,
        sortDesc: false
      },
      memberRoleFilter.value
    );
    memberPagination.total = workspaceData.value.members.total;
    memberPagination.current = workspaceData.value.members.pageIndex;
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.loadFailed"));
  } finally {
    memberLoading.value = false;
  }
}

function handleMemberSearch() {
  memberPagination.current = 1;
  void refreshMembers();
}

function handleMemberTableChange(pag: TablePaginationConfig) {
  memberPagination.current = pag.current ?? 1;
  memberPagination.pageSize = pag.pageSize ?? 20;
  void refreshMembers();
}

function handleTabChange() {
  // workspace 数据已加载，tab 切换无需额外请求
}

function resolveDeptParentName(parentId: string | null): string {
  if (!parentId) return t("org.department.none");
  const dept = workspaceData.value?.departments?.find((d) => d.id === parentId);
  return dept?.name ?? parentId;
}

watch(appId, (id) => {
  if (id) void loadWorkspace();
});

onMounted(() => {
  if (appId.value) void loadWorkspace();
});

// ==================== Add Member ====================
const addMemberVisible = ref(false);
const addMemberMode = ref<"existing" | "create">("existing");
const addMemberSubmitting = ref(false);
const addMemberForm = reactive({
  userIds: [] as string[],
  roleIds: [] as string[],
  departmentIds: [] as string[],
  positionIds: [] as string[]
});
const createMemberFormRef = ref<FormInstance>();
const createMemberForm = reactive({
  username: "",
  password: "",
  displayName: "",
  email: "",
  phoneNumber: "",
  roleIds: [] as string[]
});
const createMemberRules = computed(() => ({
  username: [{ required: true, message: t("org.member.usernameRequired") }],
  password: [{ required: true, message: t("org.member.passwordRequired") }],
  displayName: [{ required: true, message: t("org.member.displayNameRequired") }]
}));

function openAddMemberModal() {
  addMemberMode.value = "existing";
  addMemberForm.userIds = [];
  addMemberForm.roleIds = [];
  addMemberForm.departmentIds = [];
  addMemberForm.positionIds = [];
  createMemberForm.username = "";
  createMemberForm.password = "";
  createMemberForm.displayName = "";
  createMemberForm.email = "";
  createMemberForm.phoneNumber = "";
  createMemberForm.roleIds = [];
  addMemberVisible.value = true;
}

async function handleAddMemberSubmit() {
  const id = appId.value;
  if (!id) return;

  addMemberSubmitting.value = true;
  try {
    if (addMemberMode.value === "create") {
      await createMemberFormRef.value?.validate();
      await createMemberUser(id, {
        username: createMemberForm.username,
        password: createMemberForm.password,
        displayName: createMemberForm.displayName,
        email: createMemberForm.email || undefined,
        phoneNumber: createMemberForm.phoneNumber || undefined,
        isActive: true,
        roleIds: createMemberForm.roleIds
      });
      message.success(t("org.member.createSuccess"));
    } else {
      if (addMemberForm.roleIds.length === 0) {
        message.warning(t("org.member.roleIdsRequired"));
        addMemberSubmitting.value = false;
        return;
      }
      await addMembers(id, {
        userIds: addMemberForm.userIds,
        roleIds: addMemberForm.roleIds,
        departmentIds: addMemberForm.departmentIds.length > 0 ? addMemberForm.departmentIds : undefined,
        positionIds: addMemberForm.positionIds.length > 0 ? addMemberForm.positionIds : undefined
      });
      message.success(t("org.member.addSuccess"));
    }
    addMemberVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(
      e instanceof Error
        ? e.message
        : addMemberMode.value === "create" ? t("org.member.createFailed") : t("org.member.addFailed")
    );
  } finally {
    addMemberSubmitting.value = false;
  }
}

// ==================== Edit Roles ====================
const editRolesVisible = ref(false);
const editRolesSubmitting = ref(false);
const editRolesUserId = ref("");
const editRolesForm = reactive({
  roleIds: [] as string[],
  departmentIds: [] as string[],
  positionIds: [] as string[],
  projectIds: [] as string[]
});

function openEditRolesDrawer(record: TenantAppMemberListItem) {
  editRolesUserId.value = record.userId;
  editRolesForm.roleIds = [...record.roleIds];
  editRolesForm.departmentIds = [...record.departmentIds];
  editRolesForm.positionIds = [...record.positionIds];
  editRolesForm.projectIds = [...record.projectIds];
  editRolesVisible.value = true;
}

async function handleEditRolesSubmit() {
  const id = appId.value;
  if (!id) return;

  editRolesSubmitting.value = true;
  try {
    await updateMemberRoles(id, editRolesUserId.value, {
      roleIds: editRolesForm.roleIds,
      departmentIds: editRolesForm.departmentIds.length > 0 ? editRolesForm.departmentIds : undefined,
      positionIds: editRolesForm.positionIds.length > 0 ? editRolesForm.positionIds : undefined,
      projectIds: editRolesForm.projectIds.length > 0 ? editRolesForm.projectIds : undefined
    });
    message.success(t("org.member.updateRolesSuccess"));
    editRolesVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.member.updateRolesFailed"));
  } finally {
    editRolesSubmitting.value = false;
  }
}

// ==================== Edit Profile ====================
const editProfileVisible = ref(false);
const editProfileSubmitting = ref(false);
const editProfileUserId = ref("");
const editProfileForm = reactive({
  displayName: "",
  email: "",
  phoneNumber: "",
  isActive: true
});

function openEditProfileDrawer(record: TenantAppMemberListItem) {
  editProfileUserId.value = record.userId;
  editProfileForm.displayName = record.displayName;
  editProfileForm.email = record.email ?? "";
  editProfileForm.phoneNumber = record.phoneNumber ?? "";
  editProfileForm.isActive = record.isActive;
  editProfileVisible.value = true;
}

async function handleEditProfileSubmit() {
  const id = appId.value;
  if (!id) return;

  editProfileSubmitting.value = true;
  try {
    await updateMemberProfile(id, editProfileUserId.value, {
      displayName: editProfileForm.displayName,
      email: editProfileForm.email || undefined,
      phoneNumber: editProfileForm.phoneNumber || undefined,
      isActive: editProfileForm.isActive
    });
    message.success(t("org.member.updateProfileSuccess"));
    editProfileVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.member.updateProfileFailed"));
  } finally {
    editProfileSubmitting.value = false;
  }
}

// ==================== Reset Password ====================
const resetPwdVisible = ref(false);
const resetPwdSubmitting = ref(false);
const resetPwdUserId = ref("");
const resetPwdForm = reactive({ newPassword: "" });

function openResetPasswordModal(record: TenantAppMemberListItem) {
  resetPwdUserId.value = record.userId;
  resetPwdForm.newPassword = "";
  resetPwdVisible.value = true;
}

async function handleResetPasswordSubmit() {
  const id = appId.value;
  if (!id || !resetPwdForm.newPassword.trim()) return;

  resetPwdSubmitting.value = true;
  try {
    await resetMemberPassword(id, resetPwdUserId.value, {
      newPassword: resetPwdForm.newPassword
    });
    message.success(t("org.member.resetPasswordSuccess"));
    resetPwdVisible.value = false;
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.member.resetPasswordFailed"));
  } finally {
    resetPwdSubmitting.value = false;
  }
}

// ==================== Remove Member ====================
async function handleRemoveMember(record: TenantAppMemberListItem) {
  const id = appId.value;
  if (!id) return;
  try {
    await removeMember(id, record.userId);
    message.success(t("org.member.removeSuccess"));
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.member.removeFailed"));
  }
}

// ==================== Role CRUD ====================
const roleDrawerVisible = ref(false);
const roleSubmitting = ref(false);
const roleEditingId = ref<string | null>(null);
const roleFormRef = ref<FormInstance>();
const roleForm = reactive({ code: "", name: "", description: "" });
const roleFormRules = computed(() => ({
  name: [{ required: true, message: t("org.role.nameRequired") }],
  code: [{ required: true, message: t("org.role.codeRequired") }]
}));

function openRoleDrawer(record?: TenantAppRoleListItem) {
  if (record) {
    roleEditingId.value = record.id;
    roleForm.code = record.code;
    roleForm.name = record.name;
    roleForm.description = record.description ?? "";
  } else {
    roleEditingId.value = null;
    roleForm.code = "";
    roleForm.name = "";
    roleForm.description = "";
  }
  roleDrawerVisible.value = true;
}

async function handleRoleSubmit() {
  const id = appId.value;
  if (!id) return;
  await roleFormRef.value?.validate();

  roleSubmitting.value = true;
  try {
    if (roleEditingId.value) {
      await updateRole(id, roleEditingId.value, {
        name: roleForm.name,
        description: roleForm.description || undefined
      });
    } else {
      await createRole(id, {
        code: roleForm.code,
        name: roleForm.name,
        description: roleForm.description || undefined
      });
    }
    message.success(t("org.role.saveSuccess"));
    roleDrawerVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.role.saveFailed"));
  } finally {
    roleSubmitting.value = false;
  }
}

async function handleDeleteRole(record: TenantAppRoleListItem) {
  const id = appId.value;
  if (!id) return;
  if (record.isSystem) {
    message.warning(t("org.role.systemCannotDelete"));
    return;
  }
  try {
    await deleteRoleApi(id, record.id);
    message.success(t("org.role.deleteSuccess"));
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.role.deleteFailed"));
  }
}

// ==================== Department CRUD ====================
const deptDrawerVisible = ref(false);
const deptSubmitting = ref(false);
const deptEditingId = ref<string | null>(null);
const deptFormRef = ref<FormInstance>();
const deptForm = reactive({ name: "", code: "", parentId: undefined as string | undefined, sortOrder: 0 });
const deptFormRules = computed(() => ({
  name: [{ required: true, message: t("org.department.nameRequired") }],
  code: [{ required: true, message: t("org.department.codeRequired") }]
}));

function openDeptDrawer(record?: AppDepartmentListItem) {
  if (record) {
    deptEditingId.value = record.id;
    deptForm.name = record.name;
    deptForm.code = record.code;
    deptForm.parentId = record.parentId ?? undefined;
    deptForm.sortOrder = record.sortOrder;
  } else {
    deptEditingId.value = null;
    deptForm.name = "";
    deptForm.code = "";
    deptForm.parentId = undefined;
    deptForm.sortOrder = 0;
  }
  deptDrawerVisible.value = true;
}

async function handleDeptSubmit() {
  const id = appId.value;
  if (!id) return;
  await deptFormRef.value?.validate();

  deptSubmitting.value = true;
  try {
    if (deptEditingId.value) {
      await updateDepartment(id, deptEditingId.value, {
        name: deptForm.name,
        code: deptForm.code,
        parentId: deptForm.parentId,
        sortOrder: deptForm.sortOrder
      });
    } else {
      await createDepartment(id, {
        name: deptForm.name,
        code: deptForm.code,
        parentId: deptForm.parentId,
        sortOrder: deptForm.sortOrder
      });
    }
    message.success(t("org.department.saveSuccess"));
    deptDrawerVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.department.saveFailed"));
  } finally {
    deptSubmitting.value = false;
  }
}

async function handleDeleteDept(record: AppDepartmentListItem) {
  const id = appId.value;
  if (!id) return;
  try {
    await deleteDeptApi(id, record.id);
    message.success(t("org.department.deleteSuccess"));
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.department.deleteFailed"));
  }
}

// ==================== Position CRUD ====================
const posDrawerVisible = ref(false);
const posSubmitting = ref(false);
const posEditingId = ref<string | null>(null);
const posFormRef = ref<FormInstance>();
const posForm = reactive({ name: "", code: "", description: "", isActive: true, sortOrder: 0 });
const posFormRules = computed(() => ({
  name: [{ required: true, message: t("org.position.nameRequired") }],
  code: [{ required: true, message: t("org.position.codeRequired") }]
}));

function openPosDrawer(record?: AppPositionListItem) {
  if (record) {
    posEditingId.value = record.id;
    posForm.name = record.name;
    posForm.code = record.code;
    posForm.description = record.description ?? "";
    posForm.isActive = record.isActive;
    posForm.sortOrder = record.sortOrder;
  } else {
    posEditingId.value = null;
    posForm.name = "";
    posForm.code = "";
    posForm.description = "";
    posForm.isActive = true;
    posForm.sortOrder = 0;
  }
  posDrawerVisible.value = true;
}

async function handlePosSubmit() {
  const id = appId.value;
  if (!id) return;
  await posFormRef.value?.validate();

  posSubmitting.value = true;
  try {
    if (posEditingId.value) {
      await updatePosition(id, posEditingId.value, {
        name: posForm.name,
        description: posForm.description || undefined,
        isActive: posForm.isActive,
        sortOrder: posForm.sortOrder
      });
    } else {
      await createPosition(id, {
        name: posForm.name,
        code: posForm.code,
        description: posForm.description || undefined,
        isActive: posForm.isActive,
        sortOrder: posForm.sortOrder
      });
    }
    message.success(t("org.position.saveSuccess"));
    posDrawerVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.position.saveFailed"));
  } finally {
    posSubmitting.value = false;
  }
}

async function handleDeletePos(record: AppPositionListItem) {
  const id = appId.value;
  if (!id) return;
  try {
    await deletePosApi(id, record.id);
    message.success(t("org.position.deleteSuccess"));
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.position.deleteFailed"));
  }
}

// ==================== Project CRUD ====================
const projDrawerVisible = ref(false);
const projSubmitting = ref(false);
const projEditingId = ref<string | null>(null);
const projFormRef = ref<FormInstance>();
const projForm = reactive({ code: "", name: "", description: "", isActive: true });
const projFormRules = computed(() => ({
  code: [{ required: true, message: t("org.project.codeRequired") }],
  name: [{ required: true, message: t("org.project.nameRequired") }]
}));

function openProjDrawer(record?: AppProjectListItem) {
  if (record) {
    projEditingId.value = record.id;
    projForm.code = record.code;
    projForm.name = record.name;
    projForm.description = record.description ?? "";
    projForm.isActive = record.isActive;
  } else {
    projEditingId.value = null;
    projForm.code = "";
    projForm.name = "";
    projForm.description = "";
    projForm.isActive = true;
  }
  projDrawerVisible.value = true;
}

async function handleProjSubmit() {
  const id = appId.value;
  if (!id) return;
  await projFormRef.value?.validate();

  projSubmitting.value = true;
  try {
    if (projEditingId.value) {
      await updateProject(id, projEditingId.value, {
        name: projForm.name,
        description: projForm.description || undefined,
        isActive: projForm.isActive
      });
    } else {
      await createProject(id, {
        code: projForm.code,
        name: projForm.name,
        description: projForm.description || undefined,
        isActive: projForm.isActive
      });
    }
    message.success(t("org.project.saveSuccess"));
    projDrawerVisible.value = false;
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.project.saveFailed"));
  } finally {
    projSubmitting.value = false;
  }
}

async function handleDeleteProj(record: AppProjectListItem) {
  const id = appId.value;
  if (!id) return;
  try {
    await deleteProjApi(id, record.id);
    message.success(t("org.project.deleteSuccess"));
    void loadWorkspace();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.project.deleteFailed"));
  }
}
</script>

<style scoped>
.app-organization {
  max-width: 1400px;
}

.org-error {
  margin-bottom: 16px;
}

.tab-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
</style>
