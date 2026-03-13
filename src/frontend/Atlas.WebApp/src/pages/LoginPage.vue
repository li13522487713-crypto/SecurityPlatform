<template>
  <div class="login-page">
    <!-- 左侧品牌面板 -->
    <aside class="brand-panel">
      <div class="brand-content">
        <div class="brand-logo">
          <div class="logo-icon">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(255,255,255,0.15)" stroke="rgba(255,255,255,0.6)" stroke-width="1.5"/>
              <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(255,255,255,0.25)" stroke="#fff" stroke-width="1.5"/>
              <circle cx="16" cy="16" r="3" fill="#fff"/>
            </svg>
          </div>
          <div class="brand-text">
            <h1>Atlas 安全平台</h1>
            <p>Security Platform</p>
          </div>
        </div>
        <div class="brand-desc">
          <h2>统一安全管理与运维管控</h2>
          <ul>
            <li>统一身份认证 · 多租户组织管理</li>
            <li>实时审计日志 · 风险策略自动落地</li>
            <li>资产清点盘查 · 合规告警可追溯</li>
          </ul>
        </div>
      </div>
      <div class="brand-footer">
        <span>符合等保 2.0 三级要求</span>
      </div>
      <!-- 装饰元素 -->
      <div class="decor decor-1" aria-hidden="true"></div>
      <div class="decor decor-2" aria-hidden="true"></div>
      <div class="decor decor-3" aria-hidden="true"></div>
    </aside>

    <!-- 右侧表单面板 -->
    <main class="form-panel">
      <div class="form-wrapper">
        <!-- 移动端 logo -->
        <div class="mobile-logo">
          <div class="logo-icon logo-icon--sm">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.1)" stroke="var(--color-primary)" stroke-width="1.5"/>
              <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.15)" stroke="var(--color-primary)" stroke-width="1.5"/>
              <circle cx="16" cy="16" r="3" fill="var(--color-primary)"/>
            </svg>
          </div>
          <span>Atlas 安全平台</span>
        </div>

        <h3 class="form-title">账号登录</h3>

        <!-- 授权证书区 -->
        <div class="license-section">
          <div v-if="licenseLoading" class="license-status">
            <a-spin size="small" />
            <span>正在验证授权证书…</span>
          </div>

          <template v-else-if="licenseStatusCode === 'Active'">
            <div class="license-status license-status--active">
              <span class="license-dot license-dot--active"></span>
              <span>已授权</span>
              <span v-if="licenseStatusInfo?.tenantName" class="license-org">
                {{ licenseStatusInfo.tenantName }}
              </span>
              <a-tag color="blue" size="small">{{ licenseStatusInfo?.edition }}</a-tag>
              <span class="license-expire">{{ licenseExpireText }}</span>
              <a class="license-action" @click="showRenewArea = !showRenewArea">
                {{ showRenewArea ? '收起' : '更换证书' }}
              </a>
            </div>
            <div v-if="showRenewArea" class="license-upload">
              <a-alert
                v-if="licenseActivateResult"
                :type="licenseActivateResult.success ? 'success' : 'error'"
                :message="licenseActivateResult.message"
                closable
                show-icon
                style="margin-bottom: 8px"
                @close="licenseActivateResult = null"
              />
              <a-upload
                :before-upload="handleLicenseFileSelect"
                :show-upload-list="false"
                accept=".atlaslicense,.lic,.txt"
              >
                <a-button size="small" :loading="licenseActivating">
                  <template #icon><upload-outlined /></template>
                  选择证书文件
                </a-button>
              </a-upload>
            </div>
          </template>

          <template v-else>
            <div class="license-status" :class="licenseStatusCode === 'Expired' ? 'license-status--expired' : 'license-status--none'">
              <span class="license-dot" :class="licenseStatusCode === 'Expired' ? 'license-dot--expired' : 'license-dot--none'"></span>
              <span>{{ licenseStatusCode === 'Expired' ? '授权已过期' : '未激活' }}</span>
              <span class="license-hint">
                {{ licenseStatusCode === 'Expired' ? '请续签证书' : '请上传授权证书' }}
              </span>
            </div>
            <a-alert
              v-if="licenseActivateResult"
              :type="licenseActivateResult.success ? 'success' : 'error'"
              :message="licenseActivateResult.message"
              closable
              show-icon
              style="margin: 8px 0"
              @close="licenseActivateResult = null"
            />
            <a-upload
              :before-upload="handleLicenseFileSelect"
              :show-upload-list="false"
              accept=".atlaslicense,.lic,.txt"
            >
              <a-button type="primary" size="small" :loading="licenseActivating">
                <template #icon><upload-outlined /></template>
                {{ licenseActivating ? '激活中…' : '上传证书' }}
              </a-button>
            </a-upload>
            <p class="license-tip">上传 <code>.atlaslicense</code> 证书文件以激活平台授权</p>
          </template>
        </div>

        <a-divider style="margin: 16px 0" />

        <!-- 登录表单 -->
        <template v-if="licenseStatusCode === 'Active'">
          <div v-if="errorMessage" class="error-banner">
            <span class="error-icon">!</span>
            <span>{{ errorMessage }}</span>
            <span v-if="cooldownSeconds > 0" class="cooldown-text">请 {{ cooldownSeconds }}s 后再试</span>
          </div>

          <a-form
            layout="vertical"
            :model="form"
            class="login-form"
            :disabled="loading"
            @finish="handleSubmit"
          >
            <a-form-item
              label="租户 / 组织"
              name="tenantId"
              :rules="[
                { required: true, message: '授权证书未提供租户 / 组织ID' },
                { pattern: /^[0-9a-fA-F-]{36}$/, message: '证书租户ID格式无效' }
              ]"
            >
              <a-input
                v-model:value="form.tenantId"
                placeholder="租户 / 组织 ID 来自授权证书"
                readonly
                autocomplete="off"
                @focus="errorMessage = ''"
              />
              <div class="field-tip">租户由证书自动绑定，如需变更请更换证书</div>
              <div v-if="!hasValidTenantId(form.tenantId.trim())" class="field-error">
                当前证书未提供有效租户ID，请更换正确证书
              </div>
            </a-form-item>

            <a-form-item
              label="账号"
              name="username"
              :rules="[{ required: true, message: '请输入账号' }]"
            >
              <a-input
                v-model:value="form.username"
                placeholder="手机号 / 邮箱 / 用户名"
                allow-clear
                autocomplete="username"
                @focus="errorMessage = ''"
              />
            </a-form-item>

            <a-form-item
              label="密码"
              name="password"
              :rules="[{ required: true, message: '请输入密码' }]"
            >
              <a-input-password
                v-model:value="form.password"
                placeholder="请输入密码"
                autocomplete="current-password"
                @keydown="handleCapsLockEvent"
                @keyup="handleCapsLockEvent"
                @blur="capsLockOn = false"
                @focus="errorMessage = ''"
              />
              <div v-if="capsLockOn" class="caps-tip">Caps Lock 已开启</div>
            </a-form-item>

            <div class="form-extra">
              <a-checkbox v-model:checked="form.rememberMe">记住我</a-checkbox>
              <a href="/password-reset" class="forgot-link">忘记密码？</a>
            </div>

            <a-button
              type="primary"
              block
              html-type="submit"
              size="large"
              :loading="loading"
              :disabled="isSubmitDisabled"
              class="submit-btn"
            >
              <span v-if="!loading">{{ cooldownSeconds > 0 ? `请稍候 (${cooldownSeconds}s)` : '登录' }}</span>
              <span v-else>登录中</span>
            </a-button>

            <div class="register-link">
              还没有账号？<router-link to="/register">立即注册</router-link>
            </div>
          </a-form>
        </template>

        <template v-else-if="!licenseLoading">
          <div class="login-locked">
            请先激活有效的授权证书，方可登录系统
          </div>
        </template>
      </div>

      <footer class="form-footer">
        <span>隐私政策</span>
        <span class="sep">·</span>
        <span>用户协议</span>
        <span class="sep">·</span>
        <span>v1.0.2</span>
      </footer>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { UploadOutlined } from "@ant-design/icons-vue";
