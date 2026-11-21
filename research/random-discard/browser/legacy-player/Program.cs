using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length != 5) {
    throw new ArgumentException("Supply the fixture JSON, host DLL, baseline engine DLL, fixture name and output directory.");
}

string hostPath = Path.GetFullPath(args[1]);
Assembly engine = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
Console.WriteLine($"Reference engine: {engine.Location}");
AssemblyLoadContext.Default.Resolving += (_, dependency) => {
    string file = Path.Combine(Path.GetDirectoryName(hostPath)!, dependency.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
};
Assembly host = AssemblyLoadContext.Default.LoadFromAssemblyPath(hostPath);
MethodInfo execute = host.GetType("YGOProbabilityCalculatorBlazor.Services.BackgroundCalculation.CalculationWire", true)!
    .GetMethod("ExecuteInteractiveExact", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException("Calculator host does not expose ExecuteInteractiveExact.");

string name = args[3];
JsonNode fixture = JsonNode.Parse(File.ReadAllText(args[0])).AsArray().Single(entry => (string)entry["name"] == name);
JsonNode input = fixture["input"];
input["Options"]["Mode"] = 2;
string response = (string)execute.Invoke(null, [input.ToJsonString()]);
Assembly loadedEngine = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "CardProbability");
if (loadedEngine != engine) {
    throw new InvalidOperationException("The host used a different engine assembly.");
}
JsonNode wire = JsonNode.Parse(response);
JsonNode contract = wire["Result"]?["Contract"];
if (wire["Error"] is not null || contract is null || (int)contract["ExecutedMode"] != 2
    || (int)contract["Accuracy"] != 0 || (int)contract["Completion"] != 0) {
    throw new InvalidOperationException(response);
}

JsonNode fractions = contract["Fractions"];
string Fraction(JsonNode row) => $"{row["Numerator"]}/{row["Denominator"]}";
string[] rows = [Fraction(fractions["Total"]), .. fractions["Combos"].AsArray().Select(Fraction),
    .. fractions["Groups"].AsArray().Select(Fraction)];
if (rows.Length != 1 + input["Combos"].AsArray().Count + input["Groups"].AsArray().Count) {
    throw new InvalidOperationException("Unexpected row count.");
}
if (fixture["reference"] is JsonArray stored && !rows.SequenceEqual(stored.Select(value => (string)value))) {
    throw new InvalidOperationException("Reference mismatch.");
}

string directory = Directory.CreateDirectory(args[4]).FullName;
string destination = Path.Combine(directory, name + ".json");
string json = JsonSerializer.Serialize(new { Name = name, Rows = rows }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(destination + ".pending", json + Environment.NewLine);
File.Move(destination + ".pending", destination, true);
Console.WriteLine(destination);
