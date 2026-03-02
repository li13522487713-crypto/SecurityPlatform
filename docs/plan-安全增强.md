# 安全增强：XSS 防护 + 数据脱敏

> 文档版本：v1.0 | 等保2.0 覆盖：身份鉴别、访问控制、数据保密性、安全审计

---

## 一、功能描述

### 1.1 XSS 防护（Cross-Site Scripting Protection）

**背景**：等保2.0 要求对用户输入进行过滤，防止恶意脚本注入，保护终端用户安全。

**目标**：
- 对所有 HTTP 请求的 QueryString、Header、Body（JSON 字符串字段）进行 XSS 净化
- 不阻断请求流程，采用"净化而非拒绝"策略（允许富文本场景）
- 支持白名单路径，富文本编辑器接口可跳过净化

**控制点**：
| 输入来源 | 处理策略 |
|----------|----------|
| Query String 参数 | 转义 `<>'"&` 特殊字符 |
| JSON Body 字符串字段 | 深度遍历，对每个 string 值净化 |
| 富文本白名单路径 | 跳过净化，仅做 Content-Length 限制 |

### 1.2 数据脱敏（Sensitive Data Masking）

**背景**：等保2.0 要求敏感个人信息在日志、API 响应中脱敏展示，保护用户隐私。

**目标**：
- 提供声明式脱敏标注（`[Sensitive]` Attribute）
- 支持手机号、邮箱、姓名、身份证、IP 地址等多种脱敏模式
- 响应序列化时自动应用脱敏
- 不影响数据库存储，仅作用于输出层

**脱敏规则**：
| 类型 | 原值示例 | 脱敏结果 |
|------|---------|---------|
| Phone | 13812345678 | 138\*\*\*\*5678 |
| Email | user@example.com | u\*\*\*@example.com |
| Name | 张三丰 | 张\*\*丰 (≥3字) / 张\* (2字) |
| IdCard | 110101199001011234 | 1101\*\*\*\*\*\*\*\*1234 |
| IpAddress | 192.168.1.100 | 192.168.\*.\* |
| Custom | 任意 | 前N位 + \*\*\* + 后M位 |

---

## 二、产品架构清单（实现追踪）

### Phase 2A — XSS 防护中间件

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Application | `Options/XssOptions.cs` | 白名单路径配置项 | ☐ |
| A2 | Infrastructure | `Middleware/XssProtectionMiddleware.cs` | Request Body 净化（JSON 字符串深度遍历）+ QueryString 净化 | ☐ |
| A3 | WebApi | `Program.cs` | 注册 `XssOptions`，`UseXssProtection()` 注入 Pipeline | ☐ |
| A4 | WebApi | `appsettings.json` | 添加 `XssOptions` 配置节（白名单路径列表） | ☐ |

### Phase 2B — 数据脱敏

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| B1 | Core | `Attributes/SensitiveAttribute.cs` | 定义枚举 `SensitiveMaskType` + `[Sensitive]` Attribute | ☐ |
| B2 | Core | `Utils/SensitiveMasker.cs` | 各脱敏规则的纯函数实现 | ☐ |
| B3 | Infrastructure | `Json/SensitiveMaskingConverter.cs` | 自定义 `JsonConverter`，读取 Attribute 并应用脱敏 | ☐ |
| B4 | WebApi | `Program.cs` | 在 `AddControllers().AddJsonOptions()` 中注册 Converter | ☐ |
| B5 | Application | `System/Models/LoginLogModels.cs` | 为 `IpAddress` 等字段添加 `[Sensitive]` | ☐ |

---

## 三、数据模型 / 配置结构

### XssOptions（appsettings.json）

```json
{
  "XssOptions": {
    "WhitelistPaths": [
      "/api/notifications",
      "/api/files/upload"
    ],
    "MaxBodySizeBytes": 1048576
  }
}
```

### XssOptions.cs（C#）

```csharp
public sealed class XssOptions
{
    public string[] WhitelistPaths { get; init; } = [];
    public long MaxBodySizeBytes { get; init; } = 1_048_576; // 1 MB
}
```

---

## 四、API 影响说明

XSS 防护对现有 API **无新增端点**，行为变化：
- 所有 `POST/PUT/PATCH` 请求 Body 中的字符串字段会被自动净化
- 净化后字段长度不变（转义后 `<` → `&lt;`，字节数增加但长度字符数不变）
- 白名单路径请求体原样传递

数据脱敏对现有 API **响应字段值**进行脱敏，无结构变化。

---

## 五、后端实现规格

### 5.1 XssProtectionMiddleware 处理流程

```
HTTP Request
    │
    ├─ Path 在白名单? ──是──► 直接 next()
    │
    ├─ 净化 QueryString（逐参数转义）
    │
    ├─ Content-Type 是 application/json?
    │       ├─ 是：读 Body → 深度遍历 JSON 树 → 净化所有 string 值 → 替换 Body Stream
    │       └─ 否：直接 next()
    │
    └─ next()
```

**净化函数（最小转义，保留 HTML 结构）**：

```csharp
private static string SanitizeString(string input) =>
    input
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#x27;");
```

**JSON Body 深度遍历**：读取 `JsonDocument`，对每个 `JsonValueKind.String` 节点应用净化，重新序列化为 `MemoryStream` 后替换 `HttpContext.Request.Body`。

### 5.2 SensitiveMaskingConverter 序列化流程

1. 对 Response DTO 属性反射读取 `[Sensitive(MaskType.Phone)]`
2. `JsonConverter.Write` 时，取出属性值，调用 `SensitiveMasker.Mask(value, maskType)` 后写入
3. 注册为全局 Converter，不影响其他字段

---

## 六、等保2.0 合规说明

| 等保条款 | 实现措施 |
|---------|---------|
| **8.1.3 输入验证** | XSS 中间件对所有输入字符串进行净化，防止脚本注入 |
| **8.1.4 数据完整性** | 净化后字段值语义不变，仅特殊字符转义 |
| **8.2.3 数据保密性** | 脱敏 Attribute 确保 API 响应不暴露完整敏感信息 |
| **8.3.2 安全审计** | 登录日志、操作日志中 IP 地址自动脱敏（前端展示） |
| **8.4.1 白名单控制** | XssOptions.WhitelistPaths 支持细粒度豁免 |

---

## 七、验收标准

### XSS 防护

- [ ] POST `/api/auth/token` with body `{"username": "<script>alert(1)</script>"}` → Body 到达 Controller 时已净化为 `&lt;script&gt;alert(1)&lt;/script&gt;`
- [ ] GET `/api/dict-types?name=<img onerror=x>` → Controller 接收到的值已净化
- [ ] POST `/api/notifications`（白名单）带 HTML 内容 → Body 原样传递，不净化
- [ ] 净化后登录流程正常，非脚本字段值不变

### 数据脱敏

- [ ] `GET /api/login-logs` 响应中 `ipAddress` 字段展示为脱敏格式（如 `192.168.*.*`）
- [ ] 数据库中 `ip_address` 字段存储完整 IP
- [ ] 脱敏规则覆盖 Phone/Email/IdCard/IpAddress 四种模式单元测试通过
- [ ] 无 `[Sensitive]` 标注的字段不受影响
