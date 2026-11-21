using System.Numerics;

namespace CardProbability.EngineTests.Fixtures;

// Independent labeled-card oracle. Finish every fixed draw transition before
// evaluating optional search witnesses, with no production scheduler or compiler.
internal static class RevealSequenceOracle {
    internal readonly record struct Rational(BigInteger Numerator, BigInteger Denominator) {
        internal static Rational Zero => new(0, 1);
        internal static Rational One => new(1, 1);
        internal Rational Add(Rational other) => Reduce(Numerator * other.Denominator + other.Numerator * Denominator,
            Denominator * other.Denominator);
        internal Rational Scale(int numerator, int denominator) => Reduce(Numerator * numerator, Denominator * denominator);
        internal Rational Max(Rational other) => Numerator * other.Denominator >= other.Numerator * Denominator ? this : other;
        private static Rational Reduce(BigInteger numerator, BigInteger denominator) {
            BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            return new(numerator / divisor, denominator / divisor);
        }
    }

    private sealed record Token(int Label, int Row, int Encounter);
    private sealed class World {
        internal List<Token> Hand = [];
        internal List<Token> Pending = [];
        internal List<int> Deck = [];
        internal int[] Used = [];
        internal int[][] Modes = [];
        internal int NextEncounter;
        internal int DrawActivations;
        internal bool DrawLocked;
        internal bool DrawComplete;
        internal bool Failed;

        internal World Clone() => new() {
            Hand = [.. Hand], Pending = [.. Pending], Deck = [.. Deck], Used = (int[])Used.Clone(),
            Modes = Modes.Select(mode => (int[])mode.Clone()).ToArray(), NextEncounter = NextEncounter,
            DrawActivations = DrawActivations, DrawLocked = DrawLocked, DrawComplete = DrawComplete, Failed = Failed
        };
    }

