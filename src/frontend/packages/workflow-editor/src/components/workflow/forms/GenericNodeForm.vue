<template>
  <a-alert type="info" :message="t('wfUi.forms.generic.advancedConfigTip')" show-icon />
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();
</script>
