namespace Atlas.Application.AiPlatform.Abstractions;

public interface IEvaluationJobService
{
    Task ExecuteTaskAsync(long taskId);
}
