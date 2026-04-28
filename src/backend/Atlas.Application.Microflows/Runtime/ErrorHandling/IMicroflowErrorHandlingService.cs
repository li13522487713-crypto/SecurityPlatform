namespace Atlas.Application.Microflows.Runtime.ErrorHandling;

public interface IMicroflowErrorHandlingService
{
    MicroflowErrorHandlingResult Handle(MicroflowErrorHandlingContext context);

    MicroflowErrorHandlingResult CompleteHandler(MicroflowErrorHandlingContext context, string terminalStatus, string? terminalObjectId, string? terminalKind);

    MicroflowRuntimeErrorContext BuildRuntimeErrorContext(MicroflowErrorHandlingContext context);
}
