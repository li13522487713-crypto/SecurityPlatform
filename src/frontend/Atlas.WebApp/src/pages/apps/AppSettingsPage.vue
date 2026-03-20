<template>
  <div class="app-settings-page">
    <a-page-header title="应用设置" sub-title="管理应用绑定数据源、共享策略与实体别名" />

    <a-row :gutter="[16, 16]" class="mt12">
      <a-col :span="24">
        <a-card title="数据源绑定">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="数据源ID">
              {{ dataSourceInfo?.dataSourceId || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="数据源名称">
              {{ dataSourceInfo?.name || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="数据库类型">
              {{ dataSourceInfo?.dbType || "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="连接池上限">
              {{ dataSourceInfo?.maxPoolSize ?? "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="连接超时(秒)">
              {{ dataSourceInfo?.connectionTimeoutSeconds ?? "-" }}
            </a-descriptions-item>
            <a-descriptions-item label="最近测试">
              <a-tag v-if="dataSourceInfo?.lastTestSuccess === true" color="green">成功</a-tag>
              <a-tag v-else-if="dataSourceInfo?.lastTestSuccess === false" color="red">失败</a-tag>
              <span v-else>-</span>
            </a-descriptions-item>
          </a-descriptions>

          <a-space class="mt12">
            <a-button
              type="primary"
              :disabled="!dataSourceInfo?.dataSourceId"
              :loading="testingDataSource"
              @click="handleTestDataSource"
            >
              测试连接
            </a-button>
            <a-button @click="go('/console/datasources')">前往数据源管理</a-button>
            <a-button
              v-if="!dataSourceInfo?.dataSourceId"
              :loading="bindingDataSource"
              @click="handleOpenBindDataSource"
            >
              绑定数据源
            </a-button>
            <a-button
              v-else
              :loading="bindingDataSource"
              @click="handleOpenSwitchDataSource"
            >
              切换数据源
            </a-button>
            <a-popconfirm
              v-if="dataSourceInfo?.dataSourceId"
              title="确认解绑当前数据源？"
              ok-text="确认解绑"
              cancel-text="取消"
              @confirm="handleUnbindDataSource"
            >
              <a-button danger :loading="bindingDataSource">解绑数据源</a-button>
            </a-popconfirm>
          </a-space>
        </a-card>
      </a-col>

      <a-col :span="24">
        <a-card title="共享策略">
          <a-alert
            type="info"
            show-icon
            message="共享/隔离模型"
            description="开启表示“继承平台”，关闭表示“应用独立（隔离）”。切换到隔离模式时建议绑定应用专属数据源。"
            style="margin-bottom: 12px"
          />
          <a-alert
            v-if="hasIsolatedPolicy && !dataSourceInfo?.dataSourceId"
            type="warning"
            show-icon
            message="当前已选择隔离模式，但应用尚未绑定独立数据源"
            description="请先在创建阶段或应用配置中绑定数据源后再继续使用隔离策略，避免配置与数据边界不一致。"
            style="margin-bottom: 12px"
          />
          <a-space direction="vertical" style="width: 100%">
            <div class="policy-row">
              <div class="policy-title">用户账号来源</div>
              <a-switch
                v-model:checked="sharingPolicy.useSharedUsers"
                checked-children="继承平台"
                un-checked-children="应用独立"
              />
              <a-tag :color="sharingPolicy.useSharedUsers ? 'processing' : 'warning'">
                {{ sharingPolicy.useSharedUsers ? "共享" : "隔离" }}
              </a-tag>
            </div>
            <div class="policy-row-desc">控制登录账号、用户基础档案是否复用平台统一用户池。</div>

            <div class="policy-row">
              <div class="policy-title">角色权限来源</div>
              <a-switch
                v-model:checked="sharingPolicy.useSharedRoles"
                checked-children="继承平台"
                un-checked-children="应用独立"
              />
              <a-tag :color="sharingPolicy.useSharedRoles ? 'processing' : 'warning'">
                {{ sharingPolicy.useSharedRoles ? "共享" : "隔离" }}
              </a-tag>
            </div>
            <div class="policy-row-desc">控制角色定义、权限模型是否复用平台角色体系。</div>

            <div class="policy-row">
              <div class="policy-title">部门组织来源</div>
              <a-switch
                v-model:checked="sharingPolicy.useSharedDepartments"
                checked-children="继承平台"
                un-checked-children="应用独立"
              />
              <a-tag :color="sharingPolicy.useSharedDepartments ? 'processing' : 'warning'">
                {{ sharingPolicy.useSharedDepartments ? "共享" : "隔离" }}
              </a-tag>
            </div>
            <div class="policy-row-desc">控制组织架构与部门树是否复用平台主数据。</div>

            <a-button type="primary" :loading="savingPolicy" @click="saveSharingPolicy">保存共享策略</a-button>
          </a-space>
        </a-card>
      </a-col>

      <a-col :span="24">
        <a-card title="实体别名">
          <a-table :data-source="entityAliases" :pagination="false" row-key="entityType" bordered size="small">
            <a-table-column title="实体类型" data-index="entityType" key="entityType" width="180" />
            <a-table-column title="单数别名" key="singularAlias">
              <template #default="{ record }">
                <a-input v-model:value="record.singularAlias" />
              </template>
            </a-table-column>
            <a-table-column title="复数别名" key="pluralAlias">
              <template #default="{ record }">
                <a-input v-model:value="record.pluralAlias" />
              </template>
            </a-table-column>
          </a-table>
          <a-button type="primary" class="mt12" :loading="savingAliases" @click="saveEntityAliases">
            保存实体别名
          </a-button>
        </a-card>
      </a-col>
    </a-row>

    <a-modal
      v-model:open="dataSourceSelectorVisible"
      :title="dataSourceSelectionMode === 'bind' ? '绑定数据源' : '切换数据源'"
      ok-text="确认"
      cancel-text="取消"
      :confirm-loading="bindingDataSource"
      @ok="handleConfirmDataSourceSelection"
    >
      <a-alert
        type="info"
        show-icon
        message="请选择要绑定的数据源"
        description="默认展示 20 条，支持输入关键字远程检索。"
        style="margin-bottom: 12px"
      />
      <a-select
        v-model:value="selectedDataSourceId"
        style="width: 100%"
        :options="dataSourceOptions"
        show-search
        allow-clear
        :loading="loadingDataSourceOptions"
        :filter-option="false"
        placeholder="请选择数据源"
        @search="handleSearchDataSources"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import type { TenantDataSourceDto } from "@/types/api";
import type {
  LowCodeAppDataSourceInfo,
  LowCodeAppEntityAliasItem
} from "@/types/lowcode";
import type { TenantAppInstanceDetail } from "@/types/platform-v2";
import { getTenantDataSources } from "@/services/api-system";
import { getTenantAppInstanceDetail, updateTenantAppInstance } from "@/services/api-tenant-app-instances";
import {
  getLowCodeAppDataSourceInfo,
  getLowCodeAppEntityAliases,
  getLowCodeAppSharingPolicy,
  testLowCodeAppDataSource,
  updateLowCodeAppEntityAliases,
  updateLowCodeAppSharingPolicy
} from "@/services/lowcode";
import { debounce } from "@/utils/common";

const route = useRoute();
const router = useRouter();
const appId = computed(() => String(route.params.appId ?? ""));

const dataSourceInfo = ref<LowCodeAppDataSourceInfo | null>(null);
const tenantAppDetail = ref<TenantAppInstanceDetail | null>(null);
const testingDataSource = ref(false);
const bindingDataSource = ref(false);
const savingPolicy = ref(false);
const savingAliases = ref(false);
const dataSourceSelectorVisible = ref(false);
const dataSourceSelectionMode = ref<"bind" | "switch">("bind");
const loadingDataSourceOptions = ref(false);
const selectedDataSourceId = ref<string>();
const dataSourceOptions = ref<Array<{ label: string; value: string }>>([]);

const sharingPolicy = reactive({
  useSharedUsers: true,
  useSharedRoles: true,
  useSharedDepartments: true
});
const originalSharingPolicy = ref({
  useSharedUsers: true,
  useSharedRoles: true,
  useSharedDepartments: true
});
const hasIsolatedPolicy = computed(() =>
  !sharingPolicy.useSharedUsers
  || !sharingPolicy.useSharedRoles
  || !sharingPolicy.useSharedDepartments
);
const isFullySharedPolicy = computed(() =>
  sharingPolicy.useSharedUsers
  && sharingPolicy.useSharedRoles
  && sharingPolicy.useSharedDepartments
);

const entityAliases = ref<LowCodeAppEntityAliasItem[]>([
  { entityType: "users", singularAlias: "用户", pluralAlias: "用户列表" },
  { entityType: "roles", singularAlias: "角色", pluralAlias: "角色列表" },
  { entityType: "departments", singularAlias: "部门", pluralAlias: "部门列表" }
]);

function go(path: string) {
  router.push(path);
}

async function loadSettings() {
  if (!appId.value) return;

  try {
    const [dataSource, policy, aliases, appDetail] = await Promise.all([
      getLowCodeAppDataSourceInfo(appId.value),
      getLowCodeAppSharingPolicy(appId.value),
      getLowCodeAppEntityAliases(appId.value),
      getTenantAppInstanceDetail(appId.value)
    ]);

    dataSourceInfo.value = dataSource;
    tenantAppDetail.value = appDetail;
    if (policy) {
      sharingPolicy.useSharedUsers = policy.useSharedUsers;
      sharingPolicy.useSharedRoles = policy.useSharedRoles;
      sharingPolicy.useSharedDepartments = policy.useSharedDepartments;
      originalSharingPolicy.value = {
        useSharedUsers: policy.useSharedUsers,
        useSharedRoles: policy.useSharedRoles,
        useSharedDepartments: policy.useSharedDepartments
      };
    }
    if (aliases.length > 0) {
      entityAliases.value = aliases;
    }
  } catch (error) {
    message.error((error as Error).message || "加载应用设置失败");
  }
}

async function loadDataSourceOptions(keyword = "") {
  loadingDataSourceOptions.value = true;
  try {
    const allDataSources = await getTenantDataSources();
    dataSourceOptions.value = mapDataSourceOptions(allDataSources, keyword);
  } catch (error) {
    message.error((error as Error).message || "加载数据源列表失败");
  } finally {
    loadingDataSourceOptions.value = false;
  }
}

function mapDataSourceOptions(dataSources: TenantDataSourceDto[], keyword: string) {
  const normalizedKeyword = keyword.trim().toLowerCase();
  return dataSources
    .filter((item) => {
      if (item.appId && item.appId !== appId.value) {
        return false;
      }
      if (!normalizedKeyword) {
        return true;
      }
      return item.name.toLowerCase().includes(normalizedKeyword)
        || item.dbType.toLowerCase().includes(normalizedKeyword)
        || item.id.toLowerCase().includes(normalizedKeyword);
    })
    .slice(0, 20)
    .map((item) => ({
      value: item.id,
      label: `${item.name}（${item.dbType}）`
    }));
}

const handleSearchDataSources = debounce((value: string) => {
  void loadDataSourceOptions(value);
}, 300);

function handleOpenBindDataSource() {
  openDataSourceSelector("bind");
}

function handleOpenSwitchDataSource() {
  openDataSourceSelector("switch");
}

function openDataSourceSelector(mode: "bind" | "switch") {
  const openSelector = () => {
    dataSourceSelectionMode.value = mode;
    selectedDataSourceId.value = dataSourceInfo.value?.dataSourceId || undefined;
    dataSourceSelectorVisible.value = true;
    void loadDataSourceOptions();
  };

  if (isFullySharedPolicy.value) {
    Modal.confirm({
      title: "共享模式提示",
      content: "隔离模式下才需要绑定独立数据源，当前为共享模式是否继续？",
      okText: "继续",
      cancelText: "取消",
      onOk: openSelector
    });
    return;
  }

  openSelector();
}

async function ensureTenantAppDetail() {
  if (!appId.value) {
    return null;
  }

  if (tenantAppDetail.value) {
    return tenantAppDetail.value;
  }

  tenantAppDetail.value = await getTenantAppInstanceDetail(appId.value);
  return tenantAppDetail.value;
}

async function updateAppDataSourceBinding(targetDataSourceId: number | null) {
  if (!appId.value) {
    return;
  }

  const currentDetail = await ensureTenantAppDetail();
  if (!currentDetail) {
    return;
  }

  bindingDataSource.value = true;
  try {
    await updateTenantAppInstance(appId.value, {
      name: currentDetail.name,
      description: currentDetail.description,
      category: currentDetail.category,
      icon: currentDetail.icon,
      dataSourceId: targetDataSourceId,
      unbindDataSource: targetDataSourceId === null
    });
    await loadSettings();
  } catch (error) {
    message.error((error as Error).message || "更新应用数据源绑定失败");
    throw error;
  } finally {
    bindingDataSource.value = false;
  }
}

async function handleConfirmDataSourceSelection() {
  if (!selectedDataSourceId.value) {
    message.warning("请选择要绑定的数据源");
    return;
  }

  if (selectedDataSourceId.value === dataSourceInfo.value?.dataSourceId) {
    message.info("所选数据源与当前一致，无需切换");
    return;
  }

  await updateAppDataSourceBinding(Number(selectedDataSourceId.value));
  message.success(dataSourceSelectionMode.value === "bind" ? "数据源绑定成功" : "数据源切换成功");
  dataSourceSelectorVisible.value = false;
}

async function handleUnbindDataSource() {
  await updateAppDataSourceBinding(null);
  message.success("数据源已解绑");
}

async function handleTestDataSource() {
  if (!appId.value) return;
  testingDataSource.value = true;
  try {
    const result = await testLowCodeAppDataSource(appId.value);
    if (result.success) {
      message.success("数据源连接测试成功");
    } else {
      message.error(result.errorMessage || "数据源连接测试失败");
    }
    dataSourceInfo.value = await getLowCodeAppDataSourceInfo(appId.value);
  } catch (error) {
    message.error((error as Error).message || "测试数据源失败");
  } finally {
    testingDataSource.value = false;
  }
}

function buildSharingPolicyImpactMessages() {
  const messages: string[] = [];
  const previous = originalSharingPolicy.value;
  const current = sharingPolicy;

  if (previous.useSharedUsers !== current.useSharedUsers) {
    messages.push(current.useSharedUsers
      ? "用户：隔离 → 共享。应用级独立用户配置将被平台共享用户体系覆盖。"
      : "用户：共享 → 隔离。应用将使用独立用户池，现有平台用户将无法登录该应用，需在应用内重新配置成员。");
  }

  if (previous.useSharedRoles !== current.useSharedRoles) {
    messages.push(current.useSharedRoles
      ? "角色：隔离 → 共享。应用级独立角色配置将被平台共享角色体系覆盖。"
      : "角色：共享 → 隔离。应用将使用独立角色体系，现有平台角色绑定失效，需在应用内重新配置权限。");
  }

  if (previous.useSharedDepartments !== current.useSharedDepartments) {
    messages.push(current.useSharedDepartments
      ? "部门：隔离 → 共享。应用级独立部门配置将被平台共享组织体系覆盖。"
      : "部门：共享 → 隔离。数据权限中的“本部门”等范围将以应用独立部门树为准。");
  }

  return messages;
}

async function persistSharingPolicy() {
  if (!appId.value) return;
  if (hasIsolatedPolicy.value && !dataSourceInfo.value?.dataSourceId && !savingPolicy.value) {
    message.warning("隔离模式下建议先绑定应用专属数据源");
  }

  savingPolicy.value = true;
  try {
    await updateLowCodeAppSharingPolicy(appId.value, {
      useSharedUsers: sharingPolicy.useSharedUsers,
      useSharedRoles: sharingPolicy.useSharedRoles,
      useSharedDepartments: sharingPolicy.useSharedDepartments
    });
    originalSharingPolicy.value = {
      useSharedUsers: sharingPolicy.useSharedUsers,
      useSharedRoles: sharingPolicy.useSharedRoles,
      useSharedDepartments: sharingPolicy.useSharedDepartments
    };
    message.success("共享策略已保存");
  } catch (error) {
    message.error((error as Error).message || "保存共享策略失败");
  } finally {
    savingPolicy.value = false;
  }
}

function saveSharingPolicy() {
  const impactMessages = buildSharingPolicyImpactMessages();
  if (impactMessages.length === 0) {
    void persistSharingPolicy();
    return;
  }

  Modal.confirm({
    title: "共享策略影响分析",
    okText: "我已了解影响，确认切换",
    cancelText: "取消",
    content: h("div", impactMessages.map((item) => h("p", { style: "margin-bottom: 6px;" }, item))),
    onOk: () => persistSharingPolicy()
  });
}

async function saveEntityAliases() {
  if (!appId.value) return;
  savingAliases.value = true;
  try {
    await updateLowCodeAppEntityAliases(appId.value, {
      items: entityAliases.value.map((item) => ({
        entityType: item.entityType.trim(),
        singularAlias: item.singularAlias.trim(),
        pluralAlias: item.pluralAlias.trim()
      }))
    });
    message.success("实体别名已保存");
  } catch (error) {
    message.error((error as Error).message || "保存实体别名失败");
  } finally {
    savingAliases.value = false;
  }
}

onMounted(loadSettings);
</script>

<style scoped>
.app-settings-page {
  padding: 8px;
}

.mt12 {
  margin-top: 12px;
}

.policy-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.policy-title {
  min-width: 120px;
  font-weight: 500;
}

.policy-row-desc {
  color: #8c8c8c;
  font-size: 12px;
  margin: -6px 0 8px 0;
}
</style>
