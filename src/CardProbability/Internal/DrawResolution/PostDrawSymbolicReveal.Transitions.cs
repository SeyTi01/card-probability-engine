using System.Numerics;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    private sealed partial class SymbolicResolver {
        // Inside a batch, the active word determines the pending FIFO. All
        // placements of its passive slots induce identical observations and
        // contribute the binomial interleaving factor.
        private void Draw(Position state, int draws, int pending, int remove, BigInteger weight, int used) {
            int free = draws - used;
            _budget.Spend(4 + free);
            if (state.Taken + free <= _passiveCopies) {
                ulong held = state.Held;
                for (int i = 0; i < free; i++) {
                    // Full is the greatest mask, so appending at the high end
                    // preserves canonical sorted order.
                    held |= (ulong)_full << (_maskBits * Length(held, _maskBits));
                }
                Position next = state with { Held = held, Taken = state.Taken + free };
                Complete(next, pending, remove, weight * _choose[draws, free]);
            }
            if (free == 0) {
                return;
            }
            int end = Length(state.Queue, QueueBits);
            int observed = _activeCopies - DeckCount(state.Deck);
            if (!_exhaustive && observed >= MaximumObservedActives) {
                return;
            }
            for (int index = 0; index < _active.Length; index++) {
                int shift = QueueBits * index;
                int available = (state.Deck >> shift) & 7;
                _budget.Spend(1);
                int multiplicity = _colorMask == 0 || available == 0 ? 1 : ColorMultiplicity(state, index + 1, available);
                if (available > 0 && multiplicity > 0) {
                    uint queue = state.Queue | ((uint)(index + 1) << (QueueBits * end));
                    Position next = state with { Deck = state.Deck - (1 << shift), Queue = queue };
                    Draw(next, draws, pending, remove, weight * (available * multiplicity), used + 1);
                }
            }
        }

        private void Complete(Position state, int pending, int remove, BigInteger weight) {
            if (state.Queue == 0) {
                Record(state, pending > 0 && remove > 0 ? _active[pending - 1] : -1, remove, weight);
                return;
            }
            if (remove == 0) {
                if (!_exhaustive && Negligible(state, weight)) {
                    return;
                }
                int layer = DeckCount(state.Deck) + Length(state.Queue, QueueBits);
                // Omitted frontier entries are unclassified. Their entire
                // mass remains in Denominator - Resolved at finalization.
                if (_colorMask != 0) {
                    state = NormalizeColors(state);
                }
                if (!Store(_layers[layer], state, weight, ref _layerCells[layer]) && _exhaustive) {
                    // Exact streams a full bucket, then accepts later terms
                    // into its emptied map. Tiny policies may fit no entry.
                    Drain(layer);
                    if (!Store(_layers[layer], state, weight, ref _layerCells[layer])) {
                        Activate(state, weight);
                    }
                }
                return;
            }
            // Test candidates in descending removal order. Seeing a passive
            // label is necessary only after all worse active rows were absent.
            bool checkedColors = false;
            foreach (int token in _removal[pending]) {
                _budget.Spend(1);
                if (token > 0 && (_colorMask & (1 << token)) != 0) {
                    if (!checkedColors && RemoveColor(state, pending, remove, weight)) {
                        return;
                    }
                    checkedColors = true;
                } else if (token > 0) {
                    for (int index = Length(state.Queue, QueueBits) - 1; index >= 0; index--) {
                        _budget.Spend(1);
                        if (Token(state.Queue, index, QueueBits) == token) {
                            Complete(state with { Queue = (uint)Delete(state.Queue, index, QueueBits) }, pending, remove - 1, weight);
                            return;
                        }
                    }
                } else if (!Partition(state, -token - 1, pending, remove, weight, out state)) {
                    return;
                }
            }
            Record(state, Failed, 0, weight);
        }

        // Each branch corresponds to the first slot with this label. Earlier
        // slots acquire an exclusion, the selected slot becomes one fixed
        // lost label, and later slot domains stay untouched. Identity-free
        // slots remain exchangeable within equal domains. A known singleton
        // can always be chosen directly, since ties remove identical copies.
        private bool Partition(Position state, int category, int pending, int remove, BigInteger weight, out Position absent) {
            absent = state;
            int bit = 1 << category;
            int hand = Length(state.Held, _maskBits);
            ulong dead = AddMask(state.Dead, bit);
            _budget.Spend(2L * hand + 2);
            for (int i = 0; i < hand; i++) {
                if (Token(state.Held, i, _maskBits) == bit) {
                    Complete(state with { Held = Delete(state.Held, i, _maskBits), Dead = dead }, pending, remove - 1, weight);
                    return false;
                }
            }
            ulong prefix = 0;
            int prefixWidth = 0;
            for (ulong remaining = state.Held; remaining > 0;) {
                int mask = (int)(remaining & _slotMask);
                remaining >>= _maskBits;
                if ((mask & bit) != 0) {
                    // Exclusions only decrease prior masks. The untouched
                    // suffix therefore follows the sorted prefix directly.
                    ulong held = prefix | (remaining << prefixWidth);
                    _budget.Spend(2);
                    Complete(state with { Held = held, Dead = dead }, pending, remove - 1, weight);
                    mask &= ~bit;
                }
                prefix = AddMask(prefix, mask);
                prefixWidth += _maskBits;
            }
            absent = state with { Held = prefix };
            return true;
        }

        private void Activate(Position state, BigInteger weight) {
            int count = Length(state.Queue, QueueBits);
            int selected = 0;
            for (int i = 1; i < count; i++) {
                if (_priority[Token(state.Queue, i, QueueBits)] < _priority[Token(state.Queue, selected, QueueBits)]) {
                    selected = i;
                }
            }
            _budget.Spend(count + 2);
            int source = Token(state.Queue, selected, QueueBits);
            uint afterSource = (uint)Delete(state.Queue, selected, QueueBits);
            if (!_reveal[source]) {
                Draw(state with { Queue = afterSource }, _batch[source], source, _discard[source], weight, 0);
                return;
            }
            int passive = Length(state.Held, _maskBits);
            int hand = count + passive;
            BigInteger unit = weight / hand;
            _budget.Spend(count + 2L * passive + WorkBudget.IntegerCells(weight));
            for (int i = 0; i < passive;) {
                int mask = Token(state.Held, i, _maskBits);
                int next = i + 1;
                while (next < passive && Token(state.Held, next, _maskBits) == mask) {
                    next++;
                }
                // Unrestricted lost identities remain implicit. The total
                // number of encountered passive copies records their slots.
                ulong dead = mask == _full ? state.Dead : AddMask(state.Dead, mask);
                Position discarded = state with { Queue = afterSource, Held = Delete(state.Held, i, _maskBits), Dead = dead };
                Draw(discarded, 1, 0, 0, unit * (next - i), 0);
                i = next;
            }
            for (int i = 0; i < count; i++) {
                int victim = Token(state.Queue, i, QueueBits);
                uint queue = (uint)Delete(state.Queue, i, QueueBits);
                if (_identity[victim] == _identity[source]) {
                    Complete(state with { Queue = queue }, 0, 0, unit);
                } else {
                    queue = (uint)Delete(queue, selected - (i < selected ? 1 : 0), QueueBits);
                    Draw(state with { Queue = queue }, 1, 0, 0, unit, 0);
                }
            }
        }
    }
}
