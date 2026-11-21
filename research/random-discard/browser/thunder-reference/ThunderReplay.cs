using System.Numerics;
using System.Runtime.InteropServices;

internal sealed class ThunderReplay(int handSize) {
    private const int Allure = 5;
    private const int AllureToken = Allure + 1;
    private const int FirstPassive = 6;
    private const int Colors = 5;
    private const int ColorOrders = 120;
    private static readonly int[] DeckShift = [0, 2, 4, 6, 8, 10, 12, 14, 17];
    private static readonly int[] DeckMask = [3, 3, 3, 3, 3, 3, 3, 7, 31];
    private static readonly int[] HandShift = [0, 2, 5];
    private static readonly int[] InitialCopies = [2, 2, 2, 2, 2, 3, 2, 5, 20];

    // The three passive hand lanes count unprotected DARK, DARK Thunder, and
    // all non-DARK passive cards. The three-bit queue tokens are one-based.
    private readonly record struct Position(
        int Deck, int Hand, uint Queue, int DrawsLeft, bool RemoveDark, int Reveals,
        uint Order = 0, bool OrderCounted = false
    );
    private Dictionary<Position, UInt128> _next = [];
    private readonly List<uint> _orders = [];
    private readonly Dictionary<uint, int> _orderCounts = [];
    private readonly UInt128[,,,,] _terminal = new UInt128[20, 7, 7, 2, 2];
    private int _depth;

    internal (string Union, string Single) Calculate() {
        EnumerateOrders(0, 0);
        int deck = 0;
        for (int card = 0; card < InitialCopies.Length; card++) {
            deck |= InitialCopies[card] << DeckShift[card];
        }
        _next.Add(new(deck, 0, 0, handSize, false, 0), 1);
        for (_depth = 1; _next.Count > 0; _depth++) {
            Dictionary<Position, UInt128> frontier = _next;
            _next = [];
            foreach ((Position position, UInt128 multiplicity) in frontier) {
                Draw(position, multiplicity);
            }
        }
        return CountGoals();
    }

    private static int Stock(int deck, int card) => (deck >> DeckShift[card]) & DeckMask[card];
    private static int Held(int hand, int kind) => (hand >> HandShift[kind]) & (kind == 0 ? 3 : 7);
    private static int Slots(uint queue) => queue == 0 ? 0 : BitOperations.Log2(queue) / 3 + 1;
    private static int Token(uint queue, int slot) => (int)(queue >> (3 * slot) & 7);

    private static uint Delete(uint queue, int slot) {
        uint prefix = (1u << (3 * slot)) - 1;
        return (queue & prefix) | ((queue >> 3) & ~prefix);
    }

    private void Draw(Position position, UInt128 weight) {
        int length = Slots(position.Queue);
        int queued = 0;
        for (int slot = 0; slot < length; slot++) {
            queued |= 1 << Token(position.Queue, slot);
        }
        for (int card = 0; card < InitialCopies.Length; card++) {
            int stock = Stock(position.Deck, card);
            if (stock == 0) {
                continue;
            }
            int copies = stock;
            if (card < Colors && position.Order == 0 && (queued & (1 << (card + 1))) == 0) {
                // Before a rank observation, absent colors with the same stock
                // induce the same distribution. Draw one named representative.
                copies *= EquivalentColors(position.Deck, queued, card, stock);
            }
            if (copies == 0) {
                continue;
            }
            Position drawn = position with {
                Deck = position.Deck - (1 << DeckShift[card]),
                DrawsLeft = position.DrawsLeft - 1
            };
            if (card < FirstPassive) {
                drawn = drawn with { Queue = drawn.Queue | ((uint)(card + 1) << (3 * length)) };
            } else {
                drawn = drawn with { Hand = drawn.Hand + (1 << HandShift[card - FirstPassive]) };
            }
            UInt128 branch = weight * (uint)copies;
            if (drawn.DrawsLeft > 0) {
                Enqueue(drawn, branch);
            } else if (drawn.RemoveDark) {
                RemoveMaterial(drawn, branch);
            } else {
                Activate(drawn, branch);
            }
        }
    }