import { getLicenseStatus, activateLicense } from "@/services/api";
import { useUserStore } from "@/stores/user";
import { usePermissionStore } from "@/stores/permission";
import type { RequestOptions } from "@/services/api";
import type { LicenseStatus } from "@/types/api";
import {
  clearAuthStorage,
  getTenantId
} from "@/utils/auth";

interface LoginApiError extends Error {
  status?: number;
  payload?: {
    code?: string;
    message?: string;
    traceId?: string;
  } | null;
}

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const COOLDOWN_THRESHOLD = 5;
const COOLDOWN_DURATION = 30;

const userStore = useUserStore();
const permissionStore = usePermissionStore();
const route = useRoute();
const router = useRouter();
const loading = ref(false);
const errorMessage = ref("");
const failedAttempts = ref(0);
const capsLockOn = ref(false);
const cooldownSeconds = ref(0);
let cooldownTimer: number | undefined;

const REMEMBER_ME_KEY = "atlas-login-remember-me";

const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: "",
  rememberMe: localStorage.getItem(REMEMBER_ME_KEY) === "true"
});

const licenseLoading = ref(true);
const licenseActivating = ref(false);
const licenseActivateResult = ref<{ success: boolean; message: string } | null>(null);
const licenseStatusCode = ref<string>("None");
const licenseStatusInfo = ref<LicenseStatus | null>(null);
const showRenewArea = ref(false);

