namespace ChapGiti.AuthenticationTests;

// The magazine is restored. These replace the obsolete fake-guide/retirement assertions.
internal static class ContentAuditScenarios
{
    public static Task Run(Action<bool,string> check) => CatalogMagazineScenarios.Run(check);
}
