"""
Voix non verbales (provisoires, remplaçables par de vrais enregistrements du même nom) :
cris des monstres par famille et des intrus (homme / femme), synthétisés par formants.

    vx_<voix>_<événement>_<n>.wav  dans Assets/Resources/Audio/Sfx (mono 22 kHz, 16 bits)

Voix des monstres : goblin, orc, rat, beast (gnoll), undead, imp, demon, slime, lizard, golem
    événements : aggro (repère un intrus), hurt (blessé), death
Voix des intrus : man, woman
    événements : aggro (cri de guerre), hurt, death, fear (panique)

    python "Tools~/make_voices.py"
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, lfilter

SR = 22050
rng = np.random.default_rng(11)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio", "Sfx")

# voyelles (F1, F2, F3) d'une voix d'homme
VOWELS = {
    "a": (730, 1090, 2440), "o": (570, 840, 2410), "e": (530, 1840, 2480),
    "i": (300, 2200, 2950), "u": (320, 870, 2240), "ae": (660, 1720, 2410),
}


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [max(20, lo), min(SR / 2 - 100, hi)], btype="band", fs=SR, output="sos"), x)


def lp(x, hi, order=2):
    return sosfilt(butter(order, min(hi, SR / 2 - 100), btype="low", fs=SR, output="sos"), x)


def hp(x, lo, order=2):
    return sosfilt(butter(order, lo, btype="high", fs=SR, output="sos"), x)


def resonator(x, f, bw):
    """Résonateur à deux pôles (formant)."""
    r = np.exp(-np.pi * bw / SR)
    th = 2 * np.pi * f / SR
    a = [1, -2 * r * np.cos(th), r * r]
    return lfilter([1 - r], a, x)


def contour(points, n):
    """Courbe par morceaux : [(t 0..1, valeur), ...] → n échantillons."""
    ts = np.array([p[0] for p in points]) * (n - 1)
    vs = np.array([p[1] for p in points], dtype=float)
    return np.interp(np.arange(n), ts, vs)


def glottal(f0, jitter=0.012, breath=0.05, sub=0.0):
    """Source vocale : impulsions glottiques (dent de scie adoucie), gigue, souffle et sous-harmonique (rugosité)."""
    n = len(f0)
    j = lp(rng.standard_normal(n), 30) * jitter * 12
    f = f0 * (1 + j)
    ph = np.cumsum(f) / SR
    saw = 2 * (ph % 1) - 1
    src = lfilter([1], [1, -0.96], saw)               # pente spectrale de la glotte
    src = src / (np.max(np.abs(src)) + 1e-9)
    if sub > 0:
        src += sub * np.sign(np.sin(np.pi * ph)) * 0.6   # demi-fréquence : voix éraillée
    src += breath * rng.standard_normal(n)
    return src


def syllable(sec, f0pts, vowel, scale=1.0, jitter=0.012, breath=0.05, sub=0.0, bw=1.0, env_pts=None):
    n = int(SR * sec)
    f0 = contour(f0pts, n)
    src = glottal(f0, jitter, breath, sub)
    F = VOWELS[vowel]
    out = (resonator(src, F[0] * scale, 90 * bw) * 1.0 +
           resonator(src, F[1] * scale, 120 * bw) * 0.55 +
           resonator(src, F[2] * scale, 180 * bw) * 0.3)
    e = contour(env_pts or [(0, 0), (0.08, 1), (0.7, 0.8), (1, 0)], n)
    out = out / (np.max(np.abs(out)) + 1e-9)   # crête à 1 : les couches ajoutées (souffle, os) gardent leur dosage
    return out * e


def cat(*parts, gap=0.0):
    out = []
    for p in parts:
        out.append(p)
        if gap > 0:
            out.append(np.zeros(int(SR * gap)))
    return np.concatenate(out)


def drive(x, k):
    """Saturation douce (voix de monstre)."""
    x = x / (np.max(np.abs(x)) + 1e-9)
    return np.tanh(x * k) / np.tanh(k)


def save(name, x, peak=0.8):
    x = np.asarray(x, dtype=float)
    x = hp(x, 60)
    fade = min(len(x) // 10, int(SR * 0.015))
    if fade > 0:
        x[-fade:] *= np.linspace(1, 0, fade)
        x[:int(SR * 0.003)] *= np.linspace(0, 1, int(SR * 0.003))
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))


def u(a, b):
    return rng.uniform(a, b)


# ═════════════════════════════════════════════
# INTRUS
# ═════════════════════════════════════════════
for who, fk, sk in (("man", 1.0, 1.0), ("woman", 1.75, 1.17)):
    for i in range(2):
        p = u(0.92, 1.1) * fk
        s = sk * u(0.97, 1.04)
        # cri de guerre : « Hah ! » appuyé
        x = syllable(0.42, [(0, 150 * p), (0.25, 210 * p), (1, 160 * p)], "a", s, breath=0.08, sub=0.15 if who == "man" else 0,
                     env_pts=[(0, 0), (0.05, 1), (0.6, 0.9), (1, 0)])
        save(f"vx_{who}_aggro_{i + 1}", drive(x, 1.6))
        # blessé : « Agh ! » bref
        x = syllable(0.2, [(0, 230 * p), (1, 170 * p)], "ae", s, breath=0.15, env_pts=[(0, 0), (0.05, 1), (0.4, 0.7), (1, 0)])
        save(f"vx_{who}_hurt_{i + 1}", drive(x, 2.0))
        # mort : long « aaah » qui retombe et s'éteint dans un souffle
        x = syllable(0.85, [(0, 260 * p), (0.2, 240 * p), (1, 110 * p)], "a", s, jitter=0.03, breath=0.12,
                     env_pts=[(0, 0), (0.06, 1), (0.5, 0.7), (1, 0)])
        x += bp(rng.standard_normal(len(x)), 800, 3000) * contour([(0, 0), (0.6, 0), (0.85, 0.12), (1, 0)], len(x))
        save(f"vx_{who}_death_{i + 1}", x)
        # panique : « ah-ah ! » aigu et haletant
        x = cat(syllable(0.13, [(0, 330 * p), (1, 380 * p)], "a", s * 1.05, breath=0.2),
                syllable(0.17, [(0, 360 * p), (1, 300 * p)], "ae", s * 1.05, breath=0.2), gap=0.05)
        save(f"vx_{who}_fear_{i + 1}", x)

# ═════════════════════════════════════════════
# MONSTRES
# ═════════════════════════════════════════════
for i in range(2):
    k = u(0.93, 1.08)
    # GOBELIN : jacassement aigu, nasillard
    x = cat(*[syllable(u(0.07, 0.11), [(0, 330 * k), (1, u(380, 460) * k)], v, 1.3, jitter=0.03, breath=0.1, bw=0.7)
              for v in ("e", "i", "a", "e")[: 3 + i]], gap=0.03)
    save(f"vx_goblin_aggro_{i + 1}", drive(x, 2.2))
    save(f"vx_goblin_hurt_{i + 1}", drive(syllable(0.16, [(0, 520 * k), (1, 380 * k)], "i", 1.35, breath=0.15, bw=0.7), 2.5))
    save(f"vx_goblin_death_{i + 1}", drive(syllable(0.6, [(0, 480 * k), (0.3, 420 * k), (1, 170 * k)], "e", 1.3, jitter=0.05, breath=0.12, bw=0.8), 2))

    # ORC : grondement grave et rauque
    x = syllable(0.6, [(0, 85 * k), (0.3, 120 * k), (1, 95 * k)], "o", 0.82, jitter=0.03, breath=0.15, sub=0.8,
                 env_pts=[(0, 0), (0.1, 1), (0.7, 0.9), (1, 0)])
    save(f"vx_orc_aggro_{i + 1}", drive(x, 3.5))
    save(f"vx_orc_hurt_{i + 1}", drive(syllable(0.25, [(0, 120 * k), (1, 90 * k)], "u", 0.82, breath=0.2, sub=0.7), 3))
    save(f"vx_orc_death_{i + 1}", drive(syllable(1.0, [(0, 115 * k), (0.4, 95 * k), (1, 50 * k)], "o", 0.8, jitter=0.05, breath=0.2, sub=0.9,
                                                 env_pts=[(0, 0), (0.08, 1), (0.6, 0.6), (1, 0)]), 2.5))

    # RAT : couinements
    def squeak(sec, f0, f1):
        n = int(SR * sec)
        f = contour([(0, f0), (0.3, f1), (1, f0 * 0.9)], n)
        sig = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * np.sin(4 * np.pi * np.cumsum(f) / SR)
        return sig * contour([(0, 0), (0.1, 1), (0.8, 0.6), (1, 0)], n)
    save(f"vx_rat_aggro_{i + 1}", cat(squeak(0.08, 3200 * k, 4200 * k), squeak(0.1, 3400 * k, 4600 * k), squeak(0.07, 3000 * k, 3900 * k), gap=0.04))
    save(f"vx_rat_hurt_{i + 1}", squeak(0.14, 4400 * k, 5200 * k))
    save(f"vx_rat_death_{i + 1}", squeak(0.35, 4200 * k, 2400 * k))

    # GNOLL (bête) : ricanement de hyène
    x = cat(*[syllable(0.075, [(0, 420 * k), (1, 470 * k)], "i", 1.15, breath=0.2, sub=0.2, bw=0.8) for _ in range(5 + i)], gap=0.035)
    save(f"vx_beast_aggro_{i + 1}", drive(x, 2.4))
    save(f"vx_beast_hurt_{i + 1}", drive(syllable(0.22, [(0, 600 * k), (1, 420 * k)], "i", 1.2, breath=0.25), 2.4))
    save(f"vx_beast_death_{i + 1}", drive(syllable(0.7, [(0, 520 * k), (0.4, 380 * k), (1, 150 * k)], "u", 1.1, jitter=0.06, breath=0.2, sub=0.4), 2))

    # MORT-VIVANT : plainte caverneuse, souffle, cliquetis d'os
    def moan(sec, f0, f1, vowel="u"):
        n = int(SR * sec)
        x = syllable(sec, [(0, f0), (0.5, (f0 + f1) / 2 * 1.08), (1, f1)], vowel, 0.9, jitter=0.04, breath=0.45, bw=1.6)
        x *= 1 + 0.25 * np.sin(2 * np.pi * 5.5 * np.arange(n) / SR)
        return x
    rattle = np.zeros(int(SR * 0.5))
    for _ in range(10):
        p0 = rng.integers(0, len(rattle) - 400)
        rattle[p0:p0 + 300] += bp(rng.standard_normal(300), 1800, 5000) * np.exp(-np.arange(300) / 40) * u(0.3, 0.9)
    x = moan(1.0, 105 * k, 140 * k, "o")
    x[:len(rattle)] += rattle * 0.4
    save(f"vx_undead_aggro_{i + 1}", x)
    save(f"vx_undead_hurt_{i + 1}", moan(0.35, 150 * k, 120 * k, "u"))
    save(f"vx_undead_death_{i + 1}", moan(1.2, 140 * k, 60 * k, "o"))

    # DIABLOTIN : crissement aigu et saturé
    save(f"vx_imp_aggro_{i + 1}", drive(cat(syllable(0.12, [(0, 600 * k), (1, 800 * k)], "i", 1.5, breath=0.2, bw=0.6),
                                            syllable(0.2, [(0, 760 * k), (1, 540 * k)], "e", 1.5, breath=0.2, bw=0.6), gap=0.03), 4))
    save(f"vx_imp_hurt_{i + 1}", drive(syllable(0.14, [(0, 900 * k), (1, 650 * k)], "i", 1.5, breath=0.25), 4))
    save(f"vx_imp_death_{i + 1}", drive(syllable(0.55, [(0, 850 * k), (1, 260 * k)], "e", 1.45, jitter=0.06, breath=0.25), 3))

    # DÉMON : rugissement très grave, saturé, modulé en anneau
    def roar(sec, f0, f1):
        n = int(SR * sec)
        x = syllable(sec, [(0, f0), (0.35, f0 * 1.35), (1, f1)], "a", 0.65, jitter=0.04, breath=0.3, sub=1.0, bw=1.3,
                     env_pts=[(0, 0), (0.12, 1), (0.75, 0.85), (1, 0)])
        x *= 0.7 + 0.3 * np.sin(2 * np.pi * 37 * np.arange(n) / SR)
        return drive(x + lp(rng.standard_normal(n), 400) * 0.3 * contour([(0, 0), (0.2, 1), (1, 0)], n), 4)
    save(f"vx_demon_aggro_{i + 1}", roar(1.1, 60 * k, 55 * k))
    save(f"vx_demon_hurt_{i + 1}", roar(0.4, 80 * k, 60 * k))
    save(f"vx_demon_death_{i + 1}", roar(1.6, 75 * k, 30 * k))

    # SLIME / CHAMPIGNON : glouglous
    def blub(sec, f0, f1):
        n = int(SR * sec)
        f = contour([(0, f0), (1, f1)], n)
        return lp(np.sin(2 * np.pi * np.cumsum(f) / SR), 900) * contour([(0, 0), (0.1, 1), (1, 0)], n)
    save(f"vx_slime_aggro_{i + 1}", cat(blub(0.09, 180 * k, 420 * k), blub(0.08, 220 * k, 500 * k), blub(0.11, 160 * k, 380 * k), gap=0.03))
    save(f"vx_slime_hurt_{i + 1}", blub(0.14, 520 * k, 220 * k))
    save(f"vx_slime_death_{i + 1}", cat(blub(0.12, 400 * k, 150 * k), blub(0.2, 300 * k, 80 * k), gap=0.05))

    # HOMME-LÉZARD : sifflement et claquement de gorge
    def hiss(sec):
        n = int(SR * sec)
        return bp(rng.standard_normal(n), 3000 * k, 7000) * contour([(0, 0), (0.15, 1), (0.8, 0.8), (1, 0)], n)
    clk = bp(rng.standard_normal(int(SR * 0.03)), 600, 2500) * np.exp(-np.arange(int(SR * 0.03)) / 80)
    save(f"vx_lizard_aggro_{i + 1}", cat(clk, clk, hiss(0.6), gap=0.05))
    save(f"vx_lizard_hurt_{i + 1}", cat(hiss(0.18), syllable(0.12, [(0, 200 * k), (1, 160 * k)], "e", 0.9, breath=0.4), gap=0.0))
    save(f"vx_lizard_death_{i + 1}", cat(syllable(0.4, [(0, 220 * k), (1, 90 * k)], "a", 0.85, breath=0.5, jitter=0.05), hiss(0.5)))

    # GOLEM : grondement de pierre
    def grind(sec, f0):
        n = int(SR * sec)
        hum = syllable(sec, [(0, f0), (1, f0 * 0.85)], "o", 0.55, breath=0.1, sub=1.0, bw=1.5)
        crack = np.zeros(n)
        for _ in range(int(sec * 30)):
            p0 = rng.integers(0, max(1, n - 200))
            crack[p0:p0 + 200] += lp(rng.standard_normal(200), 1500) * np.exp(-np.arange(200) / 50) * u(0.2, 1)
        return drive(hum * 0.8 + crack * 0.6, 2)
    save(f"vx_golem_aggro_{i + 1}", grind(0.9, 50 * k))
    save(f"vx_golem_hurt_{i + 1}", grind(0.3, 60 * k))
    save(f"vx_golem_death_{i + 1}", grind(1.4, 45 * k))

print("voix générées dans", os.path.abspath(OUT))
