using System.Reflection;
using ERP.Application.Common.Behaviors;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

/// <summary>Registers the Application layer: MediatR handlers, FluentValidation validators, pipeline behaviors.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IAuthTokenIssuer, AuthTokenIssuer>();

        return services;
    }
}
