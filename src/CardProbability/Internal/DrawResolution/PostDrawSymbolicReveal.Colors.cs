using System.Numerics;
using CardProbability.Internal.Model;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    // Canonical removal order normally separates even interleaved equivalent
    // rows. Two ordinary labels can cross that boundary when neither can ever
    // be removed or distinguished by a reveal, search, or terminal predicate.
    private List<Card> PoolUnobservedRevealRows(List<Card> cards, List<Combo> combos) {
        int[] blank = Enumerable.Range(0, cards.Count).Where(row => {
            Card card = cards[row];
            if (card.HasEffect || card.Copies == 0 || card.RevealIdentity is not null) {
                return false;
            }
            foreach (Combo combo in combos) {
                foreach (ComboCategory requirement in combo.AllCategories) {
                    budget.Spend(requirement.Selector.EvaluationSize * (card.Categories.Count + 1L));
                    if (requirement.Selector.Matches(card)) {
                        return false;
                    }
                }
                foreach (ComboCard requirement in combo.AllCards) {
                    budget.Spend(1);
                    if (requirement.CardId == card.Id) {
                        return false;
                    }
                }
            }
            foreach (Card source in cards) {
                budget.Spend((source.PostDrawSelector.EvaluationSize + (source.Search?.Selector.EvaluationSize ?? 0) + 1L)
                    * (card.Categories.Count + 1L));
                if (source.PostDrawDiscardCount != 0 && source.PostDrawSelector.Matches(card)
                    || source.Search is not null && source.Search.Matches(card)) {
                    return false;
                }
            }
            return true;
        }).ToArray();
        if (blank.Length < 2) {
            return cards;
        }
        int first = blank[0];
        cards[first] = cards[first].WithCopies(blank.Sum(row => cards[row].Copies));
        for (int i = blank.Length - 1; i > 0; i--) {
            cards.RemoveAt(blank[i]);
        }
        return cards;
    }

    private sealed partial class SymbolicResolver {
        // Before the first comparison of their canonical keep order, these
        // source names are exchangeable. The queue still records equality of
        // names and the number of physical copies remaining for each name.
        private readonly int[] _colors;
        private readonly int _colorMask;
        private readonly int _colorScale;

        private int[] FindColors(List<Card> cards) {
            int[] tokens = Enumerable.Range(1, _active.Length).Where(t => _reveal[t]
                && _identity.Count(identity => identity == _identity[t]) == 1).ToArray();
            foreach (IGrouping<(int Copies, int Rank), int> candidates in tokens.GroupBy(t => (cards[_active[t - 1]].Copies, _priority[t]))
                .OrderByDescending(group => group.Count())) {
                int[] group = candidates.ToArray();
                if (group.Length < 3) {
                    break;
                }
                bool compatible = true;
                foreach (int source in Enumerable.Range(1, _active.Length).Where(t => _discard[t] > 0)) {
                    int[] order = _removal[source];
                    int[] positions = group.Select(token => Array.IndexOf(order, token)).Where(pos => pos >= 0).ToArray();
                    if (positions.Length > 0 && (positions.Length != group.Length || positions.Max() - positions.Min() + 1 != group.Length)) {
                        compatible = false;
                        break;
                    }
                }
                if (compatible) {
                    return group;
                }
            }
            return [];
        }

        // Every representative has the same number of unseen copies and no
        // copy in the FIFO. Selecting one produces the same quotient state.
        private int ColorMultiplicity(Position state, int token, int available) {
            if ((_colorMask & (1 << token)) == 0) {
                return 1;
            }
            int queued = 0;
            for (uint rest = state.Queue; rest != 0; rest >>= QueueBits) {
                queued |= 1 << (int)(rest & 7);
            }
            if ((queued & (1 << token)) != 0) {
                return 1;
            }
            int count = 0;
            foreach (int color in _colors) {
                if ((queued & (1 << color)) == 0 && ((state.Deck >> ((color - 1) * QueueBits)) & 7) == available) {
                    if (color < token) {
                        return 0;
                    }
                    count++;
                }
            }
            return count;
        }

        private Position NormalizeColors(Position state) {
            Span<int> map = stackalloc int[8];
            map.Clear();
            int count = 0;
            uint queue = 0;
            uint rest = state.Queue;
            for (int shift = 0; rest != 0; shift += QueueBits, rest >>= QueueBits) {
                int token = (int)(rest & 7);
                if ((_colorMask & (1 << token)) != 0) {
                    if (map[token] == 0) {
                        map[token] = _colors[count++];
                    }
                    token = map[token];
                }
                queue |= (uint)token << shift;
            }
            int deck = state.Deck;
            int maximumCopies = (_initialDeck >> (QueueBits * (_colors[0] - 1))) & 7;
            for (int available = maximumCopies; available >= 0; available--) {
                foreach (int color in _colors) {
                    int shift = (color - 1) * QueueBits;
                    int copies = (state.Deck >> shift) & 7;
                    if (copies != available) {
                        continue;
                    }
                    if (map[color] == 0) {
                        map[color] = _colors[count++];
                    }
                    int to = (map[color] - 1) * QueueBits;
                    deck = (deck & ~(7 << to)) | (copies << to);
                }
            }
            _budget.Spend(_colors.Length * (maximumCopies + 1) + Length(queue, QueueBits) + 1);
            return state with { Deck = deck, Queue = queue };
        }

        private bool RemoveColor(Position state, int pending, int remove, BigInteger weight) {
            Span<int> last = stackalloc int[8];
            last.Fill(-1);
            int count = 0;
            int i = 0;
            for (uint rest = state.Queue; rest != 0; rest >>= QueueBits, i++) {
                int token = (int)(rest & 7);
                if ((_colorMask & (1 << token)) != 0) {
                    if (last[token] < 0) {
                        count++;
                    }
                    last[token] = i;
                }
            }
            _budget.Spend(i + _colors.Length);
            if (count == 0) {
                return false;
            }
            if (count > 1 && state.Compared) {
                // A second comparison could observe the first comparison's
                // relative rank. Leave this continuation in unresolved mass.
                return true;
            }
            // An unused uniform assignment of names makes each distinct held
            // name equally likely to have the largest canonical keep rank.
            // The group factorial divides every unobserved prefix weight.
            BigInteger unit = count == 1 ? weight : weight / count;
            foreach (int color in _colors) {
                if (last[color] >= 0) {
                    Position branch = state with { Queue = (uint)Delete(state.Queue, last[color], QueueBits), Compared = state.Compared || count > 1 };
                    Complete(branch, pending, remove - 1, unit);
                }
            }
            return true;
        }
    }
}