    internal static Rational[] Count(EngineCase fixture) {
        CalculationRequest request = fixture.Request;
        OracleModel model = new(request);
        DeckEntry[] cards = request.Entries.Where(entry => entry.Copies > 0).ToArray();
        if (cards.Any(card => card.Effect?.Excavation is not null)) {
            throw new NotSupportedException("The reveal sequence oracle models draws and searches.");
        }
        int handSize = request.HandSize;
        int[] labels = cards.SelectMany((card, row) => Enumerable.Repeat(row, card.Copies)).ToArray();
        bool[] relevant = cards.Select(card => request.Goals.Any(goal => model.AllRequirements(goal)
            .Any(requirement => requirement.Minimum > 0 && model.Matches(requirement, card)))).ToArray();
        if (labels.Length > 8) {
            throw new ArgumentException("The independent labeled oracle is restricted to eight physical cards.");
        }
        Dictionary<string, List<World>> openers = [];
        int[] order = Enumerable.Range(0, labels.Length).ToArray();
        Enumerate(0);
        Func<IReadOnlyList<DeckEntry>, bool>[] individual = request.Goals.Select(goal =>
            (Func<IReadOnlyList<DeckEntry>, bool>)(hand => model.GoalMatches(hand, goal))).ToArray();
        Func<IReadOnlyList<DeckEntry>, bool>[] objectives = [hand => individual.Any(match => match(hand)), .. individual,
            .. request.Groups.Select(group => (Func<IReadOnlyList<DeckEntry>, bool>)(hand => request.Goals.Select((goal, index) => (goal, index))
                .Any(entry => entry.goal.GroupId == group && individual[entry.index](hand))))];
        int total = openers.Values.Sum(worlds => worlds.Count);
        return objectives.Select(objective => openers.Values.Aggregate(Rational.Zero,
            (sum, worlds) => sum.Add(Resolve(worlds, objective).Scale(worlds.Count, total)))).ToArray();

        void Enumerate(int position) {
            if (position < order.Length) {
                for (int index = position; index < order.Length; index++) {
                    (order[position], order[index]) = (order[index], order[position]);
                    Enumerate(position + 1);
                    (order[position], order[index]) = (order[index], order[position]);
                }
                return;
            }
            World world = new() {
                Hand = order.Take(handSize).Select((label, encounter) => new Token(label, labels[label], encounter)).ToList(),
                Deck = order.Skip(handSize).ToList(), Used = new int[cards.Length],
                Modes = cards.Select(card => new int[Math.Max(1, card.Effect?.Search?.Modes.Length ?? 0)]).ToArray(), NextEncounter = handSize
            };
            world.Pending = world.Hand.Where(token => cards[token.Row].Effect is not null).ToList();
            string observation = Observation(world);
            if (!openers.TryGetValue(observation, out List<World>? worlds)) {
                openers.Add(observation, worlds = []);
            }
            worlds.Add(world);
        }

        Rational Resolve(List<World> worlds, Func<IReadOnlyList<DeckEntry>, bool> objective) {
            World known = worlds[0];
            if (known.Failed) {
                return Rational.Zero;
            }
            if (known.Pending.Any(token => Expired(known, token))) {
                worlds = worlds.Select(world => {
                    World next = world.Clone();
                    next.Pending.RemoveAll(token => Expired(next, token));
                    return next;
                }).ToList();
                known = worlds[0];
            }
            IEnumerable<Token> candidates = known.Pending.Where(token => known.DrawComplete == (cards[token.Row].Effect?.Search is not null))
                .OrderBy(token => Rank(cards[token.Row].Id)).ThenBy(token => token.Encounter);
            foreach (Token candidate in candidates) {
                DeckEntry entry = cards[candidate.Row];
                EffectPlan effect = entry.Effect!;
                if (known.Used[candidate.Row] >= AllowanceLimit(entry) || known.Hand.Count < effect.MinimumHandSize
                    || known.Hand.Count(token => token.Encounter != candidate.Encounter
                        && model.Eligible(effect.HandCostEligibility, cards[token.Row])) < effect.HandCost) {
                    continue;
                }
                if (effect.RandomDiscardReveal) {
                    if (known.DrawLocked || known.Deck.Count == 0) {
                        continue;
                    }
                    // Every observed world has the same labeled hand. Expand one
                    // equally weighted successor per physical card, including the
                    // revealed token. No production transition or weighting helper.
                    List<World> after = worlds.SelectMany(world => world.Hand.Select(discarded =>
                        Reveal(world, candidate.Encounter, discarded.Encounter))).ToList();
                    return AverageObserved(after, objective);
                }
                if (effect.DrawCount is int draws) {
                    if (known.DrawLocked || effect.FirstActivationOnly && known.DrawActivations > 0
                        || known.Deck.Count < draws + effect.HiddenDeckCost + effect.HiddenPostDrawMill) {
                        continue;
                    }
                    List<World> after = worlds.Select(world => Draw(world, candidate.Encounter)).ToList();
                    return AverageObserved(after, objective);
                }
                SearchOperation search = effect.Search!;
                SearchMode[] modes = search.Modes.IsEmpty ? [new(search.Targets)] : search.Modes.ToArray();
                if (modes.Where((mode, index) => known.Modes[candidate.Row][index] < (mode.ActivationLimit ?? int.MaxValue)).Any() == false) {
                    continue;
                }
                // Inspecting the remaining deck exposes counts, never a permutation.
                List<List<World>> inspections = worlds.GroupBy(world => string.Join(',',
                    Enumerable.Range(0, cards.Length).Select(row => world.Deck.Count(label => labels[label] == row))))
                    .Select(group => group.ToList()).ToList();
                if (inspections.Count > 1) {
                    return inspections.Aggregate(Rational.Zero,
                        (sum, observed) => sum.Add(Resolve(observed, objective).Scale(observed.Count, worlds.Count)));
                }
                List<(int Mode, int Target)> actions = [];
                for (int mode = 0; mode < modes.Length; mode++) {
                    if (known.Modes[candidate.Row][mode] >= (modes[mode].ActivationLimit ?? int.MaxValue)) {
                        continue;
                    }
                    for (int row = 0; row < cards.Length; row++) {
                        if (row != candidate.Row && known.Deck.Any(label => labels[label] == row)
                            && model.Eligible(modes[mode].Targets, cards[row])) {
                            actions.Add((mode, row));
                        }
                    }
                }
                if (actions.Count == 0) {
                    continue;
                }
                List<World> declined = worlds.Select(world => {
                    World next = world.Clone();
                    next.Pending.RemoveAll(token => token.Encounter == candidate.Encounter);
                    return next;
                }).ToList();
                Rational best = Resolve(declined, objective);
                foreach ((int mode, int target) in actions) {
                    List<World> after = worlds.Select(world => Search(world, candidate.Encounter, mode, target)).ToList();
                    best = best.Max(AverageObserved(after, objective));
                }
                return best;
            }
            if (!known.DrawComplete) {
                foreach (World world in worlds) {
                    world.DrawComplete = true;
                    world.Pending.RemoveAll(token => IsDraw(cards[token.Row]));
                }
                return Resolve(worlds, objective);
            }
            return objective(known.Hand.Select(token => cards[token.Row]).ToArray()) ? Rational.One : Rational.Zero;
        }

        Rational AverageObserved(List<World> worlds, Func<IReadOnlyList<DeckEntry>, bool> objective) => worlds.GroupBy(Observation)
            .Aggregate(Rational.Zero, (sum, group) => sum.Add(Resolve(group.ToList(), objective).Scale(group.Count(), worlds.Count)));

        World Reveal(World original, int encounter, int discardedEncounter) {
            World next = original.Clone();
            Token source = next.Hand.Single(token => token.Encounter == encounter);
            Token discarded = next.Hand.Single(token => token.Encounter == discardedEncounter);
            string sourceIdentity = cards[source.Row].RevealIdentity!;
            string? discardedIdentity = cards[discarded.Row].RevealIdentity;
            next.Hand.Remove(discarded);
            next.Pending.Remove(discarded);
            next.DrawActivations++;
            if (sourceIdentity == discardedIdentity) {
                return next;
            }
            next.Hand.Remove(source);
            next.Pending.Remove(source);
            int label = next.Deck[0];
            next.Deck.RemoveAt(0);
            Token drawn = new(label, labels[label], next.NextEncounter++);
            next.Hand.Add(drawn);
            if (cards[drawn.Row].Effect is not null) {
                next.Pending.Add(drawn);
            }
            return next;
        }

        World Draw(World original, int encounter) {
            World next = original.Clone();
            Token source = next.Hand.Single(token => token.Encounter == encounter);
            EffectPlan effect = cards[source.Row].Effect!;
            next.Pending.Remove(source);
            RemoveFixed(next, effect.HandCost, effect.HandCostEligibility, source);
            Consume(next, source);
            next.Deck.RemoveRange(0, effect.HiddenDeckCost);
            for (int draw = 0; draw < effect.DrawCount!.Value; draw++) {
                int label = next.Deck[0];
                next.Deck.RemoveAt(0);
                Token drawn = new(label, labels[label], next.NextEncounter++);
                next.Hand.Add(drawn);
                if (cards[drawn.Row].Effect is not null) {
                    next.Pending.Add(drawn);
                }
            }
            next.DrawActivations++;
            next.DrawLocked |= effect.BlocksLaterDraws;
            next.Deck.RemoveRange(0, effect.HiddenPostDrawMill);
            RemoveFixed(next, effect.RemovalCount, effect.RemovalEligibility);
            return next;
        }

        World Search(World original, int encounter, int mode, int target) {
            World next = original.Clone();
            Token source = next.Hand.Single(token => token.Encounter == encounter);
            EffectPlan effect = cards[source.Row].Effect!;
            Consume(next, source);
            next.Modes[source.Row][mode]++;
            RemoveFixed(next, effect.HandCost, effect.HandCostEligibility);
            // A fixed label, independent of deck position. Removing the FIRST
            // matching position would bias later draws when copies are repeated.
            int label = next.Deck.Where(label => labels[label] == target).Min();
            next.Deck.Remove(label);
            next.Hand.Add(new(label, target, next.NextEncounter++));
            RemoveFixed(next, effect.RemovalCount, effect.RemovalEligibility);
            return next;
        }

        void Consume(World world, Token source) {
            world.Hand.Remove(source);
            world.Pending.Remove(source);
            world.Used[source.Row]++;
        }

        void RemoveFixed(World world, int count, Eligibility? selector, Token? reserved = null) {
            for (int removal = 0; removal < count; removal++) {
                List<Token> choices = world.Hand.Where(token => token != reserved && model.Eligible(selector, cards[token.Row])).ToList();
                if (choices.Count == 0) {
                    world.Failed = true;
                    return;
                }
                HashSet<int> payableDraws = [];
                HashSet<int> deferredDraws = [];
                long minimumNextDraw = reserved is not null && IsDraw(cards[reserved.Row])
                    ? Consumption(cards[reserved.Row]) : long.MaxValue;
                foreach (Token pending in world.Pending) {
                    DeckEntry effect = cards[pending.Row];
                    if (world.DrawComplete || !IsDraw(effect) || !DrawTiming(world, effect) || Allowance(pending.Row) <= 0
                        || Consumption(effect) > world.Deck.Count
                        || reserved is not null && IsDraw(cards[reserved.Row])
                            && (cards[reserved.Row].Effect!.BlocksLaterDraws || effect.Effect!.FirstActivationOnly)) {
                        continue;
                    }
                    bool payable = world.Hand.Count - (reserved is null ? 0 : 1) >= effect.Effect!.MinimumHandSize
                        && world.Hand.Count(token => token != pending && token != reserved
                            && model.Eligible(effect.Effect!.HandCostEligibility, cards[token.Row])) >= effect.Effect!.HandCost;
                    if (payable) {
                        payableDraws.Add(pending.Row);
                        minimumNextDraw = Math.Min(minimumNextDraw, Consumption(effect));
                    }
                    else if (effect.Effect!.HandCost > 0 || effect.Effect!.MinimumHandSize > 0) {
                        deferredDraws.Add(pending.Row);
                    }
                }
                Token removed = choices.OrderByDescending(token => Tier(token.Row))
                    .ThenByDescending(token => model.OrderKey(cards[token.Row]), StringComparer.Ordinal)
                    .ThenByDescending(token => IsDraw(cards[token.Row]))
                    .ThenByDescending(token => cards[token.Row].Id, StringComparer.Ordinal)
                    // Within one entry preserve naturally eligible copies before
                    // inert fetched/declined copies, then preserve earlier FIFO.
                    .ThenBy(token => world.Pending.Contains(token))
                    .ThenByDescending(token => token.Encounter).First();
                world.Hand.Remove(removed);
                world.Pending.Remove(removed);

                int Allowance(int row) => AllowanceLimit(cards[row]) - world.Used[row]
                    - (reserved?.Row == row ? 1 : 0);

                int Tier(int row) {
                    DeckEntry card = cards[row];
                    int held = world.Hand.Count(token => token.Row == row && token != reserved);
                    bool redundant = held > Allowance(row);
                    bool deferred = deferredDraws.Contains(row) && !redundant && !card.Effect!.FirstActivationOnly
                        && minimumNextDraw != long.MaxValue && Consumption(card) <= world.Deck.Count - minimumNextDraw;
                    int tier = relevant[row] ? 1 : payableDraws.Contains(row) && !redundant ? 2 : deferred ? 3 : 4;
                    long searchCapacity = world.Deck.Count - (reserved is null ? 0 : Consumption(cards[reserved.Row]));
                    if (card.Effect?.Search is not { } search || !world.Pending.Any(token => token.Row == row)
                        || Allowance(row) <= 0 || searchCapacity <= 0) {
                        return tier;
                    }
                    SearchMode[] modes = search.Modes.IsEmpty ? [new(search.Targets)] : search.Modes.ToArray();
                    for (int mode = 0; mode < modes.Length; mode++) {
                        if (world.Modes[row][mode] >= (modes[mode].ActivationLimit ?? int.MaxValue)) {
                            continue;
                        }
                        for (int target = 0; target < cards.Length; target++) {
                            // Plausibility uses original copies minus held copies.
                            // Inspecting hidden survivors must not change removal.
                            if (target != row && cards[target].Copies > world.Hand.Count(token => token.Row == target)
                                && model.Eligible(modes[mode].Targets, cards[target])) {
                                tier = Math.Min(tier, relevant[target] ? 1 : 2);
                            }
                        }
                    }
                    return tier;
                }
            }
        }

        bool Expired(World world, Token token) => world.Used[token.Row] >= AllowanceLimit(cards[token.Row])
            || !DrawTiming(world, cards[token.Row]) || Consumption(cards[token.Row]) > world.Deck.Count;
        bool DrawTiming(World world, DeckEntry card) => !IsDraw(card)
            || !world.DrawLocked && (card.Effect!.FirstActivationOnly == false || world.DrawActivations == 0);
        long Consumption(DeckEntry card) => card.Effect!.Search is not null || card.Effect.RandomDiscardReveal ? 1
            : (long)card.Effect.DrawCount!.Value + card.Effect.HiddenDeckCost + card.Effect.HiddenPostDrawMill;

        int Rank(string id) {
            int index = fixture.Options.PriorityEntryIds.IndexOf(id);
            return fixture.Options.ActivationOrder == ActivationOrder.Priority && index >= 0 ? index : int.MaxValue;
        }

        static bool IsDraw(DeckEntry card) => card.Effect is { DrawCount: not null } or { RandomDiscardReveal: true };
        static int AllowanceLimit(DeckEntry card) => card.Effect?.ActivationLimit ?? int.MaxValue;
        string Observation(World world) => world.Failed ? "failed"
            : string.Join(',', world.Hand.Select(token => $"{token.Row}:{token.Encounter}")) + ";"
                + string.Join(',', world.Pending.Select(token => token.Encounter)) + ";" + string.Join(',', world.Used) + ";"
                + string.Join('|', world.Modes.Select(mode => string.Join(',', mode)))
                + $";{world.NextEncounter};{world.DrawActivations};{world.DrawLocked};{world.DrawComplete};{world.Deck.Count}";
    }

}
