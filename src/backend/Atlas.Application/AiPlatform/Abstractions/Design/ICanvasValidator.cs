using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// V2 工作流画布校验器。
/// </summary>
public interface ICanvasValidator
{
    CanvasValidationResult ValidateCanvas(string canvasJson);
}

