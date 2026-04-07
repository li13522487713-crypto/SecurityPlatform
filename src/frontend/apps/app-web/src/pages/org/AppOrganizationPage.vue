<template>
  <div class="app-organization">
    <a-spin :spinning="initialLoading">
      <a-alert
        v-if="loadError"
        type="error"
        show-icon
        :message="loadError"
        closable
        class="org-error"
      />

      <a-layout class="org-layout">
        <!-- ==================== LEFT SIDEBAR ==================== -->
        <a-layout-sider :width="280" theme="light" class="org-sidebar">
          <div class="sidebar-header">
            <TeamOutlined class="sidebar-icon" />
            <span class="sidebar-title">{{ t('org.sidebarTitle') }}</span>
          </div>

          <div
            class="sidebar-nav-item sidebar-nav-all"
            :class="{ active: activeNav.type === 'all' }"
            @click="setActiveNav('all')"
          >
            <UserOutlined />
            <span>{{ t('org.allMembers') }}</span>
          </div>

          <!-- Department group -->
          <div class="sidebar-category-group">
            <div class="sidebar-category-header">
              <span class="sidebar-category-label">{{ t('org.tabDepartments') }}</span>
              <PlusOutlined
                v-if="canManageMembers"
                class="sidebar-category-add"
                @click.stop="openDeptDrawer()"
              />
            </div>
            <div
              v-for="dept in workspaceData?.departments ?? []"
              :key="dept.id"
              class="sidebar-nav-item sidebar-nav-sub"
              :class="{ active: activeNav.type === 'depts' && activeNav.value === dept.id }"
              @click="setActiveNav('depts', dept.id, dept.name)"
            >
              <span class="nav-item-text">{{ dept.name }}</span>
            </div>
          </div>

          <!-- Role group -->
          <div class="sidebar-category-group">
            <div class="sidebar-category-header">
              <span class="sidebar-category-label">{{ t('org.tabRoles') }}</span>
              <PlusOutlined
                v-if="canManageRoles"
                class="sidebar-category-add"
                @click.stop="openRoleDrawer()"
              />
            </div>
            <div
              v-for="role in workspaceData?.roles ?? []"
              :key="role.id"
              class="sidebar-nav-item sidebar-nav-sub"
              :class="{ active: activeNav.type === 'roles' && activeNav.value === role.id }"
              @click="setActiveNav('roles', role.id, role.name)"
            >
              <span class="nav-item-text">{{ role.name }}</span>
              <a-tag v-if="role.isSystem" size="small" color="default" class="nav-item-tag">{{ t('org.role.isSystem') }}</a-tag>
            </div>
          </div>

          <!-- Position group -->
          <div class="sidebar-category-group">
            <div class="sidebar-category-header">
              <span class="sidebar-category-label">{{ t('org.tabPositions') }}</span>
              <PlusOutlined
                v-if="canManageMembers"
                class="sidebar-category-add"
                @click.stop="openPosDrawer()"
              />
            </div>
            <div
              v-for="pos in workspaceData?.positions ?? []"
              :key="pos.id"
              class="sidebar-nav-item sidebar-nav-sub"
              :class="{ active: activeNav.type === 'positions' && activeNav.value === pos.id }"
              @click="setActiveNav('positions', pos.id, pos.name)"
            >
              <span class="nav-item-text">{{ pos.name }}</span>
            </div>
          </div>

          <!-- Project group -->
          <div class="sidebar-category-group">
            <div class="sidebar-category-header">
              <span class="sidebar-category-label">{{ t('org.tabProjects') }}</span>
              <PlusOutlined
                v-if="canManageMembers"
                class="sidebar-category-add"
                @click.stop="openProjDrawer()"
              />
            </div>
            <div
              v-for="proj in workspaceData?.projects ?? []"
              :key="proj.id"
              class="sidebar-nav-item sidebar-nav-sub"
              :class="{ active: activeNav.type === 'projects' && activeNav.value === proj.id }"
              @click="setActiveNav('projects', proj.id, proj.name)"
            >
              <span class="nav-item-text">{{ proj.name }}</span>
            </div>
          </div>
        </a-layout-sider>

        <!-- ==================== MAIN CONTENT ==================== -->
        <a-layout-content class="org-content">
          <!-- Page Header -->
          <div class="content-header">
            <div class="content-header-info">
              <h2 class="content-title">{{ headerTitle }}</h2>
              <p v-if="headerSubtitle" class="content-subtitle">{{ headerSubtitle }}</p>
            </div>
            <a-space>
              <a-button
                v-if="activeNav.type !== 'all' && canManageCategory"
                @click="openManageCategoryAction"
              >
                {{ t('org.manageCategory', { type: activeCategoryLabel }) }}
              </a-button>
              <a-button v-if="canManageMembers" type="primary" @click="openAddMemberModal">
                {{ t('org.member.addTitle') }}
              </a-button>
            </a-space>
          </div>

          <!-- Search & Filter bar -->
          <div class="content-search-bar">
            <a-input-search
              v-model:value="memberKeyword"
              :placeholder="t('org.member.searchPlaceholder')"
              style="width: 320px"
              allow-clear
              @search="handleMemberSearch"
            />
            <span class="search-result-count">
              {{ t('org.foundMembers', { count: memberPagination.total ?? 0 }) }}
            </span>
          </div>

          <!-- Members Table -->
          <a-table
            :columns="memberColumnsNew"
            :data-source="memberList"
            :loading="memberLoading"
            :pagination="memberPagination"
            row-key="userId"
            size="middle"
            @change="handleMemberTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'displayName'">
                <div class="member-info-cell">
                  <a-avatar :size="32" class="member-avatar">
                    {{ (record.displayName || record.username || '?')[0].toUpperCase() }}
                  </a-avatar>
                  <div class="member-info-text">
                    <span class="member-name">{{ record.displayName || record.username }}</span>
                    <span class="member-account">@{{ record.username }}</span>
                  </div>
                </div>
              </template>
              <template v-if="column.dataIndex === 'departmentNames'">
                {{ (record.departmentNames ?? []).join(', ') || '—' }}
              </template>
              <template v-if="column.dataIndex === 'positionNames'">
                {{ (record.positionNames ?? []).join(', ') || '—' }}
              </template>
              <template v-if="column.dataIndex === 'roleNames'">
                <a-tag v-for="name in record.roleNames" :key="name" color="blue">{{ name }}</a-tag>
              </template>
              <template v-if="column.dataIndex === 'isActive'">
                <span class="status-dot" :class="record.isActive ? 'status-active' : 'status-disabled'" />
                {{ record.isActive ? t('org.statusNormal') : t('org.statusDisabled') }}
              </template>
              <template v-if="column.dataIndex === 'actions'">
                <a-dropdown :trigger="['click']">
                  <a-button type="link" size="small">
                    {{ t('common.actions') }}
                    <DownOutlined />
                  </a-button>
                  <template #overlay>
                    <a-menu>
                      <a-menu-item v-if="canManageMembers" @click="openEditRolesDrawer(record)">
                        {{ t('org.member.editRoles') }}
                      </a-menu-item>
                      <a-menu-item v-if="canManageMembers" @click="openEditProfileDrawer(record)">
                        {{ t('org.member.editProfile') }}
                      </a-menu-item>
                      <a-menu-item v-if="canManageMembers" @click="openResetPasswordModal(record)">
                        {{ t('org.member.resetPassword') }}
                      </a-menu-item>
                      <a-menu-divider v-if="canManageMembers" />
                      <a-menu-item v-if="canManageMembers" danger @click="confirmRemoveMember(record)">
                        {{ t('common.delete') }}
                      </a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </template>
            </template>
          </a-table>
        </a-layout-content>
      </a-layout>
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
            <a-form-item :label="t('org.member.selectUsers')" required>
              <a-select
                v-model:value="addMemberForm.userIds"
                mode="multiple"
                :placeholder="t('org.member.searchUserPlaceholder')"
                show-search
                :filter-option="false"
                :options="userSearchOptions"
                :loading="userSearchLoading"
                @search="handleUserSearch"
                :not-found-content="userSearchLoading ? undefined : null"
              />
            </a-form-item>
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
    <!-- ==================== Permission Drawer ==================== -->
    <a-drawer
      v-model:open="permDrawerVisible"
      :title="t('org.role.permissionsTitle', { name: permRoleName })"
      :width="720"
      destroy-on-close
    >
      <a-spin :spinning="permLoading">
        <a-tabs v-model:active-key="permInnerTab">
          <a-tab-pane key="permissions" :tab="t('org.role.tabPermissions')">
            <a-checkbox-group
              v-model:value="permSelectedCodes"
              style="width: 100%"
            >
              <a-row :gutter="[0, 8]">
                <a-col
                  v-for="perm in allPermissions"
                  :key="perm.code"
                  :span="24"
                >
                  <a-checkbox :value="perm.code">
                    <span>{{ perm.name }}</span>
                    <span v-if="perm.description" class="perm-desc"> — {{ perm.description }}</span>
                  </a-checkbox>
                </a-col>
              </a-row>
            </a-checkbox-group>
            <a-empty v-if="allPermissions.length === 0 && !permLoading" />
          </a-tab-pane>

          <a-tab-pane key="dataScope" :tab="t('org.role.tabDataScope')">
            <a-alert
              type="info"
              show-icon
              :message="t('org.role.dataScopeHint')"
              style="margin-bottom: 12px"
            />
            <a-form layout="vertical">
              <a-form-item :label="t('org.role.dataScopeLabel')">
                <a-radio-group v-model:value="permDataScope" class="data-scope-radio-group">
                  <a-space direction="vertical">
                    <a-radio :value="0">{{ t("org.role.dataScopeAll") }}</a-radio>
                    <a-radio :value="1">{{ t("org.role.dataScopeCurrentTenant") }}</a-radio>
                    <a-radio :value="2">{{ t("org.role.dataScopeCustomDept") }}</a-radio>
                    <a-radio :value="3">{{ t("org.role.dataScopeCurrentDept") }}</a-radio>
                    <a-radio :value="4">{{ t("org.role.dataScopeCurrentDeptAndBelow") }}</a-radio>
                    <a-radio :value="5">{{ t("org.role.dataScopeOnlySelf") }}</a-radio>
                  </a-space>
                </a-radio-group>
              </a-form-item>
              <a-form-item
                v-if="permDataScope === 2"
                :label="t('org.role.customDeptSelect')"
              >
                <a-tree-select
                  v-model:value="permDeptIds"
                  :tree-data="deptTreeDataForScope"
                  tree-checkable
                  show-checked-strategy="SHOW_ALL"
                  allow-clear
                  tree-node-filter-prop="title"
                  style="width: 100%"
                  :placeholder="t('org.role.customDeptPlaceholder')"
                />
              </a-form-item>
            </a-form>
          </a-tab-pane>

          <a-tab-pane key="pages" :tab="t('org.role.tabPagePermissions')">
            <a-alert
              type="info"
              show-icon
              :message="t('org.role.pagePermHint')"
              style="margin-bottom: 12px"
            />
            <a-tree
              v-if="pageTreeData.length > 0"
              v-model:checked-keys="pageCheckedKeys"
              checkable
              check-strictly
              :tree-data="pageTreeData"
              :selectable="false"
              default-expand-all
              class="page-perm-tree"
            />
            <a-empty v-else :description="t('org.role.noPagesAvailable')" />
          </a-tab-pane>

          <a-tab-pane key="fieldPermissions" :tab="t('org.role.tabFieldPermissions')">
            <a-alert
              type="info"
              show-icon
              :message="t('org.role.fieldPermHint')"
              style="margin-bottom: 12px"
            />
            <a-select
              v-model:value="selectedFieldTableKey"
              style="width: 100%; margin-bottom: 12px"
              :placeholder="t('org.role.selectDynamicTable')"
              :options="dynamicTableSelectOptions"
              :loading="dynamicTablesLoading"
              show-search
              :filter-option="false"
              allow-clear
              @search="handleDynamicTableSearch"
              @focus="handleDynamicTableFocus"
            />
            <a-empty
              v-if="!selectedFieldTableKey"
              :description="t('org.role.fieldPermNoTableSelected')"
            />
            <a-table
              v-else
              :data-source="fieldPermRows"
              :loading="fieldDefinitionsLoading"
              :pagination="false"
              row-key="fieldName"
              size="small"
              class="field-perm-table"
            >
              <a-table-column
                key="label"
                :title="t('org.role.fieldColumnLabel')"
                data-index="label"
              />
              <a-table-column key="canView" align="center" width="120">
                <template #title>
                  <span>{{ t("org.role.fieldColumnVisible") }}</span>
                </template>
                <template #default="{ record }">
                  <a-switch
                    :checked="record.canView"
                    size="small"
                    @change="(v: boolean) => onFieldViewChange(record.fieldName, v)"
                  />
                </template>
              </a-table-column>
              <a-table-column key="canEdit" align="center" width="120">
                <template #title>
                  <span>{{ t("org.role.fieldColumnEditable") }}</span>
                </template>
                <template #default="{ record }">
                  <a-switch
                    :checked="record.canEdit"
                    size="small"
                    :disabled="!record.canView"
                    @change="(v: boolean) => onFieldEditChange(record.fieldName, v)"
                  />
                </template>
              </a-table-column>
            </a-table>
          </a-tab-pane>
        </a-tabs>
      </a-spin>
      <template #footer>
        <a-space>
          <a-button @click="permDrawerVisible = false">{{ t('common.cancel') }}</a-button>
          <a-button type="primary" :loading="permSaving" @click="handleSavePermissions">
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
import { message, Modal } from "ant-design-vue";
import {
  TeamOutlined,
  UserOutlined,
  PlusOutlined,
  DownOutlined
} from "@ant-design/icons-vue";
import { searchTenantUsers } from "@/services/api-users";
import { usePermission } from "@/composables/usePermission";
import { APP_PERMISSIONS } from "@/constants/permissions";
import type { TablePaginationConfig } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue/es/form";
import type { DataNode } from "ant-design-vue/es/tree";
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
  deleteProject as deleteProjApi,
  getAppPermissions,
  getRoleDetail,
  updateRolePermissions,
  getAppRoleDataScope,
  updateAppRoleDataScope,
  getAvailableAppPages,
  getRolePageIds,
  updateRolePages,
  getRoleFieldPermissions,
  updateRoleFieldPermissions,
  getAvailableDynamicTables,
  getAppDynamicTableFields
} from "@/services/api-organization";
import type {
  PermissionListItem as AppPermissionItem,
  AppPageListItem,
  AppRoleFieldPermissionGroupDto,
  AppAvailableDynamicTableItem
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
const { hasPermission } = usePermission();
const canManageMembers = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);
const canManageRoles = hasPermission(APP_PERMISSIONS.APP_ROLES_UPDATE);

