<template>
  <PageContainer :title="t('dynamic.sharedDesigner.title')">
    <DesignerCanvas />
  </PageContainer>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { DesignerCanvas } from "@atlas/designer-vue";
import { PageContainer } from "@atlas/shared-ui";

const { t } = useI18n();
</script>
