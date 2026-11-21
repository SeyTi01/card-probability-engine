using System.Text.Json;

namespace CardProbability.EngineTests.Fixtures;

internal sealed record FrozenRevealCase(
    string Name,
    int HandSize,
    Dictionary<string, int> Copies,
    string[] Priority,
    FrozenRevealCase.DirectRequirement[] DirectRequirements,
    string[]? Reference
) {
    internal sealed record DirectRequirement(int Goal, string Entry, int Minimum, int Maximum);

    internal static FrozenRevealCase[] ReadAll() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Data", "reveal-cases.json");
        return JsonSerializer.Deserialize<FrozenRevealCase[]>(File.ReadAllText(path))!;
    }

    internal static FrozenRevealCase Load(string name) => ReadAll().Single(row => row.Name == name);

    internal EngineCase Create(CalculationMode mode) {
        List<Requirement>[] requirements = [
            [Requirement.InMembership("starter", 1, 5), Requirement.InMembership("extender", 1, 5)],
            [Requirement.InMembership("both", 1, 5)],
            [Requirement.InMembership("starter", 0, 0), Requirement.InMembership("extender", 1, 5)]
        ];
        foreach (DirectRequirement bound in DirectRequirements) {
            requirements[bound.Goal].Add(Requirement.OfEntry(bound.Entry, bound.Minimum, bound.Maximum));
        }
        Membership[] memberships = [
            new("starter", ["starter", "overlap"]),
            new("extender", ["extender", "overlap"]),
            new("both", ["overlap"]),
            new("material", Copies.Keys.Where(id => id is "starter" or "overlap" || IsReveal(id)))
        ];
        // Preserve the source fixture's ordinal policy partition. The resolved
        // intersection is a predicate, not an additional original ordering bit.
        string[] direct = DirectRequirements.Select(bound => bound.Entry).Distinct().Order(StringComparer.Ordinal).ToArray();
        List<DeckEntry> entries = [];
        foreach ((string id, int copies) in Copies) {
            string policy = (id is "extender" or "overlap" ? "1" : "0") + (id is "starter" or "overlap" ? "1" : "0")
                + string.Concat(direct.Select(entry => entry == id ? '1' : '0'));
            EffectPlan? effect = id switch {
                "1-filtered" => new(DrawCount: 2, ActivationLimit: null, RemovalCount: 1, RemovalEligibility: new("material")),
                "4-draw" => new(DrawCount: 1, ActivationLimit: null),
                "6-remove" => new(DrawCount: 3, ActivationLimit: null, RemovalCount: 2),
                "search" => new(ActivationLimit: null, Search: new(new("starter"))),
                _ when IsReveal(id) => new(ActivationLimit: null) { RandomDiscardReveal = true },
                _ => null
            };
            entries.Add(new(id, copies, effect, policy) { RevealIdentity = IsReveal(id) ? id : null });
        }
        Goal[] goals = [new("distinct", requirements[0], groupId: "g"), new("overlap", requirements[1], groupId: "g"),
            new("exclusion", requirements[2], groupId: "h")];
        return new(Name, new(entries, HandSize, goals, memberships, ["g", "h"]),
            new(mode, activationOrder: Priority.Length == 0 ? ActivationOrder.EncounterOrder : ActivationOrder.Priority, priorityEntryIds: Priority));
    }

    private static bool IsReveal(string id) => id is "2-reveal" or "3-reveal" or "5-reveal";
}
