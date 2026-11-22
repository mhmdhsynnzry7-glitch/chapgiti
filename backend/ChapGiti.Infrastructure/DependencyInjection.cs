using ChapGiti.Domain.Printing;
using ChapGiti.Infrastructure.Content;
using ChapGiti.Infrastructure.Messaging;
using ChapGiti.Infrastructure.Persistence;
using ChapGiti.Infrastructure.Persistence.Repositories;
using ChapGiti.Infrastructure.Security;
using ChapGiti.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ChapGiti.Application.Realtime;
using ChapGiti.Infrastructure.Realtime;
using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
namespace ChapGiti.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => OtpVerificationConfiguration.Read(
            provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            provider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()));
        services.AddSingleton<MelipayamakOtpOptions>();
        services.AddSingleton<IOtpCodeProtector, OtpCodeProtector>();
        services.AddHttpClient<IOtpSender, MelipayamakOtpSender>()
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddDbContext<ChapGitiDbContext>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>().GetConnectionString("SqlServer"), sql => sql.UseCompatibilityLevel(150)).AddInterceptors(EfConnectionOptionsInterceptor.Instance));
        services.AddScoped<SqlStore>();
        services.TryAddSingleton<IRealtimeDispatcher, NoRealtimeDispatcher>();
        services.AddScoped<CommittedUpdates>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<SqlStore>());
        services.AddScoped<IDatabaseMaintenance, DatabaseMaintenance>();
        services.AddSingleton<IContentDefaults, ContentDefaults>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPrivateFileStore, PrivateFileStore>();
        services.AddScoped<ISmsNotifier, SmsService>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ChapGiti.Application.Orders.IOrderInboxRepository, OrderInboxRepository>();
        services.AddScoped<IOrderQuoteRepository, OrderQuoteRepository>();
        services.AddScoped<IOrderCorrectionRepository, OrderCorrectionRepository>();
        services.AddScoped<IOrderViewRepository, OrderViewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAdminOrderRepository, AdminOrderRepository>();
        services.AddScoped<IAdminContentRepository, AdminContentRepository>();
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<IPrintCatalogRepository, PrintCatalogRepository>();
        services.AddScoped<ICutTariffRepository, CutTariffRepository>();
        services.AddScoped<IMagazineRepository, MagazineRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IAccessSessionRepository, AccessSessionRepository>();
        services.AddScoped<IUserManagementRepository, UserManagementRepository>();
        services.AddScoped<ICustomerActivityRepository, CustomerActivityRepository>();
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var value = configuration["Otp:LifetimeSeconds"] ?? "120";
            if (!int.TryParse(value, out var seconds) || seconds is < 60 or > 600)
                throw new InvalidOperationException("Otp:LifetimeSeconds must be between 60 and 600.");
            return new ChapGiti.Application.Authentication.AuthenticationPolicy(seconds);
        });
        return services;
    }
}
