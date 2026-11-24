using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using ChapGiti.Api.Realtime;
using ChapGiti.Application.Realtime;

namespace ChapGiti.Api.Http;

public static class ApiRegistration
{
    public static IServiceCollection AddChapGitiApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSignalR(options => { options.EnableDetailedErrors = false; options.MaximumReceiveMessageSize = 8192; });
        services.AddSingleton<RealtimeAccess>();
        services.AddSingleton<RealtimeConnections>();
        services.AddSingleton<IRealtimeDispatcher, SignalRDispatcher>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, RealtimeSessionHandler>("ChapRealtime", _ => { });
        services.AddAuthorization(options => options.AddPolicy("ChapRealtime", policy =>
            policy.AddAuthenticationSchemes("ChapRealtime").RequireAuthenticatedUser().RequireClaim("chap:scope")));
        services.AddScoped<IRequestSession, HttpSessionContext>();
        services.AddScoped<SessionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CustomerOtpService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<CustomerActivityService>();
        services.AddScoped<AdministratorService>();
        services.AddScoped<ContentService>();
        services.AddScoped<PrintCatalogService>();
        services.AddScoped<CutTariffService>();
        services.AddScoped<MagazineService>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderQuoteService>();
        services.AddScoped<OrderCorrectionService>();
        services.AddScoped<OrderSubmissionPolicy>();
        services.AddScoped<OrderPdfFiles>();
        services.AddScoped<OrderProductionReadiness>();
        services.AddScoped<OrderViewService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<TicketService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<AdminContentService>();
        services.AddScoped<ServiceContentService>();
        services.AddScoped<PublicAssetService>();
        services.AddControllers(options => options.Filters.Add<ApiSecurityFilter>())
            .AddApplicationPart(typeof(ApiRegistration).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = c =>
                new BadRequestObjectResult(new { error = "ساختار یا فیلدهای درخواست معتبر نیست.", code = "VALIDATION_INVALID", requestId = c.HttpContext.TraceIdentifier });
        });
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    public static void MapChapGitiApi(this WebApplication app)
    {
        // Resolve eagerly: unsafe/non-boolean settings fail startup, never an auth request.
        var otp = app.Services.GetRequiredService<ChapGiti.Application.Authentication.OtpVerificationPolicy>();
        if (!otp.Enabled)
            app.Logger.LogWarning("Local Development OTP verification bypass is enabled; real SMS sending is disabled.");
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseWebSockets();
        app.UseMiddleware<RealtimeSessionMiddleware>();
        // Explicit ordering prevents minimal hosting from authenticating/caching a
        // NoResult before the existing session has been adapted for this request.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            await response.WriteAsJsonAsync(new { code = ExceptionMiddleware.DefaultCode(response.StatusCode), requestId = context.HttpContext.TraceIdentifier, error = response.StatusCode switch
            {
                404 => "مسیر پیدا نشد.", 405 => "این عملیات در این مسیر مجاز نیست.",
                413 => "حجم درخواست بیش از حد مجاز است.", 415 => "نوع محتوای درخواست معتبر نیست.",
                _ => "درخواست انجام نشد."
            }});
        });
        app.MapGet("/health", async (IDatabaseMaintenance maintenance) =>
        {
            await maintenance.Health();
            return Results.Json(new { ok = true, backend = "ASP.NET Core", database = "SQL Server" });
        });
        app.MapControllers();
        foreach (var scope in new[] { "customer", "admin" })
        {
            app.MapHub<NotificationsHub>("/api/realtime/" + scope + "/notifications").RequireAuthorization("ChapRealtime");
            app.MapHub<SupportHub>("/api/realtime/" + scope + "/support").RequireAuthorization("ChapRealtime");
        }
    }
}
