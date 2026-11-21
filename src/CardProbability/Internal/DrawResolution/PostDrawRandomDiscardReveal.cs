using System.Numerics;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    private sealed partial class OrderedResolver {
        private readonly bool _hasReveal = cards.Any(card => card.Copies > 0 && card.IsRandomDiscardReveal);
        private BigInteger? _randomScale;

        // Only settled hands can activate a reveal. Ordinary activations add at
        // most their positive net hand gain; every reveal activation shrinks the
        // hand by one and removes a copy of an active reveal identity. With H the
        // opening hand plus all possible positive gains, and D those identity
        // copies, LCM(1..min(N,H))^min(D,H) covers every random denominator,
        // independently of visible deck depth. Ordinary requests retain N! units.
        private BigInteger RandomScale(int copies, int handSize) {
            if (!_hasReveal) {
                return BigInteger.One;
            }
            if (_randomScale is { } existing) {
                return existing;
            }
            HashSet<string> sourceIdentities = cards.Where(card => card.Copies > 0 && card.IsRandomDiscardReveal)
                .Select(card => card.RevealIdentity!).ToHashSet(StringComparer.Ordinal);
            int maximumActivations = cards.Where(card => card.RevealIdentity is not null && sourceIdentities.Contains(card.RevealIdentity))
                .Sum(card => card.Copies);
            long maximumHand = handSize;
            foreach (Card card in cards) {
                if (card.HasDrawEffect && !card.IsRandomDiscardReveal) {
                    long gain = Math.Max(0L, (long)card.DrawBatchSize - 1 - card.PreDrawRemovalCount - card.PostDrawDiscardCount);
                    maximumHand += gain * Math.Min(card.Copies, card.MaximumActivations);
                }
            }
            int bound = (int)Math.Min(copies, maximumHand);
            maximumActivations = (int)Math.Min(maximumActivations, maximumHand);
            budget.Spend(cards.Count * 3L);
            BigInteger lcm = BigInteger.One;
            for (int divisor = 2; divisor <= bound; divisor++) {
                budget.Spend(WorkBudget.IntegerCells(lcm) + 1);
                lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, divisor) * divisor;
                owner.CheckStorage(1, WorkBudget.IntegerCells(lcm), "random discard unit");
            }
            BigInteger scale = BigInteger.One;
            for (int activation = 0; activation < maximumActivations; activation++) {
                budget.Spend(WorkBudget.IntegerCells(scale) * (WorkBudget.IntegerCells(lcm) + 1));
                scale *= lcm;
                owner.CheckStorage(1, WorkBudget.IntegerCells(scale), "random discard unit");
            }
            _randomScale = scale;
            return scale;
        }

        internal BigInteger Denominator(BigInteger[] factorial, int handSize) {
            BigInteger scale = RandomScale(factorial.Length - 1, handSize);
            if (scale.IsOne) {
                return factorial[^1];
            }
            budget.Spend(WorkBudget.IntegerCells(scale) * (WorkBudget.IntegerCells(factorial[^1]) + 1));
            BigInteger denominator = factorial[^1] * scale;
            owner.CheckStorage(1, WorkBudget.IntegerCells(denominator), "random discard denominator");
            return denominator;
        }

        private void SettleReveal(
            int[] values,
            int poolRemaining,
            BigInteger weight,
            int depth,
            long parentCells,
            Action<int[], BigInteger, int, long> settled
        ) {
            long cells = parentCells + values.Length + WorkBudget.IntegerCells(weight) + 4;
            owner.CheckStorage(depth + 1L, cells, "random discard stack");
            int remaining = poolRemaining - (layout.HasHiddenCount ? values[layout.HiddenCount] : 0);
            while (true) {
                budget.Spend(1);
                if (values[layout.DrawsLeft] > remaining) {
                    throw new InvalidOperationException("An activated draw batch exceeds the remaining deck size.");
                }
                if (values[layout.DrawsLeft] > 0) {
                    settled(values, weight, depth, cells);
                    return;
                }
                if (CompleteBatch(ref values, ref remaining) == ResolutionOutcome.RemovalUnavailable) {
                    return;
                }
                int selected = SelectCandidate(ref values, remaining);
                if (selected < 0) {
                    if (values.Length > layout.QueueStart) {
                        budget.Spend(values.Length);
                        values = values.AsSpan(0, layout.QueueStart).ToArray();
                    }
                    settled(values, weight, depth, cells);
                    return;
                }
                if (!cards[values[selected]].IsRandomDiscardReveal) {
                    Activate(ref values, selected, ref remaining);
                    continue;
                }
                BranchReveal(values, selected, poolRemaining, weight, depth, cells, settled);
                return;
            }
        }

        private void BranchReveal(
            int[] values,
            int selected,
            int remaining,
            BigInteger weight,
            int depth,
            long parentCells,
            Action<int[], BigInteger, int, long> settled
        ) {
            int source = values[selected];
            int handSize = 0;
            for (int row = 0; row < cards.Count; row++) {
                handSize += values[layout.HandStart + row];
            }
            budget.Spend(cards.Count + WorkBudget.IntegerCells(weight));
            BigInteger unit = BigInteger.DivRem(weight, handSize, out BigInteger remainder);
            if (!remainder.IsZero) {
                throw new InvalidOperationException("Random discard branch mass must remain integral.");
            }
            for (int row = 0; row < cards.Count; row++) {
                int held = values[layout.HandStart + row];
                budget.Spend(1);
                if (held == 0) {
                    continue;
                }
                int individual = 0;
                // Draw entries carry one physical FIFO token per
                // eligible copy, including once-per-turn entries. Removing different occurrences may change the
                // next activation, even when their card identity is identical.
                // Equal adjacent tokens alone are interchangeable: deleting any
                // token in their run leaves exactly the same encoded queue.
                if (cards[row].HasDrawEffect) {
                    for (int queue = layout.QueueStart; queue < values.Length; queue++) {
                        budget.Spend(1);
                        if (values[queue] == row) {
                            int start = queue;
                            while (queue + 1 < values.Length && values[queue + 1] == row) {
                                budget.Spend(1);
                                queue++;
                            }
                            int multiplicity = queue - start + 1;
                            Discard(row, start, -1, multiplicity);
                            individual += multiplicity;
                        }
                    }
                }
                if (layout.HasSearchQueue && cards[row].Search is not null) {
                    for (int queue = layout.SearchQueueStart; queue < layout.QueueStart; queue++) {
                        budget.Spend(1);
                        if (values[queue] == row + 1) {
                            int start = queue;
                            while (queue + 1 < layout.QueueStart && values[queue + 1] == row + 1) {
                                budget.Spend(1);
                                queue++;
                            }
                            int multiplicity = queue - start + 1;
                            Discard(row, -1, start, multiplicity);
                            individual += multiplicity;
                        }
                    }
                }
                if (held > individual) {
                    Discard(row, -1, -1, held - individual);
                }
            }

            void Discard(int row, int drawQueue, int searchQueue, int multiplicity) {
                budget.Spend(values.Length + WorkBudget.IntegerCells(unit) + 1L);
                int[] branch = (int[])values.Clone();
                branch[layout.HandStart + row]--;
                int revealQueue = selected;
                if (drawQueue >= 0) {
                    branch = RemoveCandidate(branch, drawQueue);
                    if (drawQueue < revealQueue) {
                        revealQueue--;
                    }
                }
                if (searchQueue >= 0) {
                    Array.Copy(branch, searchQueue + 1, branch, searchQueue, layout.QueueStart - searchQueue - 1);
                    branch[layout.QueueStart - 1] = 0;
                }
                if (layout.HasActivationState) {
                    branch[layout.ActivationState] = 1;
                }
                if (cards[row].RevealIdentity != cards[source].RevealIdentity) {
                    // Different identity: the revealed physical source leaves
                    // the hand and its own queue slot; only then draw one.
                    branch[layout.HandStart + source]--;
                    branch = RemoveCandidate(branch, revealQueue);
                    branch[layout.DrawsLeft] = 1;
                    if (layout.TracksEffectDraw) {
                        branch[layout.EffectDrawOccurred] = 1;
                    }
                }
                // Same identity removes only the random copy. The unchanged
                // FIFO tokens preserve every surviving reveal opportunity.
                SettleReveal(branch, remaining, unit * multiplicity, depth + 1, parentCells + WorkBudget.IntegerCells(unit) + 4, settled);
            }
        }
    }
}
