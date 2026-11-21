using System.Numerics;
using CardProbability.Internal.Counting;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    // A slot is one distinguished physical encounter, even after sorting its
    // allowed-label mask alongside equal masks. Removed unrestricted slots
    // need only a count. Every unseen label still belongs to the physical deck.
    private sealed class SymbolicTerminalCounter {
        private readonly int[] _passive;
        private readonly int[] _copies;
        private readonly int[][] _removalOrder;
        private readonly int[][] _targetSources;
        private readonly int[] _sourceAllowances;
        private readonly int _full;
        private readonly int _maskBits;
        private readonly int _slotMask;
        private readonly int _total;
        private readonly int _cardRows;
        private readonly CalculationResultRows _rows;
        private readonly WorkBudget _budget;
        private readonly PhysicalSearchHandCounter _counter;
        private readonly BigInteger[] _factorial;
        private readonly BigInteger[][] _choose;
        private readonly Dictionary<ulong, ulong> _matches = new();
        private readonly Dictionary<(ulong Held, ulong Dead, int Taken), BigInteger> _support = new();
        private readonly record struct Completion(ulong Observed, int Held, int Dead, int Source, int Remove);
        private readonly Dictionary<Completion, (BigInteger[] Wins, BigInteger Resolved)> _completions = new();
        private long _completionCells;
        private (int Taken, int Source, int Remove) _group;
        private long _supportCells;
        internal bool HasTargets { get; }

        internal SymbolicTerminalCounter(
            List<Card> cards,
            List<Combo> combos,
            CalculationResultRows rows,
            int[] passiveRows,
            int[] activeRows,
            int[][] removal,
            WorkBudget budget
        ) {
            _passive = passiveRows;
            _copies = passiveRows.Select(row => cards[row].Copies).ToArray();
            _total = _copies.Sum();
            _cardRows = cards.Count;
            _rows = rows;
            _budget = budget;
            _full = (1 << passiveRows.Length) - 1;
            _maskBits = Math.Max(5, passiveRows.Length);
            _slotMask = (1 << _maskBits) - 1;
            _counter = new(cards, combos, budget);
            budget.CheckStorage(cards.Count, cards.Count * (passiveRows.Length + 3L) + 2L * passiveRows.Length * passiveRows.Length);
            _sourceAllowances = passiveRows.Select(row => cards[row].MaximumActivations).ToArray();
            _targetSources = new int[passiveRows.Length][];
            for (int p = 0; p < passiveRows.Length; p++) {
                int row = passiveRows[p];
                budget.Spend(1L + cards.Sum(card => (card.Search?.Selector.EvaluationSize ?? 0) * (cards[row].Categories.Count + 1L)));
                _targetSources[p] = Enumerable.Range(0, passiveRows.Length).Where(s =>
                    passiveRows[s] != row && cards[passiveRows[s]].Search is { } search && search.Matches(cards[row])).ToArray();
                HasTargets |= _targetSources[p].Length > 0;
            }
            _removalOrder = new int[cards.Count][];
            for (int i = 0; i < activeRows.Length; i++) {
                int[] order = removal[i + 1];
                budget.Spend(order.Length + 1L);
                _removalOrder[activeRows[i]] = order.Where(token => token < 0).Select(token => -token - 1).ToArray();
            }

            budget.CheckStorage(_total + 1L, 3L * (_total + 1) + (_total + 1L) * (_total + 2L) / 2);
            _factorial = new BigInteger[_total + 1];
            _choose = new BigInteger[_total + 1][];
            _factorial[0] = 1;
            long cells = 2 * (_total + 1L);
            for (int n = 0; n <= _total; n++) {
                budget.CheckStorage(n + 1L, cells + (n + 1L) * (n / 32 + 2L) + 2 + (n == 0 ? 1 : WorkBudget.IntegerCells(_factorial[n - 1])));
                if (n > 0) {
                    budget.Spend(2 + WorkBudget.IntegerCells(_factorial[n - 1]));
                    _factorial[n] = _factorial[n - 1] * n;
                }
                BigInteger[] values = new BigInteger[n + 1];
                values[0] = values[n] = 1;
                for (int k = 1; k < n; k++) {
                    budget.Spend(1 + WorkBudget.IntegerCells(_choose[n - 1][k - 1]) + WorkBudget.IntegerCells(_choose[n - 1][k]));
                    values[k] = _choose[n - 1][k - 1] + _choose[n - 1][k];
                }
                _choose[n] = values;
                cells += values.Length + values.Sum(WorkBudget.IntegerCells) + WorkBudget.IntegerCells(_factorial[n]);
            }
        }

        internal BigInteger FreeLossWays(int observed, int free) => Multiply(_choose[_total - observed][free], _factorial[free]);

        // A complete terminal classification, including paths whose mandatory
        // removal fails. All counts are numbers of ordered passive assignments.
        internal (BigInteger[] Wins, BigInteger Resolved) Evaluate(ulong held, ulong dead, int taken, int pendingSource, int toRemove) {
            // For a completion, taken = observed slots + free hand + free loss.
            // A different taken count or pending policy cannot reuse this group.
            // The resolver presents consecutive groups to keep the small cache
            // available to all labels sharing the current conditioning.
            var group = (taken, toRemove == 0 ? -1 : pendingSource, toRemove);
            if (_group != group) {
                _budget.Spend(1L + _completions.EnsureCapacity(0) + _completions.Count);
                _completions.Clear();
                _completionCells = 0;
                _group = group;
            }
            ulong restrictedHand = WithoutFull(held);
            ulong restrictedDead = WithoutFull(dead);
            int freeHeld = Slots(held) - Slots(restrictedHand);
            int freeDead = taken - Slots(held) - Slots(restrictedDead);
            int[] stock = (int[])_copies.Clone();
            int[] hand = new int[_passive.Length];
            CheckScratch(taken, Slots(held) + Slots(dead));
            if ((restrictedHand | restrictedDead) == 0) {
                return CompleteFree(stock, hand, freeHeld, freeDead, taken, pendingSource, toRemove);
            }
            BigInteger[] wins = new BigInteger[_rows.Count];
            BigInteger resolved = 0;
            Assign(restrictedHand, restrictedDead, stock, hand, BigInteger.One, weight => {
                (BigInteger[] conditional, BigInteger support) = CompleteFree(stock, hand, freeHeld, freeDead, taken, pendingSource, toRemove);
                for (int row = 0; row < wins.Length; row++) {
                    BigInteger increment = Multiply(weight, conditional[row]);
                    _budget.Spend(1 + WorkBudget.IntegerCells(increment) + WorkBudget.IntegerCells(wins[row]));
                    wins[row] += increment;
                }
                BigInteger mass = Multiply(weight, support);
                _budget.Spend(1 + WorkBudget.IntegerCells(mass) + WorkBudget.IntegerCells(resolved));
                resolved += mass;
            });
            return (wins, resolved);
        }

        private (BigInteger[] Wins, BigInteger Resolved) CompleteFree(
            int[] stock,
            int[] hand,
            int freeHeld,
            int free,
            int taken,
            int pendingSource,
            int toRemove
        ) {
            // Only conditioned labels occur in this key. The unconstrained hand
            // and loss draws have the same law after any restrictions that
            // selected the same hand counts and left the same physical stock.
            ulong observed = 0;
            int n = _passive.Length;
            for (int p = 0; p < n; p++) {
                // At most 12 held and 12 restricted discarded slots are packed.
                observed |= (ulong)((_copies[p] - stock[p]) * 16 + hand[p]) << (9 * p);
            }
            Completion completionKey = new(observed, freeHeld, free, toRemove == 0 ? -1 : pendingSource, toRemove);
            _budget.Spend(n + 6L);
            if (_completions.TryGetValue(completionKey, out var cached)) {
                return cached;
            }
            int outside = _total - taken + free;
            BigInteger freeChoices = _choose[outside][free];
            int[] final = new int[n];
            int[] available = new int[_cardRows];
            int[] physicalHand = new int[_cardRows];
            int[] caps = new int[n];
            int[] uncertain = new int[n];
            BigInteger[] fullWins = new BigInteger[_rows.Count];
            BigInteger[] partialWins = new BigInteger[_rows.Count];
            BigInteger resolved = 0;
            ulong held = 0;
            for (int i = 0; i < freeHeld; i++) {
                held = (held << _maskBits) | (uint)_full;
            }
            Assign(held, 0, stock, hand, BigInteger.One, Observe);
            for (int row = 0; row < fullWins.Length; row++) {
                BigInteger complete = Multiply(fullWins[row], freeChoices);
                _budget.Spend(1 + WorkBudget.IntegerCells(complete) + WorkBudget.IntegerCells(partialWins[row]));
                fullWins[row] = Multiply(complete + partialWins[row], _factorial[free]);
            }
            resolved = Multiply(resolved, Multiply(freeChoices, _factorial[free]));
            long cells = 9 + fullWins.Length + fullWins.Sum(WorkBudget.IntegerCells) + WorkBudget.IntegerCells(resolved);
            _budget.Spend(cells);
            if (_completions.Count < _budget.StoragePolicy.EntryLimit && _completionCells + cells <= _budget.StoragePolicy.CellLimit) {
                _completionCells += cells;
                _budget.CheckStorage(_completions.Count + 1L, _completionCells);
                _completions.Add(completionKey, (fullWins, resolved));
            }
            return (fullWins, resolved);

            void Observe(BigInteger weight) {
                _budget.Spend(n + 1L + WorkBudget.IntegerCells(resolved) + WorkBudget.IntegerCells(weight));
                resolved += weight;
                hand.CopyTo(final, 0);
                for (int step = 0; step < toRemove; step++) {
                    int selected = -1;
                    foreach (int p in _removalOrder[pendingSource]) {
                        _budget.Spend(1);
                        if (final[p] > 0) {
                            selected = p;
                            break;
                        }
                    }
                    if (selected < 0) {
                        return;
                    }
                    final[selected]--;
                }

                int count = 0;
                int uncertainCopies = 0;
                ulong key = 0;
                for (int p = 0; p < n; p++) {
                    _budget.Spend(3 + _targetSources[p].Length);
                    physicalHand[_passive[p]] = final[p];
                    key |= (ulong)final[p] << (4 * p);
                    int capacity = 0;
                    foreach (int source in _targetSources[p]) {
                        capacity += Math.Min(_sourceAllowances[source], final[source]);
                    }
                    caps[p] = capacity;
                    int low = Math.Min(capacity, Math.Max(0, stock[p] - free));
                    int high = Math.Min(capacity, Math.Min(stock[p], outside - free));
                    available[_passive[p]] = low;
                    if (low != high) {
                        uncertain[count++] = p;
                        uncertainCopies += stock[p];
                    }
                }
                ulong guaranteed = Match(key, physicalHand, available);
                Credit(guaranteed, weight, fullWins);
                if (count == 0) {
                    return;
                }
                for (int i = 0; i < count; i++) {
                    int p = uncertain[i];
                    available[_passive[p]] = Math.Min(caps[p], Math.Min(stock[p], outside - free));
                }
                _budget.Spend(count);
                ulong ambiguous = Match(key, physicalHand, available) & ~guaranteed;
                if (ambiguous != 0) {
                    // Free losses in every target that cannot change these
                    // witnesses, and in all non-targets, form one complement.
                    Loss(0, free, uncertainCopies, weight);
                }

                void Loss(int i, int left, int unassigned, BigInteger ways) {
                    _budget.Spend(1);
                    int other = outside - uncertainCopies;
                    if (i == count) {
                        ulong matches = Match(key, physicalHand, available) & ambiguous;
                        Credit(matches, Multiply(ways, _choose[other][left]), partialWins);
                        return;
                    }
                    int p = uncertain[i];
                    int rest = unassigned - stock[p];
                    for (int loss = Math.Max(0, left - other - rest); loss <= Math.Min(stock[p], left); loss++) {
                        available[_passive[p]] = Math.Min(caps[p], stock[p] - loss);
                        Loss(i + 1, left - loss, rest, Multiply(ways, _choose[stock[p]][loss]));
                    }
                }
            }
        }

        // When every continuation already failed, held and discarded slots
        // have identical meaning. Full masks may all be assigned last.
        internal BigInteger EvaluateSupport(ulong held, ulong dead, int taken) {
            held = WithoutFull(held);
            dead = WithoutFull(dead);
            var key = (held, dead, taken);
            _budget.Spend(6);
            if (_support.TryGetValue(key, out BigInteger value)) {
                return value;
            }
            int constrained = Slots(held) + Slots(dead);
            CheckScratch(taken, constrained);
            int free = taken - constrained;
            BigInteger assignments = 0;
            Assign(held, dead, (int[])_copies.Clone(), new int[_passive.Length], BigInteger.One, weight => {
                _budget.Spend(1 + WorkBudget.IntegerCells(assignments) + WorkBudget.IntegerCells(weight));
                assignments += weight;
            });
            value = Multiply(assignments, Multiply(_choose[_total - constrained][free], _factorial[free]));
            long cells = 7 + WorkBudget.IntegerCells(value);
            if (_support.Count < _budget.StoragePolicy.EntryLimit && _supportCells + cells <= _budget.StoragePolicy.CellLimit) {
                _supportCells += cells;
                _budget.CheckStorage(_support.Count + 1L, _supportCells);
                _support.Add(key, value);
            }
            return value;
        }

        private ulong WithoutFull(ulong masks) {
            ulong result = 0;
            int shift = 0;
            while (masks != 0) {
                _budget.Spend(1);
                ulong mask = masks & (uint)_slotMask;
                if (mask != (uint)_full) {
                    result |= mask << shift;
                    shift += _maskBits;
                }
                masks >>= _maskBits;
            }
            return result;
        }

        // Assign a group of equal masks by its multiset. The factor count!
        // restores the order of the distinguishable encounters in the group.
        private void Assign(ulong held, ulong dead, int[] stock, int[] hand, BigInteger weight, Action<BigInteger> visit) {
            bool retained = held != 0;
            ulong word = retained ? held : dead;
            if (word == 0) {
                visit(weight);
                return;
            }
            int mask = (int)(word & (uint)_slotMask);
            int count = 0;
            do {
                _budget.Spend(1);
                count++;
                word >>= _maskBits;
            } while ((word & (uint)_slotMask) == (uint)mask);
            Choose(mask, count, Multiply(weight, _factorial[count]));

            void Choose(int eligible, int left, BigInteger ways) {
                _budget.Spend(1);
                if (left == 0) {
                    Assign(retained ? word : 0, retained ? dead : word, stock, hand, ways, visit);
                    return;
                }
                if (eligible == 0) {
                    return;
                }
                int p = BitOperations.TrailingZeroCount((uint)eligible);
                int others = eligible & (eligible - 1);
                int available = stock[p];
                for (int n = others == 0 ? left : 0; n <= Math.Min(available, left); n++) {
                    stock[p] -= n;
                    if (retained) {
                        hand[p] += n;
                    }
                    Choose(others, left - n, Multiply(ways, _choose[available][n]));
                    if (retained) {
                        hand[p] -= n;
                    }
                    stock[p] += n;
                }
            }
        }

        private ulong Match(ulong key, int[] hand, int[] available) {
            for (int p = 0; p < _passive.Length; p++) {
                key |= (ulong)available[_passive[p]] << (28 + 4 * p);
            }
            _budget.Spend(_passive.Length + 3L);
            if (_matches.TryGetValue(key, out ulong cached)) {
                return cached;
            }
            bool[] matches = _counter.Evaluate(hand, available);
            ulong mask = 0;
            for (int combo = 0; combo < matches.Length; combo++) {
                if (matches[combo]) {
                    mask |= 1;
                    if (_rows.Count > 1) {
                        mask |= 1UL << _rows.ComboRow(combo);
                    }
                }
            }
            if (_rows.Count > 1) {
                for (int group = 0; group < _rows.GroupCount; group++) {
                    if (_rows.GroupMatches(group, matches)) {
                        mask |= 1UL << _rows.GroupRow(group);
                    }
                }
            }
            _budget.Spend(matches.Length * (_rows.GroupCount + 1L) + 2);
            if (_matches.Count < _budget.StoragePolicy.EntryLimit && 4L * (_matches.Count + 1) <= _budget.StoragePolicy.CellLimit) {
                _budget.CheckStorage(_matches.Count + 1L, 4L * (_matches.Count + 1));
                _matches.Add(key, mask);
            }
            return mask;
        }

        private void Credit(ulong mask, BigInteger weight, BigInteger[] wins) {
            while (mask != 0) {
                int row = BitOperations.TrailingZeroCount(mask);
                _budget.Spend(2 + WorkBudget.IntegerCells(wins[row]) + WorkBudget.IntegerCells(weight));
                wins[row] += weight;
                mask &= mask - 1;
            }
        }

        private void CheckScratch(int taken, int slots) {
            // Each nonempty group can introduce at most one frame per passive
            // category, plus its caller. Slot, loss and capacity counts are tiny
            // on this route, but the assignment weights may span several limbs.
            long frames = (slots + 1L) * (_passive.Length + 2L) + _passive.Length + 1;
            long limbs = WorkBudget.IntegerCells(_factorial[_total]) + 1;
            _budget.CheckStorage(frames, frames * (limbs + 8) + 2L * _cardRows + 7L * _passive.Length
                + (2L * _rows.Count + 6) * (limbs + 1));
            _budget.Spend(taken + _rows.Count + 2L * _cardRows + 7L * _passive.Length);
        }

        private BigInteger Multiply(BigInteger left, BigInteger right) {
            _budget.Spend(1);
            if (left.IsOne || right.IsOne) {
                return left.IsOne ? right : left;
            }
            _budget.Spend((WorkBudget.IntegerCells(left) + 1) * (WorkBudget.IntegerCells(right) + 1));
            return left * right;
        }

        private int Slots(ulong word) => (64 - BitOperations.LeadingZeroCount(word) + _maskBits - 1) / _maskBits;
    }
}
