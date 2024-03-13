public class ProcessBenchmarkResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<YieldCurveDataSet> YieldCurveCalculations { get; set; } = new List<YieldCurveDataSet>();
    HashSet<double> tenuresThatRequireInterPolation = new HashSet<double>();
    HashSet<double> tenuresThatDoNotRequireInterpolation = new HashSet<double>();

}
