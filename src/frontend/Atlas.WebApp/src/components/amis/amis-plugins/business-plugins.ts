import { registerAmisPlugin } from "./index";

export function registerBusinessPlugins(): void {
  registerAmisPlugin({
    name: "atlas-dynamic-table",
    displayName: "DynamicTable",
    group: "business",
    icon: "fa fa-table",
    description: "Dynamic table component backed by DynamicTable API",
    schema: {
      type: "crud",
      api: "/api/v1/amis/dynamic-tables/${tableKey}/crud",
      columns: [
        { name: "id", label: "ID", type: "text" },
      ],
    },
    editorConfig: {
      tags: ["business"],
      scaffold: {
        type: "crud",
        api: "/api/v1/amis/dynamic-tables/demo/crud",
        columns: [{ name: "id", label: "ID" }],
      },
      previewSchema: {
        type: "crud",
        className: "text-left",
        columns: [
          { name: "id", label: "ID", type: "text" },
          { name: "name", label: "Name", type: "text" },
        ],
        data: {
          items: [
            { id: 1, name: "Sample Record" },
            { id: 2, name: "Another Record" },
          ],
        },
      },
    },
  });

  registerAmisPlugin({
    name: "atlas-status-tag",
    displayName: "StatusTag",
    group: "business",
    icon: "fa fa-tag",
    description: "Colored status tag with configurable value-color mapping",
    schema: {
      type: "tag",
      label: "${status}",
      color: "processing",
    },
    editorConfig: {
      tags: ["business"],
      scaffold: {
        type: "mapping",
        name: "status",
        map: {
          Active: "<span class='label label-success'>Active</span>",
          Inactive: "<span class='label label-default'>Inactive</span>",
          Pending: "<span class='label label-warning'>Pending</span>",
        },
      },
      previewSchema: {
        type: "tag",
        label: "Active",
        color: "processing",
      },
    },
  });

  registerAmisPlugin({
    name: "atlas-user-role-picker",
    displayName: "UserRolePicker",
    group: "business",
    icon: "fa fa-users",
    description: "Role-aware user picker with remote search",
    schema: {
      type: "select",
      name: "userId",
      label: "User",
      searchable: true,
      source: "/api/v1/users?keyword=${term}&pageSize=20",
      labelField: "displayName",
      valueField: "id",
    },
    editorConfig: {
      tags: ["business"],
      scaffold: {
        type: "select",
        name: "userId",
        label: "User",
        searchable: true,
        source: "/api/v1/users?keyword=${term}&pageSize=20",
        labelField: "displayName",
        valueField: "id",
      },
      previewSchema: {
        type: "select",
        name: "userId",
        label: "User",
        options: [
          { label: "Admin", value: "1" },
          { label: "User", value: "2" },
        ],
      },
    },
  });

  registerAmisPlugin({
    name: "atlas-file-uploader",
    displayName: "FileUploader",
    group: "business",
    icon: "fa fa-upload",
    description: "Atlas 文件上传组件（支持分片接口映射）",
    schema: {
      type: "input-file",
      name: "fileId",
      label: "附件",
      receiver: "/api/v1/files",
      startChunkApi: "/api/v1/files/upload/init",
      chunkApi: "/api/v1/files/upload/${sessionId}/part/${partNumber}",
      finishChunkApi: "/api/v1/files/upload/${sessionId}/complete"
    },
    editorConfig: {
      tags: ["business"],
      scaffold: {
        type: "input-file",
        name: "fileId",
        label: "附件",
        receiver: "/api/v1/files",
        startChunkApi: "/api/v1/files/upload/init",
        chunkApi: "/api/v1/files/upload/${sessionId}/part/${partNumber}",
        finishChunkApi: "/api/v1/files/upload/${sessionId}/complete"
      },
      previewSchema: {
        type: "input-file",
        name: "fileId",
        label: "附件",
        value: ""
      }
    }
  });

  registerAmisPlugin({
    name: "atlas-approval-start-button",
    displayName: "发起审批按钮",
    group: "business",
    icon: "fa fa-paper-plane",
    description: "点击后将表单数据作为 formData 启动选定的审批流",
    schema: {
      type: "button",
      label: "发起审批",
      level: "primary",
      actionType: "ajax",
      api: {
        method: "post",
        url: "/api/v1/approval/instances",
        data: {
          definitionId: "${approvalDefinitionId}",
          title: "新审批申请 - ${DATETIME(NOW(), 'YYYY-MM-DD')}",
          businessKey: "",
          formData: "$$"
        }
      }
    },
    editorConfig: {
      tags: ["business"],
      scaffold: {
        type: "button",
        label: "发起审批",
        level: "primary",
        actionType: "ajax",
        api: {
          method: "post",
          url: "/api/v1/approval/instances",
          data: {
            definitionId: "",
            title: "新审批申请 - ${DATETIME(NOW(), 'YYYY-MM-DD')}",
            businessKey: "",
            formData: "$$"
          }
        }
      },
      previewSchema: {
        type: "button",
        label: "发起审批",
        level: "primary"
      }
    }
  });
}
