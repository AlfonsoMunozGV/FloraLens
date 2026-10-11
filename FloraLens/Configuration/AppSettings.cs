namespace FloraLens.Configuration;

public class AzureCustomVisionSettings
{
    public string PredictionKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PublishedName { get; set; } = string.Empty;

    public bool IsConfigured =>
        IsProvided(PredictionKey) &&
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps &&
        Guid.TryParse(ProjectId, out _) &&
        IsProvided(PublishedName);

    private static bool IsProvided(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase);
}

public class SpeechRecognitionSettings
{
    public string DefaultCulture { get; set; } = "es-ES";
    public bool EnablePartialResults { get; set; } = true;
}

public class AppSettings
{
    public AzureCustomVisionSettings AzureCustomVision { get; set; } = new();
    public SpeechRecognitionSettings SpeechRecognition { get; set; } = new();
}
