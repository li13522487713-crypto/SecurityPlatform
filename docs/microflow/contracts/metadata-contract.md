# MetadataCatalog 契约

## 权威类型

`@atlas/microflow/metadata`：`MicroflowMetadataCatalog` 及 `MetadataEntity`、`MetadataAttribute`、`MetadataAssociation`、`MetadataEnumeration*`、`MetadataMicroflowRef`、`MetadataPageRef`、`MetadataWorkflowRef`、`MetadataConnector` 等。

## 查询 API（与纯函数对齐）

实现适配器与 `metadata-catalog.ts` 中下列函数语义一致即可：

- `getEntityByQualifiedName` / `getEntityAttributes`
- `getAssociationByQualifiedName` / `getAssociationsForEntity`
- `getEnumerationByQualifiedName` / `getEnumerationValues`
- `getMicroflowById`、`getPageById`、`getWorkflowById`
- `getSpecializations`、`isEntitySpecializationOf`

## 前端依赖

- 变量索引、表达式与元数据校验依赖 **Mock 或真实 Catalog**；禁止在表单中硬编码实体列表作为长期方案。

## 后端建议

`GET /api/microflow-metadata` 返回与 `MicroflowMetadataCatalog` 同构 JSON（或分页/按模块拆分，但需可还原为 Catalog）。
