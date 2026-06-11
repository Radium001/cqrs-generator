using System.Reflection;

namespace CqrsGenerator.Gui.Services.Updates;

public sealed class AppUpdateOptions
{
    public const string UpdateUrlMetadataKey = "CqrsGeneratorUpdateUrl";

    public string UpdateUrl { get; init; } = string.Empty;

    public bool IncludePrereleases { get; init; }

    public static AppUpdateOptions FromAssemblyMetadata()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var updateUrl = Environment.GetEnvironmentVariable("CQRS_GENERATOR_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(updateUrl))
        {
            metadata.TryGetValue(UpdateUrlMetadataKey, out updateUrl);
        }

        var prereleaseValue = Environment.GetEnvironmentVariable("CQRS_GENERATOR_UPDATE_PRERELEASES");
        var includePrereleases = bool.TryParse(prereleaseValue, out var parsedPrerelease) && parsedPrerelease;

        return new AppUpdateOptions
        {
            UpdateUrl = updateUrl?.Trim() ?? string.Empty,
            IncludePrereleases = includePrereleases
        };
    }
}
