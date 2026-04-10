<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.loop.mode')">
      <a-select v-model:value="mode" @change="emitChange">
        <a-select-option value="count">{{ t('wfUi.forms.loop.modeCount') }}</a-select-option>
        <a-select-option value="while">{{ t('wfUi.forms.loop.modeWhile') }}</a-select-option>
        <a-select-option value="forEach">{{ t('wfUi.forms.loop.modeForEach') }}</a-select-option>
      </a-select>
    </a-form-item>

    <a-form-item :label="t('wfUi.forms.loop.maxIterations')">
      <a-input-number v-model:value="maxIterations" :min="1" :max="10000" style="width: 100%" @change="emitChange" />
    </a-form-item>

    <a-form-item :label="t('wfUi.forms.loop.indexVariable')">
      <a-input v-model:value="indexVariable" placeholder="loop_index" @change="emitChange" />
    </a-form-item>

    <a-form-item v-if="mode === 'while'" :label="t('wfUi.forms.loop.condition')">
      <a-textarea v-model:value="condition" :rows="3" :placeholder="t('wfUi.forms.loop.conditionPlaceholder')" @change="emitChange" />
    </a-form-item>

    <template v-if="mode === 'forEach'">
      <a-form-item :label="t('wfUi.forms.loop.collectionPath')">
        <a-input v-model:value="collectionPath" :placeholder="t('wfUi.forms.loop.collectionPathPlaceholder')" @change="emitChange" />
      </a-form-item>
      <a-form-item :label="t('wfUi.forms.loop.itemVariable')">
        <a-input v-model:value="itemVariable" placeholder="loop_item" @change="emitChange" />
      </a-form-item>
      <a-form-item :label="t('wfUi.forms.loop.itemIndexVariable')">
        <a-input v-model:value="itemIndexVariable" placeholder="loop_item_index" @change="emitChange" />
      </a-form-item>
    </template>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();
const { t } = useI18n();

const mode = computed<string>({
  get() {
    if (typeof props.configs.mode !== "string") {
      props.configs.mode = "count";
    }
    return props.configs.mode as string;
  },
  set(value) {
    props.configs.mode = value;
  }
});

const maxIterations = computed<number>({
  get() {
    if (typeof props.configs.maxIterations !== "number") {
      props.configs.maxIterations = 10;
    }
    return props.configs.maxIterations as number;
  },
  set(value) {
    props.configs.maxIterations = value;
  }
});

const indexVariable = computed<string>({
  get() {
    if (typeof props.configs.indexVariable !== "string") {
      props.configs.indexVariable = "loop_index";
    }
    return props.configs.indexVariable as string;
  },
  set(value) {
    props.configs.indexVariable = value;
  }
});

const condition = computed<string>({
  get() {
    if (typeof props.configs.condition !== "string") {
      props.configs.condition = "";
    }
    return props.configs.condition as string;
  },
  set(value) {
    props.configs.condition = value;
  }
});

const collectionPath = computed<string>({
  get() {
    if (typeof props.configs.collectionPath !== "string") {
      props.configs.collectionPath = "";
    }
    return props.configs.collectionPath as string;
  },
  set(value) {
    props.configs.collectionPath = value;
  }
});

const itemVariable = computed<string>({
  get() {
    if (typeof props.configs.itemVariable !== "string") {
      props.configs.itemVariable = "loop_item";
    }
    return props.configs.itemVariable as string;
  },
  set(value) {
    props.configs.itemVariable = value;
  }
});

const itemIndexVariable = computed<string>({
  get() {
    if (typeof props.configs.itemIndexVariable !== "string") {
      props.configs.itemIndexVariable = "loop_item_index";
    }
    return props.configs.itemIndexVariable as string;
  },
  set(value) {
    props.configs.itemIndexVariable = value;
  }
});

function emitChange() {
  emit("change");
}
</script>
