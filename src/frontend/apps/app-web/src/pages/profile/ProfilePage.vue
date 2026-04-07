<template>
  <div class="profile-page">
    <a-card :title="t('profile.pageTitle')" :bordered="false">
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :lg="12">
          <a-descriptions bordered size="small" :column="1">
            <a-descriptions-item :label="t('profile.username')">
              {{ profile?.username || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('profile.displayName')">
              {{ profile?.displayName || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('profile.tenantId')">
              {{ profile?.tenantId || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('profile.roles')">
              <a-space wrap>
                <a-tag v-for="role in profile?.roles ?? []" :key="role">{{ role }}</a-tag>
                <span v-if="(profile?.roles?.length ?? 0) === 0">-</span>
              </a-space>
            </a-descriptions-item>
            <a-descriptions-item :label="t('profile.email')">
              {{ userDetail?.email || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('profile.phone')">
              {{ userDetail?.phoneNumber || "-" }}
            </a-descriptions-item>
          </a-descriptions>
        </a-col>

        <a-col :xs="24" :lg="12">
          <a-form :model="formModel" :rules="rules" layout="vertical">
            <a-form-item :label="t('profile.labelDisplayName')" name="displayName">
              <a-input v-model:value="formModel.displayName" :placeholder="t('profile.placeholderDisplayName')" />
            </a-form-item>
            <a-form-item :label="t('profile.labelEmail')" name="email">
              <a-input v-model:value="formModel.email" :placeholder="t('profile.placeholderEmail')" />
            </a-form-item>
            <a-form-item :label="t('profile.labelPhone')" name="phoneNumber">
              <a-input v-model:value="formModel.phoneNumber" :placeholder="t('profile.placeholderPhone')" />
            </a-form-item>
            <a-space>
              <a-button type="primary" :loading="saving" @click="submit">{{ t("profile.save") }}</a-button>
              <a-button :disabled="saving" @click="resetForm">{{ t("profile.reset") }}</a-button>
            </a-space>
          </a-form>
        </a-col>
      </a-row>
    </a-card>

    <a-card :title="t('profile.passwordCardTitle')" :bordered="false" class="password-card">
      <a-form ref="passwordFormRef" :model="passwordModel" :rules="passwordRules" layout="vertical">
        <a-row :gutter="[16, 0]">
          <a-col :xs="24" :md="12">
            <a-form-item :label="t('profile.currentPassword')" name="currentPassword">
              <a-input-password v-model:value="passwordModel.currentPassword" :placeholder="t('profile.placeholderCurrentPassword')" />
            </a-form-item>
          </a-col>
          <a-col :xs="24" :md="12">
            <a-form-item :label="t('profile.newPassword')" name="newPassword">
              <a-input-password v-model:value="passwordModel.newPassword" :placeholder="t('profile.placeholderNewPassword')" />
            </a-form-item>
          </a-col>
          <a-col :xs="24" :md="12">
            <a-form-item :label="t('profile.confirmPassword')" name="confirmPassword">
              <a-input-password v-model:value="passwordModel.confirmPassword" :placeholder="t('profile.placeholderConfirmPassword')" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-space>
          <a-button type="primary" :loading="changing" @click="submitPassword">{{ t("profile.updatePassword") }}</a-button>
          <a-button :disabled="changing" @click="resetPasswordForm">{{ t("profile.resetPassword") }}</a-button>
        </a-space>
      </a-form>
    </a-card>

    <a-card :title="t('profile.mfaCardTitle')" :bordered="false" class="mfa-card">
      <div v-if="mfaStatusLoading">{{ t("profile.mfaLoading") }}</div>
      <div v-else>
        <template v-if="mfaEnabled">
          <a-alert type="success" :message="t('profile.mfaEnabledTitle')" :description="t('profile.mfaEnabledDesc')" show-icon style="margin-bottom: 16px" />
          <a-form layout="inline">
            <a-form-item :label="t('profile.mfaTotpCode')">
              <a-input v-model:value="mfaCodeInput" :placeholder="t('profile.placeholderTotp')" />
            </a-form-item>
            <a-form-item>
              <a-button danger :loading="disablingMfa" @click="handleDisableMfa">{{ t("profile.disableMfa") }}</a-button>
            </a-form-item>
          </a-form>
        </template>
        <template v-else>
          <a-alert type="warning" :message="t('profile.mfaDisabledTitle')" :description="t('profile.mfaDisabledDesc')" show-icon style="margin-bottom: 16px" />
          <a-button type="primary" :loading="settingMfa" @click="handleSetupMfa">{{ t("profile.setupMfa") }}</a-button>

          <div v-if="mfaSetupContext" class="mfa-setup-area">
            <a-divider />
            <h3>{{ t("profile.mfaScanTitle") }}</h3>
            <a-qrcode :value="mfaSetupContext.provisioningUri" :size="200" />
            <p>{{ t("profile.mfaSecretHint") }}<a-typography-text copyable>{{ mfaSetupContext.secretKey }}</a-typography-text></p>
            <a-form layout="inline" style="margin-top: 16px">
              <a-form-item :label="t('profile.mfa6DigitCode')">
                <a-input v-model:value="mfaCodeInput" :placeholder="t('profile.placeholderBindCode')" />
              </a-form-item>
              <a-form-item>
                <a-button type="primary" :loading="verifyingMfa" @click="handleVerifyMfa">{{ t("profile.verifyAndEnable") }}</a-button>
              </a-form-item>
            </a-form>
          </div>
        </template>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { clearAuthStorage, getAuthProfile, setAuthProfile } from "@atlas/shared-core";
import type { AuthProfile, ChangePasswordRequest, UserProfileDetail, UserProfileUpdateRequest } from "@atlas/shared-core";
import {
  changePassword,
  getCurrentUser,
  getProfileDetail,
  updateProfile,
  getMfaStatus,
  setupMfa,
  verifyMfaSetup,
  disableMfa,
  type MfaSetupResult
} from "@/services/api-profile";
import { logout as apiLogout } from "@/services/api-auth";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

const appKey = computed(() => String(route.params.appKey ?? ""));

const profile = ref<AuthProfile | null>(null);
const userDetail = ref<UserProfileDetail | null>(null);
const saving = ref(false);
const changing = ref(false);
const passwordFormRef = ref();

const formModel = reactive<UserProfileUpdateRequest>({
  displayName: "",
  email: "",
  phoneNumber: ""
});

const rules = computed(() => ({
  displayName: [{ required: true, message: t("profile.displayNameRequired") }]
}));

const passwordModel = reactive<ChangePasswordRequest>({
  currentPassword: "",
  newPassword: "",
  confirmPassword: ""
});

const passwordRules = computed(() => ({
  currentPassword: [{ required: true, message: t("profile.currentPasswordRequired") }],
  newPassword: [{ required: true, message: t("profile.newPasswordRequired") }],
  confirmPassword: [
    { required: true, message: t("profile.confirmPasswordRequired") },
    {
      validator: (_: unknown, value: string) =>
        value === passwordModel.newPassword ? Promise.resolve() : Promise.reject(t("profile.passwordMismatch"))
    }
  ]
}));

const loadProfile = async () => {
  const cached = getAuthProfile();
  if (cached) profile.value = cached;
  try {
    const result = await getCurrentUser();
    if (!isMounted.value) return;
    profile.value = result;
    setAuthProfile(result);
  } catch (error) {
    message.error((error as Error).message || t("profile.fetchUserFailed"));
  }
};

const loadDetail = async () => {
  try {
    const detail = await getProfileDetail();
    if (!isMounted.value) return;
    userDetail.value = detail;
    formModel.displayName = detail.displayName;
    formModel.email = detail.email ?? "";
    formModel.phoneNumber = detail.phoneNumber ?? "";
  } catch (error) {
    message.error((error as Error).message || t("profile.loadProfileFailed"));
  }
};

const resetForm = () => {
  if (!userDetail.value) return;
  formModel.displayName = userDetail.value.displayName;
  formModel.email = userDetail.value.email ?? "";
  formModel.phoneNumber = userDetail.value.phoneNumber ?? "";
};

const submit = async () => {
  saving.value = true;
  try {
    await updateProfile({
      displayName: formModel.displayName,
      email: formModel.email || undefined,
      phoneNumber: formModel.phoneNumber || undefined
    });
    if (!isMounted.value) return;
    message.success(t("profile.saveSuccess"));
    await Promise.allSettled([loadProfile(), loadDetail()]);
  } catch (error) {
    message.error((error as Error).message || t("profile.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const resetPasswordForm = () => {
  passwordModel.currentPassword = "";
  passwordModel.newPassword = "";
  passwordModel.confirmPassword = "";
  passwordFormRef.value?.clearValidate?.();
};

const submitPassword = async () => {
  try {
    await passwordFormRef.value?.validate();
    if (!isMounted.value) return;
  } catch { return; }

  changing.value = true;
  try {
    await changePassword({
      currentPassword: passwordModel.currentPassword,
      newPassword: passwordModel.newPassword,
      confirmPassword: passwordModel.confirmPassword
    });
    if (!isMounted.value) return;
    message.success(t("profile.passwordUpdatedRelogin"));
    resetPasswordForm();
    await logoutAndRedirect();
  } catch (error) {
    message.error((error as Error).message || t("profile.changePasswordFailed"));
  } finally {
    changing.value = false;
  }
};

const logoutAndRedirect = async () => {
  try { await apiLogout(); } catch { /* ignore */ }
  clearAuthStorage();
  router.push({ name: "app-login", params: { appKey: appKey.value } });
};

const mfaEnabled = ref(false);
const mfaStatusLoading = ref(true);
const settingMfa = ref(false);
const disablingMfa = ref(false);
const verifyingMfa = ref(false);
const mfaSetupContext = ref<MfaSetupResult | null>(null);
const mfaCodeInput = ref("");

const loadMfaStatus = async () => {
  mfaStatusLoading.value = true;
  try {
    const status = await getMfaStatus();
    if (!isMounted.value) return;
    mfaEnabled.value = status.mfaEnabled;
  } catch (error) {
    message.error(t("profile.loadMfaStatusFailed") + ((error as Error).message || t("profile.unknownError")));
  } finally {
    mfaStatusLoading.value = false;
  }
};

const handleSetupMfa = async () => {
  settingMfa.value = true;
  mfaCodeInput.value = "";
  try {
    const result = await setupMfa();
    if (!isMounted.value) return;
    mfaSetupContext.value = result;
  } catch (error) {
    message.error(t("profile.setupMfaFailed") + ((error as Error).message || t("profile.unknownError")));
  } finally {
    settingMfa.value = false;
  }
};

const handleVerifyMfa = async () => {
  if (!mfaCodeInput.value) {
    message.warning(t("profile.enterOtpWarning"));
    return;
  }
  verifyingMfa.value = true;
  try {
    const success = await verifyMfaSetup(mfaCodeInput.value);
    if (!isMounted.value) return;
    if (success) {
      message.success(t("profile.mfaEnableSuccess"));
      mfaEnabled.value = true;
      mfaSetupContext.value = null;
      mfaCodeInput.value = "";
    }
  } catch (error) {
    message.error(t("profile.mfaVerifyFailed") + ((error as Error).message || t("profile.mfaVerifyHint")));
  } finally {
    verifyingMfa.value = false;
  }
};

const handleDisableMfa = async () => {
  if (!mfaCodeInput.value) {
    message.warning(t("profile.enterOtpToDisable"));
    return;
  }
  disablingMfa.value = true;
  try {
    const success = await disableMfa(mfaCodeInput.value);
    if (!isMounted.value) return;
    if (success) {
      message.success(t("profile.mfaDisableSuccess"));
      mfaEnabled.value = false;
      mfaCodeInput.value = "";
    }
  } catch (error) {
    message.error(t("profile.mfaDisableFailed") + ((error as Error).message || t("profile.mfaDisableHint")));
  } finally {
    disablingMfa.value = false;
  }
};

onMounted(async () => {
  await Promise.allSettled([loadProfile(), loadDetail(), loadMfaStatus()]);
});
</script>

<style scoped>
.profile-page {
  padding: 24px;
}

.password-card, .mfa-card {
  margin-top: 16px;
}

.mfa-setup-area {
  margin-top: 24px;
}
</style>
