using System;
using System.Collections.Generic;
using System.Text;

namespace FloraLens.Models
{
    public class PredictionResult
    {
        public string TagName { get; set; } = "";
        public double Probability { get; set; }
        public string Display => $"{TagName} ({Probability * 100:F1}%";
    }
}