interface ActiveNavState {
  type: "all" | "depts" | "roles" | "positions" | "projects";
  value: string;
  label: string;
}

const activeNav = ref<ActiveNavState>({ type: "all", value: "", label: "" });
const initialLoading = ref(false);
const loadError = ref("");
const workspaceData = ref<AppOrganizationWorkspaceResponse | null>(null);

function setActiveNav(type: ActiveNavState["type"], value = "", label = "") {
  activeNav.value = { type, value, label };
  memberRoleFilter.value = type === "roles" ? value : undefined;
  memberKeyword.value = "";
  memberPagination.current = 1;
  void refreshMembers();
}

const headerTitle = computed(() => {
  const nav = activeNav.value;
  if (nav.type === "all") return t("org.allMembers");
  return nav.label || t("org.allMembers");
});

const headerSubtitle = computed(() => {
  const nav = activeNav.value;
  if (nav.type === "all") return t("org.allMembersDesc");
  const typeLabels: Record<string, string> = {
    depts: t("org.tabDepartments"),
    roles: t("org.tabRoles"),
    positions: t("org.tabPositions"),
    projects: t("org.tabProjects")
  };
  return t("org.categoryFilterDesc", { value: nav.label, type: typeLabels[nav.type] ?? "" });
});

const activeCategoryLabel = computed(() => {
  const typeLabels: Record<string, string> = {
    depts: t("org.tabDepartments"),
    roles: t("org.tabRoles"),
    positions: t("org.tabPositions"),
    projects: t("org.tabProjects")
  };
  return typeLabels[activeNav.value.type] ?? "";
});

