using AutoMapper;
using Atlas.Application.LowCode.Models;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Mappings;

/// <summary>
/// LowCode 领域 ↔ DTO 映射 Profile（M01）。
/// 所有 long 主键统一以 string 形式输出（前端 JSON number 64-bit 精度问题考虑）。
/// </summary>
public sealed class LowCodeMappingProfile : Profile
{
    public LowCodeMappingProfile()
    {
        CreateMap<AppThemeConfig, AppThemeConfigDto>().ReverseMap();

        CreateMap<AppDefinition, AppDefinitionListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam("Description", opt => opt.MapFrom(src => src.Description))
            .ForCtorParam("SchemaVersion", opt => opt.MapFrom(src => src.SchemaVersion))
            .ForCtorParam("TargetTypes", opt => opt.MapFrom(src => src.TargetTypes))
            .ForCtorParam("DefaultLocale", opt => opt.MapFrom(src => src.DefaultLocale))
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status))
            .ForCtorParam("CurrentVersionId", opt => opt.MapFrom(src => src.CurrentVersionId.HasValue && src.CurrentVersionId.Value > 0 ? src.CurrentVersionId : null))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt))
            .ForCtorParam("WorkspaceId", opt => opt.MapFrom(src => src.WorkspaceId))
            .ForCtorParam("FolderId", opt => opt.MapFrom(_ => (string?)null));

        CreateMap<AppDefinition, AppDefinitionDetail>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam("Description", opt => opt.MapFrom(src => src.Description))
            .ForCtorParam("SchemaVersion", opt => opt.MapFrom(src => src.SchemaVersion))
            .ForCtorParam("TargetTypes", opt => opt.MapFrom(src => src.TargetTypes))
            .ForCtorParam("DefaultLocale", opt => opt.MapFrom(src => src.DefaultLocale))
            .ForCtorParam("Theme", opt => opt.MapFrom(src => src.Theme))
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status))
            .ForCtorParam("CurrentVersionId", opt => opt.MapFrom(src => src.CurrentVersionId.HasValue && src.CurrentVersionId.Value > 0 ? src.CurrentVersionId.Value.ToString() : null))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt))
            .ForCtorParam("WorkspaceId", opt => opt.MapFrom(src => src.WorkspaceId));

        CreateMap<PageDefinition, PageDefinitionListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("AppId", opt => opt.MapFrom(src => src.AppId.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam("Path", opt => opt.MapFrom(src => src.Path))
            .ForCtorParam("TargetType", opt => opt.MapFrom(src => src.TargetType))
            .ForCtorParam("Layout", opt => opt.MapFrom(src => src.Layout))
            .ForCtorParam("OrderNo", opt => opt.MapFrom(src => src.OrderNo))
            .ForCtorParam("IsVisible", opt => opt.MapFrom(src => src.IsVisible))
            .ForCtorParam("IsLocked", opt => opt.MapFrom(src => src.IsLocked))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<PageDefinition, PageDefinitionDetail>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("AppId", opt => opt.MapFrom(src => src.AppId.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam("Path", opt => opt.MapFrom(src => src.Path))
            .ForCtorParam("TargetType", opt => opt.MapFrom(src => src.TargetType))
            .ForCtorParam("Layout", opt => opt.MapFrom(src => src.Layout))
            .ForCtorParam("OrderNo", opt => opt.MapFrom(src => src.OrderNo))
            .ForCtorParam("IsVisible", opt => opt.MapFrom(src => src.IsVisible))
            .ForCtorParam("IsLocked", opt => opt.MapFrom(src => src.IsLocked))
            .ForCtorParam("SchemaJson", opt => opt.MapFrom(src => src.SchemaJson))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<AppVariable, AppVariableDto>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("AppId", opt => opt.MapFrom(src => src.AppId.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("DisplayName", opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam("Scope", opt => opt.MapFrom(src => src.Scope))
            .ForCtorParam("ValueType", opt => opt.MapFrom(src => src.ValueType))
            .ForCtorParam("IsReadOnly", opt => opt.MapFrom(src => src.IsReadOnly))
            .ForCtorParam("IsPersisted", opt => opt.MapFrom(src => src.IsPersisted))
            .ForCtorParam("DefaultValueJson", opt => opt.MapFrom(src => src.DefaultValueJson))
            .ForCtorParam("ValidationJson", opt => opt.MapFrom(src => src.ValidationJson))
            .ForCtorParam("Description", opt => opt.MapFrom(src => src.Description))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<AppContentParam, AppContentParamDto>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("AppId", opt => opt.MapFrom(src => src.AppId.ToString()))
            .ForCtorParam("Code", opt => opt.MapFrom(src => src.Code))
            .ForCtorParam("Kind", opt => opt.MapFrom(src => src.Kind))
            .ForCtorParam("ConfigJson", opt => opt.MapFrom(src => src.ConfigJson))
            .ForCtorParam("Description", opt => opt.MapFrom(src => src.Description))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam("UpdatedAt", opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<AppVersionArchive, AppVersionArchiveListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("AppId", opt => opt.MapFrom(src => src.AppId.ToString()))
            .ForCtorParam("VersionLabel", opt => opt.MapFrom(src => src.VersionLabel))
            .ForCtorParam("Note", opt => opt.MapFrom(src => src.Note))
            .ForCtorParam("IsSystemSnapshot", opt => opt.MapFrom(src => src.IsSystemSnapshot))
            .ForCtorParam("CreatedByUserId", opt => opt.MapFrom(src => src.CreatedByUserId))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAt));
    }
}
