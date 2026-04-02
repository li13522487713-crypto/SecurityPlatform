using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Validators;
using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions.Functions;
using Atlas.Infrastructure.LogicFlow.Expressions.Repositories;
using Atlas.Infrastructure.LogicFlow.Expressions.Rules;
using Atlas.Infrastructure.LogicFlow.Expressions.Services;
using Atlas.Infrastructure.LogicFlow.Flows;
using Atlas.Infrastructure.LogicFlow.Repositories;
using Atlas.Infrastructure.LogicFlow.Seeds;
using Atlas.Infrastructure.LogicFlow.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.LogicFlow;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogicFlowInfrastructure(this IServiceCollection services)
    {
        // ── Expression engine core ──
        services.AddSingleton<IAstCache>(new AstCompilationCache(4096));
        services.AddSingleton<IFunctionRegistry, BuiltinFunctionRegistry>();
        services.AddSingleton<ITypeInferencer, ExprTypeInferencer>();
        services.AddSingleton<ExprEvaluator>();

        // Decision table & Rule chain executors
        services.AddSingleton<DecisionTableExecutor>();
        services.AddSingleton<IDecisionTableExecutor>(sp => sp.GetRequiredService<DecisionTableExecutor>());
        services.AddSingleton<RuleChainExecutor>();
        services.AddSingleton<IRuleChainExecutor>(sp => sp.GetRequiredService<RuleChainExecutor>());

        // Expression repositories
        services.AddScoped<IFunctionDefinitionRepository, FunctionDefinitionRepository>();
        services.AddScoped<IDecisionTableRepository, DecisionTableRepository>();
        services.AddScoped<IRuleChainRepository, RuleChainRepository>();

        // Expression query services
        services.AddScoped<IFunctionDefinitionQueryService, FunctionDefinitionQueryService>();
        services.AddScoped<IDecisionTableQueryService, DecisionTableQueryService>();
        services.AddScoped<IRuleChainQueryService, RuleChainQueryService>();

        // Expression command services
        services.AddScoped<IFunctionDefinitionCommandService, FunctionDefinitionCommandService>();
        services.AddScoped<IDecisionTableCommandService, DecisionTableCommandService>();
        services.AddScoped<IRuleChainCommandService, RuleChainCommandService>();

        // Expression validators
        services.AddScoped<IValidator<FunctionDefinitionCreateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.FunctionDefinitionCreateRequestValidator>();
        services.AddScoped<IValidator<FunctionDefinitionUpdateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.FunctionDefinitionUpdateRequestValidator>();
        services.AddScoped<IValidator<DecisionTableCreateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.DecisionTableCreateRequestValidator>();
        services.AddScoped<IValidator<DecisionTableUpdateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.DecisionTableUpdateRequestValidator>();
        services.AddScoped<IValidator<RuleChainCreateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.RuleChainCreateRequestValidator>();
        services.AddScoped<IValidator<RuleChainUpdateRequest>,
            Atlas.Application.LogicFlow.Expressions.Validators.RuleChainUpdateRequestValidator>();

        // ── Node type meta-model ──

        // Registry (singleton – holds built-in node declarations in memory)
        services.AddSingleton<INodeTypeRegistry>(sp =>
        {
            var registry = new NodeTypeRegistry();
            foreach (var decl in BuiltInNodeSeeds.All)
            {
                registry.Register(decl);
            }
            return registry;
        });

        // Repositories
        services.AddScoped<INodeTypeRepository, NodeTypeRepository>();
        services.AddScoped<INodeTemplateRepository, NodeTemplateRepository>();

        // Query services
        services.AddScoped<INodeTypeQueryService, NodeTypeQueryService>();
        services.AddScoped<INodeTemplateQueryService, NodeTemplateQueryService>();

        // Command services
        services.AddScoped<INodeTypeCommandService, NodeTypeCommandService>();
        services.AddScoped<INodeTemplateCommandService, NodeTemplateCommandService>();

        // Node validators
        services.AddScoped<IValidator<NodeTypeCreateRequest>,
            Atlas.Application.LogicFlow.Nodes.Validators.NodeTypeCreateRequestValidator>();
        services.AddScoped<IValidator<NodeTemplateCreateRequest>,
            Atlas.Application.LogicFlow.Nodes.Validators.NodeTemplateCreateRequestValidator>();

        services.AddScoped<IValidator<LogicFlowCreateRequest>, LogicFlowCreateRequestValidator>();
        services.AddScoped<IValidator<LogicFlowUpdateRequest>, LogicFlowUpdateRequestValidator>();

        services.AddScoped<ILogicFlowRepository, LogicFlowRepository>();
        services.AddScoped<IFlowNodeBindingRepository, FlowNodeBindingRepository>();
        services.AddScoped<IFlowEdgeRepository, FlowEdgeRepository>();
        services.AddScoped<ILogicFlowQueryService, LogicFlowQueryService>();
        services.AddScoped<ILogicFlowCommandService, LogicFlowCommandService>();
        services.AddScoped<IFlowValidator, FlowValidator>();
        services.AddScoped<IFlowCompiler, FlowCompiler>();
        services.AddSingleton<IDagScheduler, DagScheduler>();

        services.AddScoped<IFlowExecutionRepository, FlowExecutionRepository>();
        services.AddScoped<INodeRunRepository, NodeRunRepository>();
        services.AddScoped<IExecutionStateService, ExecutionStateService>();
        services.AddScoped<IFlowExecutionQueryService, FlowExecutionQueryService>();
        services.AddScoped<IFlowExecutionCommandService, FlowExecutionCommandService>();
        services.AddSingleton<INodeExecutor, NodeExecutorDispatcher>();

        return services;
    }
}
