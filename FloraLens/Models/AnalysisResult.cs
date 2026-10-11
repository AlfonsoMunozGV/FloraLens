namespace FloraLens.Models;

public class AnalysisResult
{
    public string UserPrediction { get; set; } = string.Empty;
    public string AIPrediction { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime AnalysisTime { get; set; } = DateTime.Now;
    public List<PredictionResult> AllPredictions { get; set; } = new();

    public bool IsMatch =>
        !string.IsNullOrEmpty(UserPrediction) &&
        !string.IsNullOrEmpty(AIPrediction) &&
        UserPrediction.Trim().Equals(AIPrediction.Trim(), StringComparison.OrdinalIgnoreCase);

    public string ResultMessage
    {
        get
        {
            if (string.IsNullOrEmpty(UserPrediction) || string.IsNullOrEmpty(AIPrediction))
                return "Esperando análisis...";

            if (IsMatch)
                return $"¡Acertaste! Es un {AIPrediction}";
            else
                return $"No coincide: tú dijiste '{UserPrediction}' y Azure dice '{AIPrediction}' con {Confidence:P0}";
        }
    }
}