const licenseExpireText = computed(() => {
  const info = licenseStatusInfo.value;
  if (!info) return "";
  if (info.isPermanent) return "永久授权";
  if (info.remainingDays !== null && info.remainingDays !== undefined) {
    return `剩余 ${info.remainingDays} 天`;
  }
  if (info.expiresAt) {
    return `到期：${info.expiresAt.substring(0, 10)}`;
  }
  return "";
});

const isSubmitDisabled = computed(
  () =>
    loading.value ||
    cooldownSeconds.value > 0 ||
    !hasValidTenantId(form.tenantId.trim()) ||
    !form.username.trim() ||
    !form.password ||
    licenseStatusCode.value !== "Active"
);

const handleCapsLockEvent = (event: KeyboardEvent) => {
  if (typeof event.getModifierState === "function") {
    capsLockOn.value = event.getModifierState("CapsLock");
  }
};

const startCooldown = () => {
  cooldownSeconds.value = COOLDOWN_DURATION;
  window.clearInterval(cooldownTimer);
  cooldownTimer = window.setInterval(() => {
    cooldownSeconds.value -= 1;
    if (cooldownSeconds.value <= 0) {
      window.clearInterval(cooldownTimer);
      cooldownSeconds.value = 0;
    }
  }, 1000);
};

const normalizeError = (error: unknown) => {
  const loginError = error as LoginApiError;
  const code = loginError?.payload?.code ?? "";
  const traceId = loginError?.payload?.traceId;
  const raw = error instanceof Error ? error.message : "登录失败";

  if (code === "INVALID_CREDENTIALS" || raw.includes("账号或密码")) {
    return "账号或密码错误，请重试";
  }
  if (code === "ACCOUNT_LOCKED") {
    return "账号已被锁定，请联系管理员或稍后再试";
  }
  if (code === "PASSWORD_EXPIRED") {
    return "密码已过期，请联系管理员重置密码后登录";
  }
  if (code === "TENANT_NOT_FOUND") {
    return "租户不存在，请检查租户ID是否正确";
  }
  if (code === "VALIDATION_ERROR") {
    return raw || "参数校验失败，请检查输入后重试";
  }
  if (raw.toLowerCase().includes("网络")) {
    return "网络异常，请稍后再试";
  }
  if (traceId) {
    return `${raw}（traceId: ${traceId}）`;
  }
  return raw;
};

const hasValidTenantId = (tenantId: string) => TENANT_ID_REGEX.test(tenantId);

