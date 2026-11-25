using System.Numerics;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    internal sealed record SymbolicRevealResult(BigInteger[] Wins, BigInteger Resolved, BigInteger Denominator);

    internal SymbolicRevealResult? TryCountSymbolicReveal(List<Combo> combos, IReadOnlyList<ComboGroup>? groups, bool exhaustive = false) {
        CalculationResultRows rows = new(combos, groups);
        if (rows.Count > 64 || handSize is < 1 or > 10
            || !deck.Any(card => card.Copies > 0 && card.IsRandomDiscardReveal)
            || deck.Any(card => card.Copies > 0 && !SupportsSymbolicEffect(card))
            || deck.Sum(card => (long)card.Copies) > 100) {
            return null;
        }

        List<Card> cards = CoalescePhysicalSearchDeck(ReduceSearchDeck(combos, out _, merge: false), combos);
        cards = PoolUnobservedRevealRows(cards, combos);
        int[] active = Enumerable.Range(0, cards.Count).Where(row => cards[row].HasDrawEffect && cards[row].Copies > 0).ToArray();
        int[] passive = Enumerable.Range(0, cards.Count).Where(row => !cards[row].HasDrawEffect && cards[row].Copies > 0).ToArray();
        if (active.Length is < 1 or > 7 || passive.Length is < 1 or > 7 || active.Any(row => cards[row].Copies > 7)) {
            return null;
        }
        int total = cards.Sum(card => card.Copies);
        long maximumObserved = handSize + active.Sum(row => (long)cards[row].Copies * cards[row].DrawBatchSize);
        long maximumTemporaryHand = (long)handSize + active.Max(row => cards[row].PostDrawDiscardCount);
        long maximumDead = Math.Min(handSize, active.Where(row => cards[row].IsRandomDiscardReveal).Sum(row => cards[row].Copies))
            + active.Sum(row => (long)cards[row].Copies * cards[row].PostDrawDiscardCount);
        int slotWidth = Math.Max(5, passive.Length);
        int passiveSlotCapacity = 64 / slotWidth;
        if (maximumObserved >= total || maximumTemporaryHand > Math.Min(10, passiveSlotCapacity) || maximumDead > passiveSlotCapacity) {
            return null;
        }
        HashSet<string?> revealIdentities = active.Where(row => cards[row].IsRandomDiscardReveal).Select(row => cards[row].RevealIdentity).ToHashSet();
        if (passive.Any(row => cards[row].RevealIdentity is not null && revealIdentities.Contains(cards[row].RevealIdentity))) {
            return null;
        }

        OrderedDrawPreparation preparation = PrepareOrderedDraw(cards, combos);
        (bool[]?[] eligibility, _) = CompileHandOperations(cards);
        bool[] relevant = preparation.Relevant;
        foreach (int source in passive.Where(row => cards[row].Search is not null)) {
            SearchEffectPlan search = cards[source].Search!;
            int[] targets = Enumerable.Range(0, cards.Count).Where(row => row != source && search.Matches(cards[row])).ToArray();
            budget.Spend(cards.Sum(card => (long)(card.Categories.Count + 1) * (search.Selector.EvaluationSize + 1)));
            if (targets.Any(row => cards[row].HasDrawEffect)) {
                return null;
            }
            if (targets.Length == 0 || relevant[source]) {
                continue;
            }
            bool positive = targets.Any(row => relevant[row]);
            // While this search is held, at most H-1 other cards can be held.
            // Thus at least one original target of its best tier is always
            // outside the hand, which makes the existing retention tier static.
            int copies = targets.Where(row => relevant[row] == positive).Sum(row => cards[row].Copies);
            if (copies < maximumTemporaryHand && active.Any(row => cards[row].PostDrawDiscardCount > 0
                && (eligibility[row] is null || eligibility[row]![source]))) {
                return null;
            }
        }
        CheckStorage(cards.Count, cards.Count * (long)(active.Length + 6), "symbolic reveal descriptors");
        SearchKeepPolicy searchPolicy = new(cards, relevant, budget);
        int[] empty = new int[cards.Count];
        int[] ranks = new int[cards.Count];
        for (int row = 0; row < cards.Count; row++) {
            int tier = relevant[row] ? 1 : cards[row].HasDrawEffect ? 2 : 4;
            tier = Math.Min(tier, searchPolicy.Tier(row, empty, 0, total));
            ranks[row] = tier * cards.Count + row;
        }
        int[][] removal = new int[active.Length + 1][];
        for (int index = 0; index < active.Length; index++) {
            int source = active[index];
            bool[]? allowed = eligibility[source];
            // Positive tokens identify queued effects; negative tokens identify
            // passive mask bits. Descending order is the production keep
            // policy's worst tier, followed by its last canonical row.
            removal[index + 1] = active.Select((row, i) => (Row: row, Token: i + 1))
                .Concat(passive.Select((row, i) => (Row: row, Token: -i - 1)))
                .Where(entry => allowed is null || allowed[entry.Row]).OrderByDescending(entry => ranks[entry.Row])
                .Select(entry => entry.Token).ToArray();
        }
        SymbolicTerminalCounter terminal = new(cards, combos, rows, passive, active, removal, budget);
        int[] order = active.Select(row => preparation.CustomPriority ? preparation.Ranks[row] : 0).Prepend(0).ToArray();
        SymbolicResolver resolver = new(this, budget, cards, active, passive, removal, order, terminal, rows.Count,
            preparation.Factorial, handSize, total - (int)maximumObserved, exhaustive);
        return resolver.Run();
    }

    private static bool SupportsSymbolicEffect(Card card) {
        if (card.Excavation is not null || card.EffectPlan.HasActivationRestrictions || card.PreDrawRemovalCount != 0
            || card.PreDrawDeckRemovalCount != 0 || card.PostDrawDeckMillCount != 0 || card.EffectPlan.RequiresSearchResolution) {
            return false;
        }
        return !card.HasDrawEffect || (card.MaximumActivations >= card.Copies
            && (card.IsRandomDiscardReveal || card.DrawBatchSize == (long)card.PostDrawDiscardCount + 1));
    }

    private sealed partial class SymbolicResolver {
        private readonly record struct Position(int Deck, uint Queue, ulong Held, ulong Dead, int Taken, bool Compared = false);
        private readonly record struct Leaf(ulong Held, ulong Dead, int Taken, int Pending, int Remove);
        private readonly int _maskBits;
        private readonly ulong _slotMask;
        private const int QueueBits = 3;
        private const int Failed = -2;
        private readonly PostDrawCalculation _owner;
        private readonly WorkBudget _budget;
        private readonly int[] _active;
        private readonly int[] _priority;
        private readonly int[] _batch;
        private readonly int[] _discard;
        private readonly bool[] _reveal;
        private readonly string?[] _identity;
        private readonly int[][] _removal;
        private readonly SymbolicTerminalCounter _terminal;
        private readonly int _handSize;
        private readonly bool _exhaustive;
        private readonly int _passiveCopies;
        private readonly int _activeCopies;
        private readonly int _initialDeck;
        private readonly int _full;
        private readonly BigInteger _randomChoices;
        private readonly int _minimumRemaining;
        private readonly BigInteger[] _suffixes;
        private readonly int[,] _choose;
        private readonly Dictionary<Position, BigInteger>[] _layers;
        private readonly long[] _layerCells;
        private readonly Dictionary<Leaf, BigInteger> _leaves = [];
        private readonly BigInteger[] _wins;
        private readonly int _framesPerBatch;
        private readonly long _cellsPerBatch;
        private int _drainDepth;
        private BigInteger _resolved;
        private long _leafCells;

        internal SymbolicResolver(
            PostDrawCalculation owner, WorkBudget budget, List<Card> cards, int[] active, int[] passive,
            int[][] removal, int[] priority, SymbolicTerminalCounter terminal, int rowCount, BigInteger[] factorial,
            int handSize, int minimumRemaining, bool exhaustive
        ) {
            _owner = owner;
            _budget = budget;
            _active = active;
            _priority = priority;
            _batch = active.Select(row => cards[row].DrawBatchSize).Prepend(0).ToArray();
            _discard = active.Select(row => cards[row].PostDrawDiscardCount).Prepend(0).ToArray();
            _reveal = active.Select(row => cards[row].IsRandomDiscardReveal).Prepend(false).ToArray();
            _identity = active.Select(row => cards[row].RevealIdentity).Prepend(null).ToArray();
            _removal = removal;
            _exhaustive = exhaustive;
            _colors = exhaustive ? [] : FindColors(cards);
            _colorScale = 1;
            for (int i = 0; i < _colors.Length; i++) {
                _colorMask |= 1 << _colors[i];
                _colorScale *= i + 1;
            }
            _terminal = terminal;
            _handSize = handSize;
            _passiveCopies = passive.Sum(row => cards[row].Copies);
            _activeCopies = active.Sum(row => cards[row].Copies);
            _maskBits = Math.Max(5, passive.Length);
            _slotMask = (1UL << _maskBits) - 1;
            _full = (1 << passive.Length) - 1;
            _randomChoices = factorial[handSize] * _colorScale;
            // A live activation retains the draw word, its mandatory removals
            // and their integer coefficients. Only an actual nested drain
            // keeps another such path on the call stack.
            _framesPerBatch = 8 + Math.Max(handSize, _batch.Max()) + 3 * _discard.Max();
            long coefficientCells = WorkBudget.IntegerCells(factorial[^1]) + WorkBudget.IntegerCells(_randomChoices) + 1;
            _cellsPerBatch = _framesPerBatch * (18L + 4 * coefficientCells);
            owner.CheckStorage(_framesPerBatch, _cellsPerBatch, "symbolic reveal transition stack");
            // At least this many cards remain after every possible operation.
            // Their unseen permutations multiply every terminal mass and the
            // denominator equally. Retain only remaining! / minimumRemaining!.
            _minimumRemaining = minimumRemaining;
            _suffixes = new BigInteger[factorial.Length - minimumRemaining];
            long suffixCells = _suffixes.Length;
            _suffixes[0] = BigInteger.One;
            for (int i = 1; i < _suffixes.Length; i++) {
                _budget.Spend(2 + WorkBudget.IntegerCells(_suffixes[i - 1]));
                _suffixes[i] = _suffixes[i - 1] * (minimumRemaining + i);
                suffixCells += WorkBudget.IntegerCells(_suffixes[i]);
            }
            owner.CheckStorage(_suffixes.Length, suffixCells + 1, "symbolic unseen suffixes");
            for (int i = 0; i < active.Length; i++) {
                _initialDeck |= cards[active[i]].Copies << (QueueBits * i);
            }
            _layers = Enumerable.Range(0, _activeCopies + 1).Select(_ => new Dictionary<Position, BigInteger>()).ToArray();
            _layerCells = new long[_layers.Length];
            _wins = new BigInteger[rowCount];
            int maxDraw = Math.Max(handSize, _batch.Max());
            _choose = new int[maxDraw + 1, maxDraw + 1];
            for (int n = 0; n <= maxDraw; n++) {
                _choose[n, 0] = _choose[n, n] = 1;
                for (int k = 1; k < n; k++) {
                    _choose[n, k] = _choose[n - 1, k] + _choose[n - 1, k - 1];
                }
            }
            owner.CheckStorage(_layers.Length, _layers.Length * 4L, "symbolic reveal layers");
            owner.CheckStorage(_choose.Length, _choose.Length, "symbolic draw multiplicities");
            budget.Spend(_choose.Length + _layers.Length + active.Length * 8L);
            if (!exhaustive) {
                PreparePriorities(passive.Select(row => cards[row].Copies).ToArray(), factorial);
            }
        }

        internal SymbolicRevealResult Run() {
            // Each successful ordinary effect preserves settled hand size.
            // Each reveal reduces it by one, so the product of random-hand
            // denominators on every path divides h!, including same identities.
            Draw(new(_initialDeck, 0, 0, 0, 0), _handSize, 0, 0, _randomChoices, 0);
            for (int active = _activeCopies; active > 0; active--) {
                Drain(active);
                _layers[active].TrimExcess();
            }
            DrainLeaves();
            BigInteger denominator = _suffixes[^1] * _randomChoices;
            if (_exhaustive && _resolved != denominator) {
                throw new InvalidOperationException("Exhaustive reveal counting must classify every physical history.");
            }
            return new(_wins, _resolved, denominator);
        }

        private void DrainLeaves() {
            // All completions within a group share the same number of passive
            // encounters and terminal policy. Grouping keeps unrelated
            // conditional-hand values from competing for optional cache space.
            // Sort keys only, leaving the accumulated integers in the map.
            long sortCells = 8L * _leaves.Count;
            if (sortCells <= _budget.StoragePolicy.CellLimit) {
                _owner.CheckStorage(_leaves.Count, sortCells, "symbolic terminal order");
                int levels = _leaves.Count > 1 ? (int)uint.Log2((uint)_leaves.Count) + 1 : 1;
                _budget.Spend((3L * levels + 9) * _leaves.Count);
                Leaf[] order = _leaves.Keys.ToArray();
                Array.Sort(order, static (left, right) => {
                    int compare = left.Pending.CompareTo(right.Pending);
                    if (compare != 0) {
                        return compare;
                    }
                    compare = left.Remove.CompareTo(right.Remove);
                    return compare != 0 ? compare : left.Taken.CompareTo(right.Taken);
                });
                foreach (Leaf leaf in order) {
                    Classify(leaf, _leaves[leaf]);
                }
            } else {
                foreach ((Leaf leaf, BigInteger mass) in _leaves) {
                    Classify(leaf, mass);
                }
            }
            _budget.Spend(1L + _leaves.EnsureCapacity(0) + _leaves.Count);
            _leaves.Clear();
            _leafCells = 0;
        }

        private void Drain(int layer) {
            // Each activation consumes a physical source before another state
            // can enter a layer. Its descendants only write to lower layers.
            _drainDepth++;
            _owner.CheckStorage((1L + _drainDepth) * _framesPerBatch, (1L + _drainDepth) * _cellsPerBatch, "symbolic reveal transition stack");
            foreach ((Position state, BigInteger weight) in _layers[layer]) {
                Activate(state, weight);
            }
            _drainDepth--;
            _layers[layer].Clear();
            _layerCells[layer] = 0;
        }

        private void Classify(Leaf leaf, BigInteger mass) {
            if (leaf.Pending == Failed) {
                BigInteger support = _terminal.EvaluateSupport(leaf.Held, leaf.Dead, leaf.Taken);
                _budget.Spend((WorkBudget.IntegerCells(support) + 1) * (WorkBudget.IntegerCells(mass) + 1));
                _resolved += support * mass;
                return;
            }
            (BigInteger[] wins, BigInteger resolved) = _terminal.Evaluate(leaf.Held, leaf.Dead, leaf.Taken, leaf.Pending, leaf.Remove);
            _budget.Spend((WorkBudget.IntegerCells(resolved) + 1) * (WorkBudget.IntegerCells(mass) + 1) * (_wins.Length + 1));
            for (int row = 0; row < _wins.Length; row++) {
                _wins[row] += wins[row] * mass;
            }
            _resolved += resolved * mass;
        }

        private void Record(Position state, int pending, int remove, BigInteger mass) {
            int remaining = DeckCount(state.Deck) + _passiveCopies - state.Taken;
            BigInteger suffix = _suffixes[remaining - _minimumRemaining];
            BigInteger weight = mass * suffix;
            _budget.Spend(8 + (WorkBudget.IntegerCells(mass) + 1) * (WorkBudget.IntegerCells(suffix) + 1));
            int taken = state.Taken;
            if (!_terminal.HasTargets || pending == Failed) {
                // No successful witness can inspect the remaining deck. Each
                // assignment to held and restricted dead slots has exactly
                // (passiveCopies - observed)_free extensions to free dead slots.
                // Extract them now, after fixing the physical suffix factor.
                int observed = Length(state.Held, _maskBits) + Length(state.Dead, _maskBits);
                if (taken > observed) {
                    BigInteger lossWays = _terminal.FreeLossWays(observed, taken - observed);
                    _budget.Spend((WorkBudget.IntegerCells(weight) + 1) * (WorkBudget.IntegerCells(lossWays) + 1));
                    weight *= lossWays;
                    taken = observed;
                }
            }
            Leaf key = new(state.Held, state.Dead, taken, pending, remove);
            if (!Store(_leaves, key, weight, ref _leafCells)) {
                // A full map can still classify and aggregate another batch.
                // Every flush precedes insertion of the new physical mass.
                if (_leaves.Count > 0) {
                    DrainLeaves();
                }
                if (!Store(_leaves, key, weight, ref _leafCells)) {
                    Classify(key, weight);
                }
            }
        }

        private bool Store<TKey>(Dictionary<TKey, BigInteger> map, TKey key, BigInteger weight, ref long cells) where TKey : notnull {
            bool found = map.TryGetValue(key, out BigInteger previous);
            BigInteger combined = previous + weight;
            int keyCells = typeof(TKey) == typeof(Position) ? 8 : 7;
            long nextCells = cells + WorkBudget.IntegerCells(combined) - WorkBudget.IntegerCells(previous) + (found ? 0 : keyCells);
            int count = map.Count + (found ? 0 : 1);
            _budget.Spend(10 + WorkBudget.IntegerCells(combined));
            if (nextCells > _budget.StoragePolicy.CellLimit || count > _budget.StoragePolicy.EntryLimit) {
                return false;
            }
            _owner.CheckStorage(count, nextCells, "symbolic reveal weights");
            map[key] = combined;
            cells = nextCells;
            return true;
        }

        private static int Length(ulong word, int bits) => (64 - BitOperations.LeadingZeroCount(word) + bits - 1) / bits;
        private static int Token(ulong word, int i, int bits) => (int)((word >> (bits * i)) & ((1u << bits) - 1));

        private static ulong Delete(ulong word, int i, int bits) {
            ulong prefix = (1UL << (bits * i)) - 1;
            return (word & prefix) | ((word >> bits) & ~prefix);
        }

        private ulong AddMask(ulong word, int mask) {
            int shift = 0;
            while ((word >> shift) != 0 && (int)((word >> shift) & _slotMask) <= mask) {
                shift += _maskBits;
            }
            ulong prefix = (1UL << shift) - 1;
            return (word & prefix) | ((ulong)mask << shift) | ((word & ~prefix) << _maskBits);
        }

        private static int DeckCount(int deck) {
            int count = 0;
            while (deck > 0) {
                count += deck & 7;
                deck >>= QueueBits;
            }
            return count;
        }
    }
}