const canManageCategory = computed(() => {
  const nav = activeNav.value;
  if (nav.type === "roles") return canManageRoles;
  return canManageMembers;
});

function openManageCategoryAction() {
  const nav = activeNav.value;
  if (!nav.value) return;
  switch (nav.type) {
    case "depts": {
      const dept = workspaceData.value?.departments?.find((d) => d.id === nav.value);
      if (dept) openDeptDrawer(dept);
      break;
    }
    case "roles": {
      const role = workspaceData.value?.roles?.find((r) => r.id === nav.value);
      if (role) openPermissionDrawer(role);
      break;
    }
    case "positions": {
      const pos = workspaceData.value?.positions?.find((p) => p.id === nav.value);
      if (pos) openPosDrawer(pos);
      break;
    }
    case "projects": {
      const proj = workspaceData.value?.projects?.find((p) => p.id === nav.value);
      if (proj) openProjDrawer(proj);
      break;
    }
  }
}

function confirmRemoveMember(record: TenantAppMemberListItem) {
  Modal.confirm({
    title: t("org.member.removeConfirm"),
    onOk: () => handleRemoveMember(record)
  });
}

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

const memberColumnsNew = computed(() => [
  { title: t("org.memberInfo"), dataIndex: "displayName", width: 220 },
  { title: t("org.memberDept"), dataIndex: "departmentNames", width: 160 },
  { title: t("org.memberPosition"), dataIndex: "positionNames", width: 140 },
  { title: t("org.memberRoles"), dataIndex: "roleNames", width: 200 },
  { title: t("org.memberStatusCol"), dataIndex: "isActive", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 100, fixed: "right" as const }
]);

