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
}
