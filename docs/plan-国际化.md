# Plan: 国际化（i18n）

## 1. 功能说明

国际化允许系统界面根据用户语言偏好显示对应语言的文本。本期实现中文（zh-CN）和英文（en-US）两种语言。

### 1.1 国际化范围

| 层次 | 内容 |
|---|---|
| 前端 UI | 菜单、按钮、标签、错误提示、表单校验信息 |
| 后端错误消息 | 业务异常、验证错误 |
| 后端响应内容 | API 错误码描述（可选，本期仅前端） |

### 1.2 本期实现范围

- 后端：`RequestLocalizationMiddleware` + 资源文件（`zh-CN.resx` / `en-US.resx`）
- 前端：`vue-i18n` + `locales/zh.ts` + `locales/en.ts` + 顶栏语言切换按钮

## 2. 等保 2.0 要求

国际化本身不影响等保合规，但用户操作界面的可读性提升有助于减少误操作风险。

## 3. 接口设计

后端通过 `Accept-Language` 请求头判断语言，前端切换语言时同步更新请求头。

```
Headers: Accept-Language: zh-CN  或  en-US
```

## 4. 后端实现步骤

1. 在 `Atlas.Application` 中添加 `Resources/` 目录
2. 创建 `Messages.zh-CN.resx`（中文消息）
3. 创建 `Messages.en-US.resx`（英文消息）
4. 在 `Program.cs` 注册 `RequestLocalizationMiddleware`，支持 `zh-CN` 和 `en-US`
5. 在业务异常中使用 `IStringLocalizer<Messages>` 获取本地化消息

## 5. 前端实现步骤

1. 安装 `vue-i18n` npm 包
2. 创建 `src/locales/zh.ts`（中文语言包）
3. 创建 `src/locales/en.ts`（英文语言包）
4. 在 `main.ts` 中初始化 `vue-i18n` 插件
5. 在 `MainLayout.vue` 顶栏添加语言切换按钮（中文/English）
6. 切换语言时更新 `axios` 请求头的 `Accept-Language`

## 6. 语言包结构示例

```typescript
// locales/zh.ts
export default {
  common: {
    save: "保存", cancel: "取消", delete: "删除", edit: "编辑",
    confirm: "确认", loading: "加载中...", search: "搜索"
  },
  auth: { login: "登录", logout: "退出", username: "用户名", password: "密码" },
  menu: { system: "系统管理", roles: "角色管理", users: "用户管理" }
}
```

## 7. 验收标准

- [ ] 后端注册 `RequestLocalizationMiddleware`，支持 `zh-CN` 和 `en-US`
- [ ] 前端安装 `vue-i18n` 并创建中英文语言包
- [ ] 顶栏有语言切换按钮，切换后界面文本变化
- [ ] 切换语言后请求头 `Accept-Language` 同步更新
- [ ] 默认语言为中文
