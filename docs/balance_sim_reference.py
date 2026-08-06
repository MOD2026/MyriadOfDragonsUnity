# -*- coding: utf-8 -*-
"""Faithful Python replica of the shipped combat maths, to answer empirically:
how do matches actually end - by knockout, or on the tick cap?

Mirrors LaneBattleResolver / BattleController / Card.ComputeStats as implemented.
"""
import random
from collections import Counter

# --- Card.cs RarityTable: rarity -> (cost, atkMin, atkMax, hpMin, hpMax) ---
RARITY = {
    1: (1, 1, 2, 1, 2),
    2: (2, 2, 3, 2, 3),
    3: (3, 3, 4, 3, 4),
    4: (4, 4, 6, 4, 6),
    5: (5, 5, 7, 5, 7),
    6: (6, 6, 9, 6, 9),
    7: (7, 7, 12, 7, 12),
}
# Actual distribution in card_data.json (85 cards)
POOL = ([1] * 8 + [2] * 9 + [3] * 15 + [4] * 21 + [5] * 16 + [6] * 9 + [7] * 7)
CLASSES = ["Warrior", "Knight", "Strategist", "Perfect"]
ELEMENTS = ["Andras", "Ktini", "Pnevmas"]

# --- shipped constants ---
FRONT_ATK_BONUS = 1
MIDDLE_HP_BONUS = 1
MAX_SLOTS = 3
MAX_TICKS = 12
ELEM_BONUS = 0.25


def multiplier(tick):
    if tick >= 10:
        return 12
    if tick >= 7:
        return 9
    return 6


def counters(a, d):
    return (a == "Ktini" and d == "Andras") or \
           (a == "Andras" and d == "Pnevmas") or \
           (a == "Pnevmas" and d == "Ktini")


class Unit:
    def __init__(self, rarity, cls, elem, lane):
        cost, amin, amax, hmin, hmax = RARITY[rarity]
        atk = random.randint(amin, amax)
        hp = random.randint(hmin, hmax)
        if cls == "Warrior":
            atk += 1
        if lane == "Front":
            atk += FRONT_ATK_BONUS
        if lane == "Middle":
            hp += MIDDLE_HP_BONUS
        self.rarity, self.cls, self.elem = rarity, cls, elem
        self.atk, self.hp = atk, hp
        self.slots = 2 if rarity >= 5 else 1

    @property
    def alive(self):
        return self.hp > 0


def dominant(lane_units):
    living = [u for u in lane_units if u.alive]
    if not living:
        return None
    c = Counter(u.elem for u in living).most_common()
    if len(c) > 1 and c[0][1] == c[1][1]:
        return None
    return c[0][0]


def apply_damage(lane_units, dmg):
    """Taunt (Knight) absorbs first. Returns leftover only if no living defenders remain."""
    taunts = [u for u in lane_units if u.alive and u.cls == "Knight"]
    others = [u for u in lane_units if u.alive and u.cls != "Knight"]
    remaining = dmg
    for u in taunts + others:
        if remaining <= 0:
            break
        dealt = min(remaining, u.hp)
        u.hp -= dealt
        remaining -= dealt
    undefended = not any(u.alive for u in lane_units)
    return remaining if undefended else 0


def build_squad():
    """Fill 3 lanes to their 3-slot budget, honouring slot weighting."""
    squad = {"Front": [], "Middle": [], "Back": []}
    for lane in squad:
        used = 0
        while used < MAX_SLOTS:
            r = random.choice(POOL)
            w = 2 if r >= 5 else 1
            if used + w > MAX_SLOTS:
                r = random.choice([x for x in POOL if x <= 4])
                w = 1
            squad[lane].append(Unit(r, random.choice(CLASSES), random.choice(ELEMENTS), lane))
            used += w
    return squad


def simulate(hp_a, hp_b):
    A, B = build_squad(), build_squad()
    ha, hb = hp_a, hp_b
    for tick in range(1, MAX_TICKS + 1):
        m = multiplier(tick)
        ovf_a = ovf_b = 0
        for lane in ("Front", "Middle", "Back"):
            la, lb = A[lane], B[lane]
            atk_a = sum(u.atk for u in la if u.alive)
            atk_b = sum(u.atk for u in lb if u.alive)
            da, db = dominant(la), dominant(lb)
            if da and db and counters(da, db):
                atk_a = round(atk_a * (1 + ELEM_BONUS))
            if da and db and counters(db, da):
                atk_b = round(atk_b * (1 + ELEM_BONUS))
            ovf_b += apply_damage(lb, atk_a)
            ovf_a += apply_damage(la, atk_b)
            A[lane] = [u for u in la if u.alive]
            B[lane] = [u for u in lb if u.alive]
        ha = max(0, ha - ovf_a * m)
        hb = max(0, hb - ovf_b * m)
        if ha <= 0 or hb <= 0:
            return ("KO", tick, ha / hp_a, hb / hp_b)
    return ("CAP", MAX_TICKS, ha / hp_a, hb / hp_b)


def run(hp, n=4000, label=""):
    random.seed(7)
    outcomes = Counter()
    ticks = []
    margins = []
    for _ in range(n):
        how, tick, fa, fb = simulate(hp, hp)
        outcomes[how] += 1
        ticks.append(tick)
        if how == "CAP":
            margins.append(abs(fa - fb))
    ko = outcomes["KO"]
    print(f"  Avatar HP {hp:>6}{label}: KO {100*ko/n:5.1f}%  |  cap {100*outcomes['CAP']/n:5.1f}%"
          f"  |  avg length {sum(ticks)/len(ticks):4.1f} ticks", end="")
    if margins:
        near = sum(1 for m in margins if m < 0.05)
        print(f"  |  cap-decided within 5% HP: {100*near/len(margins):4.1f}%")
    else:
        print()


print("How do matches actually end? (4,000 sims each, shipped maths)\n")
print("Current shipped value:")
run(400, label="  <- NEW shipped (mid profile)")
print("\nSweep:")
for hp in (120, 300, 400, 540, 800, 1300):
    run(hp)
