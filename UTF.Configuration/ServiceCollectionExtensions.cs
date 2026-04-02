using Microsoft.Extensions.DependencyInjection;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;
using UTF.Configuration.Validators;

namespace UTF.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUtfConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationSerializer, JsonConfigurationSerializer>();
        services.AddSingleton<IConfigurationValidator<SystemConfig>, SystemConfigValidator>();
        services.AddSingleton<IConfigurationValidator<DUTConfig>, DUTConfigValidator>();
        services.AddSingleton<IConfigurationValidator<TestConfig>, TestConfigValidator>();
        services.AddSingleton<CompositeConfigurationValidator>();
        return services;
    }
}