const roleColumns = computed(() => [
  { title: t("org.role.name"), dataIndex: "name", width: 150 },
  { title: t("org.role.code"), dataIndex: "code", width: 130 },
  { title: t("org.role.description"), dataIndex: "description", ellipsis: true },
  { title: t("org.role.isSystem"), dataIndex: "isSystem", width: 100 },
  { title: t("org.role.memberCount"), dataIndex: "memberCount", width: 100 },
  { title: t("common.actions"), dataIndex: "actions", width: 220, fixed: "right" as const }
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

const userSearchOptions = ref<Array<{ value: string; label: string }>>([]);
const userSearchLoading = ref(false);
let userSearchTimer: ReturnType<typeof setTimeout> | null = null;

function handleUserSearch(keyword: string) {
  if (userSearchTimer) clearTimeout(userSearchTimer);
  if (!keyword.trim()) {
    userSearchOptions.value = [];
    return;
  }
  userSearchTimer = setTimeout(async () => {
    userSearchLoading.value = true;
    try {
      const result = await searchTenantUsers(keyword, 20);
      userSearchOptions.value = (result.items ?? []).map((u) => ({
        value: u.id,
        label: `${u.displayName || u.username} (${u.username})`
      }));
    } catch {
      userSearchOptions.value = [];
    } finally {
      userSearchLoading.value = false;
    }
  }, 300);
}

function openAddMemberModal() {
  addMemberMode.value = "existing";
  addMemberForm.userIds = [];
  addMemberForm.roleIds = [];
  addMemberForm.departmentIds = [];
  addMemberForm.positionIds = [];
  userSearchOptions.value = [];
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
      if (addMemberForm.userIds.length === 0) {
        message.warning(t("org.member.userRequired"));
        addMemberSubmitting.value = false;
        return;
      }
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

// ==================== Permission Assignment ====================
interface FieldPermRow {
  fieldName: string;
  label: string;
  canView: boolean;
  canEdit: boolean;
}

const permDrawerVisible = ref(false);
const permLoading = ref(false);
const permSaving = ref(false);
const permRoleId = ref("");
const permRoleName = ref("");
const allPermissions = ref<AppPermissionItem[]>([]);
const permSelectedCodes = ref<string[]>([]);
const permInnerTab = ref("permissions");
const permDataScope = ref(0);
const permDeptIds = ref<string[]>([]);
const permAvailablePages = ref<AppPageListItem[]>([]);
const pageCheckedKeys = ref<(string | number)[]>([]);
const fieldGroupsDraft = ref<AppRoleFieldPermissionGroupDto[]>([]);
const selectedFieldTableKey = ref<string | undefined>(undefined);
const fieldPermRows = ref<FieldPermRow[]>([]);
const fieldDefinitionsLoading = ref(false);
const dynamicTableOptionsList = ref<AppAvailableDynamicTableItem[]>([]);
const dynamicTablesLoading = ref(false);
const fieldTableWatchSuspended = ref(false);
let dynamicTableSearchTimer: ReturnType<typeof setTimeout> | null = null;

const deptTreeDataForScope = computed<DataNode[]>(() =>
  buildDepartmentTreeNodes(workspaceData.value?.departments ?? [])
);

const pageTreeData = computed<DataNode[]>(() => buildPageTreeFromItems(permAvailablePages.value));

const dynamicTableSelectOptions = computed(() =>
  dynamicTableOptionsList.value.map((item) => ({
    value: item.tableKey,
    label: `${item.displayName} (${item.tableKey})`
  }))
);

function cloneFieldGroups(src: AppRoleFieldPermissionGroupDto[]): AppRoleFieldPermissionGroupDto[] {
  return src.map((g) => ({
    tableKey: g.tableKey,
    fields: g.fields.map((f) => ({ ...f }))
  }));
}

function buildDepartmentTreeNodes(depts: AppDepartmentListItem[]): DataNode[] {
  const sorted = [...depts].sort((a, b) => a.sortOrder - b.sortOrder);
  const map = new Map<string, DataNode>();
  for (const d of sorted) {
    map.set(d.id, { title: d.name, value: d.id, key: d.id, children: [] });
  }
  const roots: DataNode[] = [];
  for (const d of sorted) {
    const node = map.get(d.id);
    if (!node) continue;
    const pid = d.parentId;
    if (pid && map.has(pid)) {
      const parent = map.get(pid)!;
      if (!parent.children) parent.children = [];
      (parent.children as DataNode[]).push(node);
    } else {
      roots.push(node);
    }
  }
  const stripEmptyChildren = (nodes: DataNode[]): DataNode[] =>
    nodes.map((n) => {
      const ch = n.children as DataNode[] | undefined;
      if (ch?.length) {
        return { ...n, children: stripEmptyChildren(ch) };
      }
      const { children: _c, ...rest } = n;
      return rest;
    });
  return stripEmptyChildren(roots);
}

function buildPageTreeFromItems(pages: AppPageListItem[]): DataNode[] {
  const sorted = [...pages].sort((a, b) => a.sortOrder - b.sortOrder);
  const map = new Map<number, DataNode>();
  for (const p of sorted) {
    const id = Number(p.id);
    if (!Number.isFinite(id)) continue;
    map.set(id, { title: p.name, key: id, children: [] });
  }
  const roots: DataNode[] = [];
  for (const p of sorted) {
    const id = Number(p.id);
    if (!Number.isFinite(id)) continue;
    const node = map.get(id);
    if (!node) continue;
    const pid = p.parentPageId;
    if (pid != null && map.has(pid)) {
      const parent = map.get(pid)!;
      if (!parent.children) parent.children = [];
      (parent.children as DataNode[]).push(node);
    } else {
      roots.push(node);
    }
  }
  const stripEmptyChildren = (nodes: DataNode[]): DataNode[] =>
    nodes.map((n) => {
      const ch = n.children as DataNode[] | undefined;
      if (ch?.length) {
        return { ...n, children: stripEmptyChildren(ch) };
      }
      const { children: _c, ...rest } = n;
      return rest;
    });
  return stripEmptyChildren(roots);
}

function filterNumericPageKeys(keys: (string | number)[]): number[] {
  return keys
    .map((k) => (typeof k === "number" ? k : Number(k)))
    .filter((n): n is number => Number.isFinite(n) && !Number.isNaN(n) && n > 0);
}

function persistCurrentFieldTableToDraft() {
  const tk = selectedFieldTableKey.value;
  if (!tk || fieldPermRows.value.length === 0) return;
  const fields = fieldPermRows.value.map((r) => ({
    fieldName: r.fieldName,
    canView: r.canView,
    canEdit: r.canEdit
  }));
  const draft = [...fieldGroupsDraft.value];
  const i = draft.findIndex((g) => g.tableKey === tk);
  const group: AppRoleFieldPermissionGroupDto = { tableKey: tk, fields };
  if (i >= 0) draft[i] = group;
  else draft.push(group);
  fieldGroupsDraft.value = draft;
}

async function loadFieldEditorForTable(tableKey: string) {
  fieldDefinitionsLoading.value = true;
  fieldPermRows.value = [];
  try {
    const defs = await getAppDynamicTableFields(tableKey);
    const existing = fieldGroupsDraft.value.find((g) => g.tableKey === tableKey);
    fieldPermRows.value = defs.map((f) => {
      const rule = existing?.fields.find((x) => x.fieldName === f.name);
      return {
        fieldName: f.name,
        label: f.displayName || f.name,
        canView: rule?.canView ?? false,
        canEdit: rule?.canEdit ?? false
      };
    });
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.role.loadFieldsFailed"));
  } finally {
    fieldDefinitionsLoading.value = false;
  }
}

watch(selectedFieldTableKey, async (newKey, oldKey) => {
  if (fieldTableWatchSuspended.value) return;
  if (oldKey !== undefined && oldKey !== "" && fieldPermRows.value.length > 0) {
    const fields = fieldPermRows.value.map((r) => ({
      fieldName: r.fieldName,
      canView: r.canView,
      canEdit: r.canEdit
    }));
    const draft = [...fieldGroupsDraft.value];
    const i = draft.findIndex((g) => g.tableKey === oldKey);
    const group: AppRoleFieldPermissionGroupDto = { tableKey: oldKey, fields };
    if (i >= 0) draft[i] = group;
    else draft.push(group);
    fieldGroupsDraft.value = draft;
  }
  if (!newKey) {
    fieldPermRows.value = [];
    return;
  }
  await loadFieldEditorForTable(newKey);
});

function onFieldViewChange(fieldName: string, checked: boolean) {
  const row = fieldPermRows.value.find((r) => r.fieldName === fieldName);
  if (!row) return;
  row.canView = checked;
  if (!checked) row.canEdit = false;
}

function onFieldEditChange(fieldName: string, checked: boolean) {
  const row = fieldPermRows.value.find((r) => r.fieldName === fieldName);
  if (!row) return;
  row.canEdit = checked;
  if (checked) row.canView = true;
}

async function handleDynamicTableFocus() {
  const id = appId.value;
  if (!id || dynamicTableOptionsList.value.length > 0) return;
  dynamicTablesLoading.value = true;
  try {
    dynamicTableOptionsList.value = await getAvailableDynamicTables(id);
  } catch {
    message.warning(t("org.role.loadDynamicTablesFailed"));
  } finally {
    dynamicTablesLoading.value = false;
  }
}

function handleDynamicTableSearch(keyword: string) {
  const id = appId.value;
  if (!id) return;
  if (dynamicTableSearchTimer) clearTimeout(dynamicTableSearchTimer);
  dynamicTableSearchTimer = setTimeout(async () => {
    dynamicTablesLoading.value = true;
    try {
      dynamicTableOptionsList.value = await getAvailableDynamicTables(
        id,
        keyword.trim() || undefined
      );
    } catch {
      dynamicTableOptionsList.value = [];
    } finally {
      dynamicTablesLoading.value = false;
    }
  }, 300);
}

async function openPermissionDrawer(record: TenantAppRoleListItem) {
  const id = appId.value;
  if (!id) return;

  fieldTableWatchSuspended.value = true;
  permRoleId.value = record.id;
  permRoleName.value = record.name;
  permInnerTab.value = "permissions";
  permSelectedCodes.value = [];
  allPermissions.value = [];
  permDataScope.value = 0;
  permDeptIds.value = [];
  permAvailablePages.value = [];
  pageCheckedKeys.value = [];
  fieldGroupsDraft.value = [];
  selectedFieldTableKey.value = undefined;
  fieldPermRows.value = [];
  dynamicTableOptionsList.value = [];

  permDrawerVisible.value = true;
  permLoading.value = true;

  try {
    const [perms, detail, dataScopeDetail, pages, rolePageIds, fieldGroups, dynTables] = await Promise.all([
      getAppPermissions(id),
      getRoleDetail(id, record.id),
      getAppRoleDataScope(id, record.id),
      getAvailableAppPages(id),
      getRolePageIds(id, record.id),
      getRoleFieldPermissions(id, record.id),
      getAvailableDynamicTables(id)
    ]);
    allPermissions.value = perms;
    permSelectedCodes.value = [...detail.permissionCodes];
    permDataScope.value = dataScopeDetail.dataScope;
    permDeptIds.value = [...dataScopeDetail.deptIds];
    permAvailablePages.value = pages;
    pageCheckedKeys.value = [...rolePageIds];
    fieldGroupsDraft.value = cloneFieldGroups(fieldGroups);
    dynamicTableOptionsList.value = dynTables;
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.role.loadPermFailed"));
  } finally {
    permLoading.value = false;
    fieldTableWatchSuspended.value = false;
  }
}

async function handleSavePermissions() {
  const id = appId.value;
  if (!id) return;

  if (permDataScope.value === 2 && permDeptIds.value.length === 0) {
    message.warning(t("org.role.deptRequiredForCustomScope"));
    return;
  }

  persistCurrentFieldTableToDraft();

  permSaving.value = true;
  try {
    const pageIds = filterNumericPageKeys(pageCheckedKeys.value);
    const deptIds =
      permDataScope.value === 2
        ? permDeptIds.value.map((x) => Number(x)).filter((n) => Number.isFinite(n) && n > 0)
        : undefined;

    await Promise.all([
      updateRolePermissions(id, permRoleId.value, permSelectedCodes.value),
      updateAppRoleDataScope(id, permRoleId.value, {
        dataScope: permDataScope.value,
        deptIds
      }),
      updateRolePages(id, permRoleId.value, pageIds),
      updateRoleFieldPermissions(id, permRoleId.value, fieldGroupsDraft.value)
    ]);
    message.success(t("org.role.roleConfigSaveSuccess"));
    permDrawerVisible.value = false;
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("org.role.roleConfigSaveFailed"));
  } finally {
    permSaving.value = false;
  }
}
</script>

<style scoped>
.app-organization {
  height: 100%;
}

.org-error {
  margin-bottom: 16px;
}

.org-layout {
  height: calc(100vh - 64px);
  background: #fff;
}

.org-sidebar {
  border-right: 1px solid #f0f0f0;
  overflow-y: auto;
  padding: 0;
}

.org-sidebar :deep(.ant-layout-sider-children) {
  display: flex;
  flex-direction: column;
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 20px 16px 12px;
  font-size: 16px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
}

.sidebar-icon {
  font-size: 18px;
  color: #1677ff;
}

.sidebar-nav-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  cursor: pointer;
  color: rgba(0, 0, 0, 0.65);
  border-radius: 6px;
  margin: 2px 8px;
  transition: background 0.2s, color 0.2s;
}

.sidebar-nav-item:hover {
  background: #f5f5f5;
}

.sidebar-nav-item.active {
  background: #e6f4ff;
  color: #1677ff;
  font-weight: 500;
}

.sidebar-nav-all {
  font-weight: 500;
  padding: 10px 16px;
  margin: 4px 8px 8px;
}

.sidebar-nav-sub {
  padding-left: 24px;
  font-size: 13px;
}

.nav-item-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nav-item-tag {
  flex-shrink: 0;
  font-size: 11px;
}

.sidebar-category-group {
  margin-top: 4px;
}

.sidebar-category-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px 4px;
}