const handleSubmit = async () => {
  errorMessage.value = "";
  loading.value = true;
  try {
    clearAuthStorage();
    const tenantId = form.tenantId.trim();
    const tokenOptions: RequestOptions = { suppressErrorMessage: true };

    await userStore.login(
      tenantId,
      form.username.trim(),
      form.password,
      tokenOptions,
      {
        rememberMe: form.rememberMe
      }
    );
    await userStore.getInfo();
    const routes = await permissionStore.generateRoutes();
    permissionStore.registerRoutes(router);
    localStorage.setItem(REMEMBER_ME_KEY, String(form.rememberMe));
    failedAttempts.value = 0;
    cooldownSeconds.value = 0;
    errorMessage.value = "";
    const rawRedirect = route.query.redirect;
    const redirect =
      typeof rawRedirect === "string" &&
      rawRedirect.startsWith("/") &&
      !rawRedirect.startsWith("//")
        ? rawRedirect
        : null;
    const targetPath = redirect ?? "/console";
    const canNavigate = routes.some((item) => typeof item.path === "string" && targetPath.startsWith(item.path));
    const fallbackPath = "/console";
    const staticAllowedTargets = new Set(["/console"]);
    const isUnsafeRedirect =
      targetPath === "/" ||
      targetPath.startsWith("/login") ||
      targetPath.startsWith("/register");
    router.push(!isUnsafeRedirect && (canNavigate || staticAllowedTargets.has(targetPath)) ? targetPath : fallbackPath);
  } catch (error) {
    clearAuthStorage();
    failedAttempts.value += 1;
    errorMessage.value = normalizeError(error);
    if (failedAttempts.value >= COOLDOWN_THRESHOLD) {
      startCooldown();
    }
  } finally {
    loading.value = false;
  }
};

async function handleLicenseFileSelect(file: File): Promise<false> {
  licenseActivating.value = true;
  licenseActivateResult.value = null;

  let content = "";
  try {
    content = await readFileAsText(file);
  } catch (error) {
    licenseActivateResult.value = {
      success: false,
      message: error instanceof Error ? error.message : "文件读取失败，请重试"
    };
    licenseActivating.value = false;
    return false;
  }

  try {
    const resp = await activateLicense(content);
    if (resp.success) {
      licenseActivateResult.value = {
        success: true,
        message: resp.data?.message ?? resp.message ?? "授权激活成功！"
      };
      showRenewArea.value = false;
      await loadLicenseStatus();
    } else {
      licenseActivateResult.value = {
        success: false,
        message: resp.message || "证书激活失败"
      };
    }
  } catch (error) {
    const requestError = error as LoginApiError;
    const detailMessage =
      requestError?.payload?.message ??
      (error instanceof Error ? error.message : "");
    licenseActivateResult.value = {
      success: false,
      message: detailMessage || "证书激活失败，请重试"
    };
  } finally {
    licenseActivating.value = false;
  }
  return false;
}

function readFileAsText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => resolve((e.target?.result as string) ?? "");
    reader.onerror = () => reject(new Error("文件读取失败，请重试"));
    reader.readAsText(file);
  });
}

async function loadLicenseStatus() {
  licenseLoading.value = true;
  try {
    const status = await getLicenseStatus();
    licenseStatusCode.value = status.status;
    licenseStatusInfo.value = status;

    if (status.status === "Active" && status.tenantId && hasValidTenantId(status.tenantId)) {
      if (!form.tenantId || !hasValidTenantId(form.tenantId)) {
        form.tenantId = status.tenantId;
      }
    }
  } catch {
    licenseStatusCode.value = "None";
  } finally {
    licenseLoading.value = false;
  }
}

onMounted(() => {
  loadLicenseStatus();
});
onBeforeUnmount(() => {
  window.clearInterval(cooldownTimer);
});
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  background: #fff;
}

/* ── 左侧品牌面板 ── */
.brand-panel {
  width: 440px;
  min-height: 100vh;
  background: linear-gradient(160deg, #0d47a1 0%, #1565c0 40%, #1e88e5 100%);
  color: #fff;
  display: flex;
  flex-direction: column;
  padding: 48px 40px;
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.brand-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  position: relative;
  z-index: 1;
}

.brand-logo {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 48px;
}

.logo-icon {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
}

.logo-icon svg {
  width: 100%;
  height: 100%;
}

.brand-text h1 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.brand-text p {
  margin: 2px 0 0;
  font-size: 12px;
  opacity: 0.65;
  letter-spacing: 1px;
}

.brand-desc h2 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 24px;
  line-height: 1.4;
}

