namespace ChapGiti.Application.Abstractions;

public interface IDatabaseMaintenance
{
    Task Migrate();
    Task Import(string snapshot);
    Task VerifyAdministrator();
    Task VerifyStartup();
    Task Health();
}
