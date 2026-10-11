using FloraLens.Configuration;
using FloraLens.Models;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FloraLens.Services
{
    public class CustomVisionService : ICustomVisionService
    {
        private readonly AzureCustomVisionSettings _settings;
        private readonly CustomVisionPredictionClient? _client;

        public CustomVisionService(IOptions<AzureCustomVisionSettings> settings)
        {
            _settings = settings.Value;

            if (_settings.IsConfigured)
            {
                _client = new CustomVisionPredictionClient(new ApiKeyServiceClientCredentials(_settings.PredictionKey))
                {
                    Endpoint = _settings.Endpoint
                };
            }
        }

        public async Task<List<PredictionResult>> GetAllPredictionsAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("La imagen no existe", imagePath);

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            return await ClassifyImageAsync(imageBytes);
        }

        public async Task<PredictionResult> GetTopPredictionAsync(string imagePath)
        {
            var predictions = await GetAllPredictionsAsync(imagePath);
            return predictions.FirstOrDefault() ?? new PredictionResult();
        }

        private async Task<List<PredictionResult>> ClassifyImageAsync(byte[] imageBytes)
        {
            if (_client is null || !_settings.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Azure Custom Vision no está configurado. En Debug, crea appsettings.local.json a partir de appsettings.example.json y añade tus datos locales. No incluyas credenciales en el repositorio.");
            }

            using var stream = new MemoryStream(imageBytes);

            var result = await _client.ClassifyImageAsync(
                Guid.Parse(_settings.ProjectId),
                _settings.PublishedName,
                stream);

            return result.Predictions
                .OrderByDescending(p => p.Probability)
                .Take(3)
                .Select(p => new PredictionResult
                {
                    TagName = p.TagName,
                    Probability = p.Probability
                })
                .ToList();
        }
    }
}