    private static int EquivalentColors(int deck, int queued, int card, int stock) {
        int count = 0;
        for (int other = 0; other < Colors; other++) {
            if (Stock(deck, other) == stock && (queued & (1 << (other + 1))) == 0) {
                if (other < card) {
                    return 0;
                }
                count++;
            }
        }
        return count;
    }

    private void Activate(Position position, UInt128 weight) {
        int hand = handSize - position.Reveals;
        if (hand < 2 || hand == 2 && position.Queue != 0 && Token(position.Queue, 0) != AllureToken) {
            // Every reveal costs one net card. Neither goal can subsequently
            // fill two distinct positive slots. Count all its continuations.
            Record(position, weight, false);
            return;
        }
        if (position.Queue == 0) {
            Record(position, weight, true);
            return;
        }
        int source = Token(position.Queue, 0);
        uint afterSource = Delete(position.Queue, 0);
        if (source == AllureToken) {
            Enqueue(position with { Queue = afterSource, DrawsLeft = 2, RemoveDark = true }, weight);
            return;
        }
        position = position with { Reveals = position.Reveals + 1 };
        for (int kind = 0; kind < 3; kind++) {
            int copies = Held(position.Hand, kind);
            if (copies > 0) {
                Enqueue(position with { Queue = afterSource, DrawsLeft = 1, Hand = position.Hand - (1 << HandShift[kind]) },
                    weight * (uint)copies);
            }
        }
        for (int victim = 0; victim < Slots(position.Queue); victim++) {
            uint surviving = Delete(position.Queue, victim);
            if (Token(position.Queue, victim) == source) {
                Activate(position with { Queue = surviving }, weight);
            } else {
                Enqueue(position with { Queue = Delete(surviving, 0), DrawsLeft = 1 }, weight);
            }
        }
    }

    private void RemoveMaterial(Position position, UInt128 weight) {
        position = position with { RemoveDark = false };
        if (Held(position.Hand, 0) > 0) {
            Activate(position with { Hand = position.Hand - 1 }, weight);
            return;
        }
        uint present = 0;
        Span<int> last = stackalloc int[Colors];
        for (int slot = 0; slot < Slots(position.Queue); slot++) {
            int token = Token(position.Queue, slot);
            if (token <= Colors) {
                present |= 1u << (token - 1);
                last[token - 1] = slot;
            }
        }
        if (present != 0) {
            for (uint candidates = present; candidates != 0; candidates &= candidates - 1) {
                int color = BitOperations.TrailingZeroCount(candidates);
                uint others = present ^ (1u << color);
                uint constraints = position.Order | (others << (Colors * color));
                if (CompatibleOrders(constraints) > 0) {
                    Activate(position with { Queue = Delete(position.Queue, last[color]), Order = constraints }, weight);
                }
            }
        } else if (Held(position.Hand, 1) > 0) {
            Activate(position with { Hand = position.Hand - (1 << HandShift[1]) }, weight);
        } else {
            Record(position, weight, false);
        }
    }

    private void Enqueue(Position position, UInt128 weight) {
        if (handSize - position.Reveals < 2) {
            Record(position, weight, false);
            return;
        }
        // Canonically rename colors by first queue occurrence, then by their
        // remaining stock. Apply the same bijection to observed comparisons.
        Span<int> renamed = stackalloc int[Colors + 1];
        renamed.Clear();
        int named = 0;
        uint queue = 0;
        bool canCompare = Stock(position.Deck, Allure) > 0 || position.RemoveDark;
        for (int slot = 0; slot < Slots(position.Queue); slot++) {
            int token = Token(position.Queue, slot);
            if (token <= Colors) {
                if (renamed[token] == 0) {
                    renamed[token] = ++named;
                }
                token = renamed[token];
            } else {
                canCompare = true;
            }
            queue |= (uint)token << (3 * slot);
        }
        int deck = position.Deck & ~1023;
        for (int copies = 2; copies >= 0; copies--) {
            for (int color = 0; color < Colors; color++) {
                if (Stock(position.Deck, color) != copies) {
                    continue;
                }
                if (renamed[color + 1] == 0) {
                    renamed[color + 1] = ++named;
                }
                deck |= copies << (2 * (renamed[color + 1] - 1));
            }
        }
        uint order = 0;
        if (!canCompare && !position.OrderCounted) {
            weight *= (uint)CompatibleOrders(position.Order);
            position = position with { OrderCounted = true };
        } else {
            for (uint relation = position.Order; relation != 0; relation &= relation - 1) {
                int edge = BitOperations.TrailingZeroCount(relation);
                int higher = renamed[edge / Colors + 1] - 1;
                int lower = renamed[edge % Colors + 1] - 1;
                order |= 1u << (higher * Colors + lower);
            }
        }
        position = position with { Deck = deck, Queue = queue, Order = order };
        ref UInt128 total = ref CollectionsMarshal.GetValueRefOrAddDefault(_next, position, out _);
        total += weight;
    }

