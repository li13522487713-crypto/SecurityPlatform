import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import StartNodeForm from "./StartNodeForm.vue";

describe("StartNodeForm", () => {
  it("新增变量时应更新配置并触发变更事件", async () => {
    const configs: Record<string, unknown> = {};
    const wrapper = mount(StartNodeForm, {
      props: { configs },
      global: {
        stubs: {
          "a-form": { template: "<form><slot /></form>" },
          "a-form-item": { template: "<div><slot /></div>" },
          "a-input": { template: "<input />" },
          "a-select": { template: "<select><slot /></select>" },
          "a-select-option": { template: "<option><slot /></option>" },
          "a-button": { template: "<button @click='$emit(`click`)'><slot /></button>" },
          "a-switch": { template: "<input type='checkbox' />" }
        }
      }
    });

    await wrapper.find("button").trigger("click");

    expect(Array.isArray(configs.variables)).toBe(true);
    expect((configs.variables as unknown[]).length).toBeGreaterThan(0);
    expect(wrapper.emitted("change")).toBeTruthy();
  });
});