.brand-desc ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.brand-desc li {
  padding: 8px 0;
  font-size: 14px;
  opacity: 0.85;
  position: relative;
  padding-left: 20px;
}

.brand-desc li::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.6);
}

.brand-footer {
  position: relative;
  z-index: 1;
  font-size: 12px;
  opacity: 0.5;
}

/* 装饰圆 */
.decor {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.04);
}

.decor-1 {
  width: 300px;
  height: 300px;
  bottom: -80px;
  right: -80px;
}

.decor-2 {
  width: 180px;
  height: 180px;
  top: -40px;
  right: 60px;
}

.decor-3 {
  width: 100px;
  height: 100px;
  bottom: 120px;
  left: -30px;
}

/* ── 右侧表单面板 ── */
.form-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
  min-height: 100vh;
  background: #fff;
}

.form-wrapper {
  width: 100%;
  max-width: 400px;
}

.mobile-logo {
  display: none;
  align-items: center;
  gap: 10px;
  margin-bottom: 32px;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.logo-icon--sm {
  width: 32px;
  height: 32px;
}

.form-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 24px;
}

/* ── 授权证书 ── */
.license-section {
  padding: 12px 16px;
  background: var(--color-bg-subtle);
  border-radius: var(--border-radius-md);
  border: 1px solid var(--color-border);
}

.license-status {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.license-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.license-dot--active { background: var(--color-success); }
.license-dot--expired { background: var(--color-warning); }
.license-dot--none { background: var(--color-text-quaternary); }

.license-status--active { color: var(--color-text-primary); }
.license-status--expired { color: var(--color-warning); }

.license-org {
  font-weight: 500;
  color: var(--color-text-primary);
}

.license-expire {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.license-action {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-primary);
  cursor: pointer;
}

.license-hint {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.license-upload {
  margin-top: 10px;
  padding: 10px;
  border: 1px dashed var(--color-border-secondary);
  border-radius: 6px;
  background: #fff;
}

.license-tip {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--color-text-tertiary);
}

/* ── 登录表单 ── */
.login-form :deep(.ant-form-item) {
  margin-bottom: 20px;
}

.login-form :deep(.ant-input),
.login-form :deep(.ant-input-password .ant-input) {
  height: 40px;
}

.field-tip {
  margin-top: 4px;
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.field-error {
  margin-top: 4px;
  color: var(--color-error-text);
  font-size: 12px;
}

.caps-tip {
  margin-top: 4px;
  color: var(--color-warning);
  font-size: 12px;
}

.form-extra {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.forgot-link {
  color: var(--color-primary);
  font-size: 14px;
}

.submit-btn {
  height: 44px;
  font-size: 16px;
  border-radius: var(--border-radius-md);
}

.register-link {
  text-align: center;
  margin-top: 16px;
  font-size: 14px;
  color: var(--color-text-secondary);
}

.register-link a {
  color: var(--color-primary);
}

.login-locked {
  padding: 24px 0;
  text-align: center;
  color: var(--color-text-tertiary);
  font-size: 14px;
}

/* ── 错误提示 ── */
.error-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--color-error-bg);
  border: 1px solid var(--color-error-border);
  color: var(--color-error-text);
  padding: 10px 14px;
  border-radius: var(--border-radius-md);
  margin-bottom: 16px;
  font-size: 14px;
}

.error-icon {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--color-error-text);
  color: #fff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  flex-shrink: 0;
}

.cooldown-text {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-text-tertiary);
  white-space: nowrap;
}

/* ── 页脚 ── */
.form-footer {
  margin-top: 48px;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-quaternary);
}

.form-footer .sep {
  margin: 0 6px;
}

/* ── 响应式 ── */
@media screen and (max-width: 960px) {
  .brand-panel {
    display: none;
  }

  .mobile-logo {
    display: flex;
  }

  .form-panel {
    padding: 32px 24px;
  }
}

@media screen and (max-width: 480px) {
  .form-panel {
    padding: 24px 16px;
  }

  .form-title {
    font-size: 20px;
  }

  .form-wrapper {
    max-width: 100%;
  }
}
</style>
