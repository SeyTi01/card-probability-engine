using System.Numerics;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.DrawResolution;

internal sealed partial class PostDrawCalculation {
    private sealed partial class SymbolicResolver {
        private int[,] _prefixScore = null!;
        private int[] _maskScore = null!;
        private int MaximumObservedActives => _colorMask != 0 ? 8 : _handSize >= 6 || _maskBits >= 6 ? 6 : 5;

        // Approximate log2 with sixteen integer subdivisions per binary
        // exponent. This ranking only selects abstract prefixes to classify.
        // The interval itself uses exact integer counts independently of all
        // rank estimates and all discarded prefix/label multiplicities.
        private static int Score(BigInteger value) {
            int exponent = (int)value.GetBitLength() - 1;
            BigInteger leading = exponent >= 4 ? value >> (exponent - 4) : value << (4 - exponent);
            return (exponent << 4) + (int)leading - 16;
        }

        private void PreparePriorities(int[] copies, BigInteger[] factorial) {
            int maximum = Math.Min(_activeCopies, MaximumObservedActives);
            _prefixScore = new int[maximum + 1, _passiveCopies + 1];
            _maskScore = new int[_full + 1];
            _owner.CheckStorage(_prefixScore.Length + _maskScore.Length, _prefixScore.Length + _maskScore.Length, "symbolic reveal ranking");
            int denominatorScore = Score(factorial[^1] * _randomChoices);
            BigInteger falling = BigInteger.One;
            for (int passive = 0; passive <= _passiveCopies; passive++) {
                for (int active = 0; active <= maximum; active++) {
                    int remaining = _passiveCopies + _activeCopies - passive - active;
                    _budget.Spend(4 + WorkBudget.IntegerCells(falling) + WorkBudget.IntegerCells(factorial[remaining]));
                    _prefixScore[active, passive] = Score(falling) + Score(factorial[remaining]) - denominatorScore;
                }
                falling *= _passiveCopies - passive;
            }
            for (int mask = 1; mask <= _full; mask++) {
                int population = 0;
                for (int i = 0; i < copies.Length; i++) {
                    if ((mask & (1 << i)) != 0) {
                        population += copies[i];
                    }
                }
                _maskScore[mask] = Score(population) - Score(_passiveCopies);
            }
            _budget.Spend(_maskScore.Length * (copies.Length + 3L));
        }

        private bool Negligible(Position state, BigInteger mass) {
            if (_activeCopies <= MaximumObservedActives) {
                return false;
            }
            int observed = _activeCopies - DeckCount(state.Deck);
            int score = _prefixScore[observed, state.Taken] + Score(mass);
            int slots = 0;
            for (ulong held = state.Held; held > 0; held >>= _maskBits) {
                int mask = (int)(held & _slotMask);
                if (mask == _full) {
                    // Full domains sort last and contribute zero to the rank.
                    break;
                }
                score += _maskScore[mask];
                slots++;
            }
            for (ulong dead = state.Dead; dead > 0; dead >>= _maskBits) {
                score += _maskScore[dead & _slotMask];
                slots++;
            }
            _budget.Spend(4 + slots + WorkBudget.IntegerCells(mass));
            // Approximately one ten-millionth of the unconditioned measure.
            return score < -372;
        }
    }
}
