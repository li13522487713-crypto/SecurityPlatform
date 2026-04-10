import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { createI18n } from "vue-i18n";
import EditorToolbar from "./EditorToolbar.vue";

describe("EditorToolbar", () => {
  it("未保存状态应展示自动保存提示", async () => {
    const i18n = createI18n({
      legacy: false,
      locale: "zh-CN",
      messages: {
        "zh-CN": {
          workflow: {
            autosaveNotStarted: "尚未保存",
            autosaveAt: "已保存 {time}",
            editorUnsaved: "未保存改动",
            saveDraft: "保存草稿",
            publish: "发布",
            colLatestVersion: "最新版本",
            testRunToolbar: "测试运行",
            moreActions: "更多",
            exportCanvasJson: "导出",
            importCanvasJson: "导入",
            resetCanvas: "重置"
          }
        }
      }
    });

    const wrapper = mount(EditorToolbar, {
      props: {
        name: "demo",
        isDirty: false,
        saving: false,
        showTestPanel: false
      },
      global: {
        plugins: [i18n],
        stubs: {
          "a-button": { template: "<button @click='$emit(`click`)'><slot /></button>" },
          "a-input": { template: "<input />" },
          "a-tag": { template: "<span><slot /></span>" },
          "a-space": { template: "<div><slot /></div>" },
          "a-dropdown": { template: "<div><slot /><slot name='overlay' /></div>" },
          "a-menu": { template: "<div><slot /></div>" },
          "a-menu-item": { template: "<button @click='$emit(`click`)'><slot /></button>" },
          LeftOutlined: { template: "<i />" },
          PlayCircleOutlined: { template: "<i />" },
          DownOutlined: { template: "<i />" }
        }
      }
    });

    expect(wrapper.text()).toContain("尚未保存");
  });
});
