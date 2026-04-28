/**
 * 统一元数据查询入口：请从此文件引用查找函数，避免在业务中重复实现检索逻辑。
 * 类型定义见 {@link "./metadata-catalog"}。
 */
export {
  createMetadataCatalog,
  getEntityByQualifiedName,
  findEntity,
  resolveStoredEntityQualifiedName,
  getEntityAttributes,
  getAttributeByQualifiedName,
  getAssociationsForEntity,
  getAssociationByQualifiedName,
  findAssociation,
  getTargetEntityByAssociation,
  getEnumerationByQualifiedName,
  findEnumeration,
  getEnumerationValues,
  getEnumerationValueKeys,
  getMicroflowById,
  getMicroflowByQualifiedName,
  getPageById,
  getWorkflowById,
  getSpecializations,
  isEntitySpecializationOf,
  searchEntities,
  searchAttributes,
  searchAssociations,
  searchEnumerations,
  searchMicroflows,
  searchPages,
  searchWorkflows,
} from "./metadata-catalog";

export type { MicroflowMetadataCatalog } from "./metadata-catalog";
