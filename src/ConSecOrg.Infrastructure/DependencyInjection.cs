using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Infrastructure.Crypto;
using ConSecOrg.Infrastructure.Device;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Infrastructure.Persistence.Repositories;
using ConSecOrg.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConSecOrg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useLocalDb = false)
    {
        var connectionString = useLocalDb
            ? configuration.GetConnectionString("LocalConnection")
              ?? "Server=(localdb)\\MSSQLLocalDB;Database=ConSecOrg_Personal;Trusted_Connection=True;"
            : configuration.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("DefaultConnection string not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Crypto services
        services.AddSingleton<ICryptoService, GostCryptoService>();
        services.AddSingleton<IKdfService, KdfService>();
        services.AddSingleton<IHashChainService, HashChainService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Device service (Windows only)
        services.AddSingleton<IDeviceService, WindowsDeviceService>();

        // Session key store (singleton — keys persist for the lifetime of the app process)
        services.AddSingleton<IEncryptionKeyStore, EncryptionKeyStore>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
