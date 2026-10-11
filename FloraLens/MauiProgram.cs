using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CommunityToolkit.Maui;
using FloraLens.Configuration;
using FloraLens.Services;
using System.Diagnostics;
using System.Reflection;

namespace FloraLens
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Load configuration from embedded appsettings.json
            var assembly = typeof(MauiProgram).Assembly;
            
#if DEBUG
            // Debug: List all embedded resources
            var resources = assembly.GetManifestResourceNames();
            Debug.WriteLine("=== Embedded Resources ===");
            foreach (var res in resources)
            {
                Debug.WriteLine($"  {res}");
            }
            Debug.WriteLine("==========================");
#endif

            using var stream = assembly.GetManifestResourceStream("FloraLens.appsettings.json");
            if (stream != null)
            {
                builder.Configuration.AddJsonStream(stream);
                Debug.WriteLine("appsettings.json loaded successfully from embedded resources");
            }
            else
            {
                Debug.WriteLine("WARNING: appsettings.json NOT found in embedded resources!");
                Debug.WriteLine($"Assembly: {assembly.FullName}");
                Debug.WriteLine($"Looking for: FloraLens.appsettings.json");
            }

#if DEBUG
            // Local overrides may contain developer credentials. They are ignored by Git
            // and are embedded only in Debug builds for local development.
            using var localStream = assembly.GetManifestResourceStream("FloraLens.appsettings.local.json");
            if (localStream != null)
            {
                builder.Configuration.AddJsonStream(localStream);
                Debug.WriteLine("Local development settings loaded");
            }
#endif

            // Bind configuration to strongly-typed settings
            builder.Services.AddOptions<AppSettings>().Configure<IConfiguration>((settings, config) => config.Bind(settings));
            builder.Services.AddOptions<AzureCustomVisionSettings>().Configure<IConfiguration>((settings, config) => config.GetSection("AzureCustomVision").Bind(settings));
            builder.Services.AddOptions<SpeechRecognitionSettings>().Configure<IConfiguration>((settings, config) => config.GetSection("SpeechRecognition").Bind(settings));

            // Register services
            builder.Services.AddSingleton<ICustomVisionService, CustomVisionService>();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