    private void EnumerateOrders(int placed, uint constraints) {
        if (placed == (1 << Colors) - 1) {
            _orders.Add(constraints);
            return;
        }
        for (int color = 0; color < Colors; color++) {
            if ((placed & (1 << color)) == 0) {
                EnumerateOrders(placed | (1 << color), constraints | ((uint)placed << (Colors * color)));
            }
        }
    }

    private int CompatibleOrders(uint constraints) {
        if (!_orderCounts.TryGetValue(constraints, out int count)) {
            count = _orders.Count(order => (order & constraints) == constraints);
            _orderCounts.Add(constraints, count);
        }
        return count;
    }

    private void Record(Position position, UInt128 weight, bool qualifies) {
        int nonDark = qualifies ? Held(position.Hand, 2) : 0;
        int darkThunder = qualifies && Held(position.Hand, 1) > 0 ? 1 : 0;
        int orders = position.OrderCounted ? 1 : CompatibleOrders(position.Order);
        _terminal[_depth, position.Reveals, nonDark, darkThunder, qualifies ? 1 : 0] += weight * (uint)orders;
    }

    private (string Union, string Single) CountGoals() {
        // Three Allures can each draw two. At most H reveals can draw once.
        int maximumDraws = handSize + 6 + handSize;
        BigInteger denominator = Falling(40, maximumDraws) * Falling(handSize, handSize) * ColorOrders;
        BigInteger classified = 0;
        BigInteger[] wins = [0, 0];
        BigInteger[] units = [1, 1];
        for (int depth = handSize; depth <= maximumDraws; depth++) {
            for (int reveals = 0; reveals <= handSize; reveals++) {
                BigInteger physical = denominator / (Falling(40, depth) * Falling(handSize, reveals) * ColorOrders);
                for (int hand = 0; hand <= handSize; hand++) {
                    for (int dark = 0; dark <= 1; dark++) {
                        classified += (BigInteger)_terminal[depth, reveals, hand, dark, 0] * physical;
                        BigInteger weight = (BigInteger)_terminal[depth, reveals, hand, dark, 1] * physical;
                        if (weight.IsZero) {
                            continue;
                        }
                        classified += weight;
                        Credit(0, 6, hand, dark != 0, weight);
                        Credit(1, 3, hand, dark != 0, weight);
                    }
                }
            }
        }
        if (classified != denominator) {
            throw new InvalidOperationException("Physical histories do not sum to exactly one.");
        }
        return (Fraction(0), Fraction(1));

        void Credit(int row, int desired, int hand, bool hasDarkThunder, BigInteger weight) {
            BigInteger choices = Choose(20, hand);
            BigInteger favorable = choices - Choose(20 - desired, hand);
            if (!hasDarkThunder) {
                favorable -= Choose(18, hand) - Choose(18 - desired, hand);
            }
            BigInteger common = choices * units[row] / BigInteger.GreatestCommonDivisor(choices, units[row]);
            wins[row] = wins[row] * (common / units[row]) + weight * favorable * (common / choices);
            units[row] = common;
        }

        string Fraction(int row) {
            BigInteger divisor = denominator * units[row];
            BigInteger gcd = BigInteger.GreatestCommonDivisor(divisor, wins[row]);
            return $"{wins[row] / gcd}/{divisor / gcd}";
        }
    }

    private static BigInteger Falling(int size, int count) {
        BigInteger ways = BigInteger.One;
        for (int i = 0; i < count; i++) {
            ways *= size - i;
        }
        return ways;
    }

    private static BigInteger Choose(int size, int count) => Falling(size, count) / Falling(count, count);
}
