import { computed } from "vue";
import type { Ref } from "vue";

export type FeatureFlagMap = Record<string, boolean>;

export function useFeatureFlag(flags: Ref<FeatureFlagMap> | FeatureFlagMap) {
  const isRefInput = (input: Ref<FeatureFlagMap> | FeatureFlagMap): input is Ref<FeatureFlagMap> => {
    return typeof input === "object" && input !== null && "value" in input;
  };

  const source = computed<FeatureFlagMap>(() => {
    if (isRefInput(flags)) {
      return flags.value;
    }
    return flags;
  });

  const isEnabled = (featureKey: string) => computed(() => Boolean(source.value[featureKey]));

  return {
    isEnabled
  };
}
