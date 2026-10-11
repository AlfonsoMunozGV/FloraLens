using FloraLens.Models;

namespace FloraLens.Services
{
    public interface ICustomVisionService
    {
        Task<List<PredictionResult>> GetAllPredictionsAsync(string imagePath);
        Task<PredictionResult> GetTopPredictionAsync(string imagePath);
    }
}