using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

if (args.Length != 2 || !int.TryParse(args[0], out int handSize) || handSize is not (5 or 6)) {
    throw new ArgumentException("Supply a hand size of 5 or 6 and the JSON exported by browser/fixtures.cjs.");
}

string name = $"thunder-dragon-{handSize}";
using JsonDocument exported = JsonDocument.Parse(File.ReadAllText(args[1]));
JsonElement input = exported.RootElement.EnumerateArray().Single(row => row.GetProperty("name").GetString() == name).GetProperty("input");
ValidateModel(input, handSize);
(string union, string single) = new ThunderReplay(handSize).Calculate();
Console.WriteLine(JsonSerializer.Serialize(new {
    Name = name,
    Rows = new[] { union, single, single, union },
    InputHash = InputHash(input),
    Method = "Independent integer enumeration of Thunder Dragon reveal histories with conditional terminal hand counting",
    Source = "research/random-discard/browser/thunder-reference/README.md",
    Normalized = true
}));

static void ValidateModel(JsonElement input, int handSize) {
    const string dark = "metadata:attribute:dark";
    const string thunder = "user:Thunder Dragon monster";
    const string solar = "user:Solar";
    const string fusion = "user:Fusion";
    if (input.GetProperty("HandSize").GetInt32() != handSize
        || input.GetProperty("Options").GetProperty("DrawActivationPriority").ValueKind != JsonValueKind.Null
        || input.GetProperty("Groups").GetArrayLength() != 1 || input.GetProperty("Groups")[0].GetProperty("Id").GetString() != "g") {
        throw new InvalidDataException("The oracle requires this list's encounter-order activation and single goal group.");
    }
    JsonElement[] cards = input.GetProperty("Cards").EnumerateArray().ToArray();
    Dictionary<string, int> counts = new();
    foreach (JsonElement card in cards) {
        int copies = card.GetProperty("Copies").GetInt32();
        HashSet<string?> categories = card.GetProperty("Categories").EnumerateArray()
            .Select(category => category.GetProperty("Identity").GetString()).ToHashSet();
        bool material = categories.Contains(dark);
        bool family = categories.Contains(thunder);
        if (card.GetProperty("PreDrawRemovalCount").GetInt32() != 0 || card.GetProperty("PreDrawDeckRemovalCount").GetInt32() != 0
            || card.GetProperty("PostDrawDeckMillCount").GetInt32() != 0 || card.GetProperty("MinimumHandSize").GetInt32() != 0
            || card.GetProperty("FirstActivationOnly").GetBoolean() || card.GetProperty("BlocksLaterDraws").GetBoolean()
            || HasValue(card, "Search") || HasValue(card, "Excavation")) {
            throw new InvalidDataException("Unexpected additional effect in the Thunder player input.");
        }
        if (categories.Contains(solar) && (material || family || categories.Contains(fusion)) || categories.Contains(fusion) && (material || family)) {
            throw new InvalidDataException("Both three-copy goal pieces must belong to the non-DARK passive pool.");
        }
        string kind;
        if (card.TryGetProperty("DrawOperation", out JsonElement operation) && operation.GetInt32() == 1) {
            if (!material || family || copies != 2 || card.GetProperty("DrawOncePerTurn").GetBoolean()
                || HasValue(card, "DrawCount") || card.GetProperty("PostDrawDiscardCount").GetInt32() != 0) {
                throw new InvalidDataException("Each Danger identity requires two repeatable, non-goal DARK copies.");
            }
            kind = "Danger";
        } else if (HasValue(card, "DrawCount")) {
            JsonElement selector = card.GetProperty("PostDrawSelector");
            if (material || family || categories.Contains(solar) || categories.Contains(fusion) || card.GetProperty("DrawOncePerTurn").GetBoolean()
                || card.GetProperty("DrawCount").GetInt32() != 2 || card.GetProperty("PostDrawDiscardCount").GetInt32() != 1
                || selector.GetProperty("Kind").GetString() != "Category" || selector.GetProperty("Categories").GetArrayLength() != 1
                || selector.GetProperty("Categories")[0].GetString() != dark || card.GetProperty("PostDrawFailureBehavior").GetInt32() != 0) {
                throw new InvalidDataException("The draw operation must be repeatable Allure with mandatory DARK removal.");
            }
            kind = "Allure";
        } else if (material) {
            kind = family ? "DARK Thunder" : "Other DARK";
        } else if (family) {
            kind = "Light Thunder";
        } else {
            kind = categories.Contains(solar) ? "Solar" : categories.Contains(fusion) ? "Fusion" : "Other passive";
        }
        counts[kind] = counts.GetValueOrDefault(kind) + copies;
    }
    Dictionary<string, int> expected = new() {
        ["Danger"] = 10, ["Allure"] = 3, ["Other DARK"] = 2, ["DARK Thunder"] = 5,
        ["Light Thunder"] = 2, ["Solar"] = 3, ["Fusion"] = 3, ["Other passive"] = 12
    };
    if (counts.Count != expected.Count || expected.Any(pair => counts.GetValueOrDefault(pair.Key) != pair.Value)
        || cards.Select(card => card.GetProperty("Id").GetString()).Distinct().Count() != cards.Length) {
        throw new InvalidDataException("The supplied physical copies differ from the named 40-card player model.");
    }
    JsonElement[] goals = input.GetProperty("Combos").EnumerateArray().ToArray();
    if (goals.Length != 2) {
        throw new InvalidDataException("Two goal rows are required.");
    }
    for (int index = 0; index < goals.Length; index++) {
        JsonElement goal = goals[index];
        JsonElement[] requirements = goal.GetProperty("Categories").EnumerateArray().ToArray();
        HashSet<string?> identities = requirements.Select(r => r.GetProperty("BaseCategory").GetProperty("Identity").GetString()).ToHashSet();
        if (goal.GetProperty("GroupId").GetString() != "g" || goal.GetProperty("Cards").GetArrayLength() != 0
            || goal.GetProperty("AlternativeGroups").GetArrayLength() != 0 || requirements.Length != 2
            || !identities.SetEquals([thunder, index == 0 ? solar : fusion])
            || requirements.Any(r => r.GetProperty("MinCount").GetInt32() != 1 || r.GetProperty("MaximumMode").GetString() != "HandSize")) {
            throw new InvalidDataException("The two goal rows must match Solar/Fusion plus a Thunder Dragon monster.");
        }
    }
}

static bool HasValue(JsonElement element, string property) =>
    element.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null;

// Match the fixture exporter's recursively sorted JSON hash.
static string InputHash(JsonElement input) {
    using MemoryStream bytes = new();
    using (Utf8JsonWriter writer = new(bytes, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) {
        Write(input, writer);
    }
    return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLower(CultureInfo.InvariantCulture);

    static void Write(JsonElement element, Utf8JsonWriter writer) {
        if (element.ValueKind == JsonValueKind.Object) {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) {
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer);
            }
            writer.WriteEndObject();
        } else if (element.ValueKind == JsonValueKind.Array) {
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray()) {
                Write(item, writer);
            }
            writer.WriteEndArray();
        } else {
            element.WriteTo(writer);
        }
    }
}