.sidebar-category-label {
  font-size: 12px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.45);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.sidebar-category-add {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.35);
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s, color 0.2s;
}

.sidebar-category-header:hover .sidebar-category-add {
  opacity: 1;
}

.sidebar-category-add:hover {
  color: #1677ff;
}

.org-content {
  padding: 20px 24px;
  overflow-y: auto;
  background: #fff;
}

.content-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
}

.content-header-info {
  flex: 1;
}

.content-title {
  font-size: 20px;
  font-weight: 600;
  margin: 0 0 4px;
  color: rgba(0, 0, 0, 0.85);
}

.content-subtitle {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  margin: 0;
}

.content-search-bar {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
}

.search-result-count {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
}

.member-info-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.member-avatar {
  background: #1677ff;
  flex-shrink: 0;
}

.member-info-text {
  display: flex;
  flex-direction: column;
  line-height: 1.4;
}

.member-name {
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.member-account {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.status-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-right: 6px;
}

.status-active {
  background: #52c41a;
}

.status-disabled {
  background: #d9d9d9;
}

.perm-desc {
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.data-scope-radio-group {
  width: 100%;
}

.page-perm-tree {
  max-height: 420px;
  overflow-y: auto;
  padding: 8px 0;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.field-perm-table {
  margin-top: 4px;
}
</style>
