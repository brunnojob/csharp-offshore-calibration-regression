using System.Globalization;
using System.Text.Json;

static class Program
{
    static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    static double Numeric(string value)
    {
        double result = double.Parse(value, Culture);
        Require(double.IsFinite(result), "Finite numeric input required");
        return result;
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    static List<string[]> Input(int columns)
    {
        var rows = new List<string[]>();
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            Require(rows.Count < 100000, "Input capacity exceeded");
            string[] fields = line.Split(',');
            Require(fields.Length == columns && fields.All(f => !string.IsNullOrWhiteSpace(f)), "Invalid CSV row");
            rows.Add(fields);
        }
        Require(rows.Count > 0, "Missing input");
        return rows;
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--self-test" })) { Test(); Console.WriteLine("Tests passed"); }
            else Run();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
    }

    record Point(double Reference, double Measured, double Sigma);
    record Fit(double Slope, double Intercept, double Rms, double SlopeSigma, double MaxResidual);
    static Fit Calibrate(List<Point> points)
    {
        Require(points.Count >= 3, "At least three points required");
        foreach (var p in points) Require(p.Sigma > 0, "Positive uncertainty required");
        double sum = points.Sum(p => 1 / (p.Sigma * p.Sigma));
        double mx = points.Sum(p => p.Reference / (p.Sigma * p.Sigma)) / sum;
        double my = points.Sum(p => p.Measured / (p.Sigma * p.Sigma)) / sum;
        double xx = points.Sum(p => Math.Pow(p.Reference - mx, 2) / Math.Pow(p.Sigma, 2));
        Require(xx > 1e-12, "Reference range is degenerate");
        double xy = points.Sum(p => (p.Reference - mx) * (p.Measured - my) / Math.Pow(p.Sigma, 2));
        double slope = xy / xx, intercept = my - slope * mx;
        double[] residuals = points.Select(p => p.Measured - slope * p.Reference - intercept).ToArray();
        return new Fit(slope, intercept, Math.Sqrt(residuals.Sum(r => r * r) / points.Count), Math.Sqrt(1 / xx), residuals.Max(r => Math.Abs(r)));
    }
    static void Test()
    {
        var fit = Calibrate(new List<Point> { new(0, 1, 0.1), new(1, 3, 0.1), new(2, 5, 0.1) });
        Require(Math.Abs(fit.Slope - 2) < 1e-10 && Math.Abs(fit.Intercept - 1) < 1e-10 && fit.Rms < 1e-10, "Exact fit mismatch");
        bool rejected = false;
        try { Calibrate(new List<Point> { new(1, 1, 1), new(1, 2, 1), new(1, 3, 1) }); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Degenerate references accepted");
    }
    static void Run()
    {
        var points = Input(3).Select(r => new Point(Numeric(r[0]), Numeric(r[1]), Numeric(r[2]))).ToList();
        var fit = Calibrate(points); Require(Math.Abs(fit.Slope) > 1e-12, "Calibration slope cannot be inverted"); Console.WriteLine(JsonSerializer.Serialize(fit));
        foreach (var p in points) Console.WriteLine(FormattableString.Invariant($"reference={p.Reference:F4},corrected={(p.Measured - fit.Intercept) / fit.Slope:F4}"));
    }

}
