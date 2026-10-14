"""
Bruitages procéduraux (provisoires, remplaçables par de vrais sons du même nom) :
coups, flèches, sorts, pièges, creusage, butin, niveau, cris, cristal, tonnerre...
Plusieurs variantes par son : nom_1, nom_2... (le jeu en tire une au hasard).
Sortie : Assets/Resources/Audio/Sfx/*.wav (mono 22 kHz, 16 bits)

    python "Tools~/make_sfx.py"
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 22050
rng = np.random.default_rng(3)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio", "Sfx")


def T(sec):
    return np.arange(int(SR * sec)) / SR


def noise(sec):
    return rng.standard_normal(int(SR * sec))


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def lp(x, hi, order=2):
    return sosfilt(butter(order, hi, btype="low", fs=SR, output="sos"), x)


def hp(x, lo, order=2):
    return sosfilt(butter(order, lo, btype="high", fs=SR, output="sos"), x)


def env(n, attack=0.002, decay=8.0):
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-t * decay)


def sweep(f0, f1, sec, shape="sin"):
    t = T(sec)
    f = f0 * (f1 / f0) ** (t / sec)
    ph = 2 * np.pi * np.cumsum(f) / SR
    if shape == "saw":
        return 2 * ((ph / (2 * np.pi)) % 1) - 1
    return np.sin(ph)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def pad(x, sec):
    return np.concatenate([np.zeros(int(SR * sec)), x])


def save(name, x, peak=0.85):
    x = np.asarray(x, dtype=float)
    fade = min(len(x) // 10, int(SR * 0.01))
    if fade > 0:
        x[-fade:] *= np.linspace(1, 0, fade)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))


def ring(freqs, sec, decay):
    t = T(sec)
    return sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * np.exp(-t * decay * (1 + i * 0.4)) for i, f in enumerate(freqs))


# ── Combat ──
for i in range(3):
    s = rng.uniform(0.9, 1.15)
    # épée qui frappe : impact + résonance métallique
    x = mix(bp(noise(0.05), 1500, 6000) * env(int(SR * 0.05), 0.001, 60),
            ring([1250 * s, 2380 * s, 3710 * s], 0.35, 14) * 0.35,
            lp(noise(0.12), 400) * env(int(SR * 0.12), 0.001, 30) * 1.5)
    save(f"hit_sword_{i + 1}", x)
    # coup dans la chair / armure légère
    save(f"hit_flesh_{i + 1}", mix(lp(noise(0.14), 700 * s) * env(int(SR * 0.14), 0.001, 28),
                                   np.sin(2 * np.pi * 90 * s * T(0.12)) * env(int(SR * 0.12), 0.001, 30)))
    # griffes / morsure de monstre
    save(f"hit_claw_{i + 1}", mix(bp(noise(0.09), 900 * s, 4000) * env(int(SR * 0.09), 0.004, 35),
                                  pad(lp(noise(0.1), 500) * env(int(SR * 0.1), 0.001, 30), 0.03)))
    # moulinet dans le vide
    n = int(SR * 0.22)
    w = bp(noise(0.22), 500, 2500) * np.sin(np.linspace(0, np.pi, n)) ** 2
    save(f"swing_{i + 1}", w * (0.6 + 0.4 * np.linspace(0, 1, n)))

# Arc : corde qui claque + souffle
for i in range(2):
    s = rng.uniform(0.95, 1.1)
    twang = sweep(210 * s, 150 * s, 0.25, "saw") * env(int(SR * 0.25), 0.001, 16)
    save(f"arrow_shoot_{i + 1}", mix(lp(twang, 1800) * 0.8, bp(noise(0.18), 1500, 5000) * env(int(SR * 0.18), 0.02, 16) * 0.5))
    save(f"arrow_hit_{i + 1}", mix(lp(noise(0.08), 1200) * env(int(SR * 0.08), 0.001, 45),
                                   np.sin(2 * np.pi * 160 * T(0.1)) * env(int(SR * 0.1), 0.001, 35) * 0.8))

# Magie
x = bp(noise(0.55), 300, 3000) * np.linspace(0.2, 1, int(SR * 0.55)) ** 1.5
x = mix(x * env(int(SR * 0.55), 0.25, 3), hp(noise(0.55), 3000) * (rng.random(int(SR * 0.55)) > 0.97) * 0.8)
save("fireball", x)
for i in range(2):
    boom = lp(noise(1.3), 350 + 150 * i) * env(int(SR * 1.3), 0.002, 3.5)
    crack = bp(noise(0.15), 800, 5000) * env(int(SR * 0.15), 0.001, 25)
    save(f"explosion_{i + 1}", mix(boom * 2, crack, np.sin(2 * np.pi * 45 * T(0.6)) * env(int(SR * 0.6), 0.002, 6)))
t = T(0.9)
save("heal", sum(np.sin(2 * np.pi * f * t) * env(len(t), 0.02 + k * 0.08, 4) * (0.8 ** k)
                 for k, f in enumerate([523, 659, 784, 1046])) + hp(noise(0.9), 5000) * env(len(t), 0.1, 4) * 0.08)
t = T(1.6)
save("summon", mix(bp(noise(1.6), 150, 1200) * np.linspace(0, 1, len(t)) ** 2 * env(len(t), 0.001, 0.6),
                   sweep(60, 110, 1.6) * np.linspace(0, 1, len(t)) * 0.6))
t = T(1.8)
save("evolve", mix(sum(np.sin(2 * np.pi * f * t) * np.linspace(0, 1, len(t)) * np.exp(-np.maximum(t - 1.2, 0) * 6)
                       for f in [392, 587, 784, 1175]) * 0.3, hp(noise(1.8), 4000) * np.linspace(0, 1, len(t)) * 0.15))

# Pièges
for i in range(2):
    save(f"trap_spikes_{i + 1}", mix(hp(noise(0.12), 2500) * env(int(SR * 0.12), 0.001, 30),
                                     ring([2900, 4100, 5300], 0.3, 18) * 0.25, lp(noise(0.06), 600) * env(int(SR * 0.06), 0.001, 50)))
    n = int(SR * 0.3)
    save(f"trap_blade_{i + 1}", mix(bp(noise(0.3), 1200, 5000) * np.sin(np.linspace(0, np.pi, n)) ** 3, ring([1800, 2600], 0.3, 10) * 0.2))
save("trap_snap", mix(bp(noise(0.05), 1000, 6000) * env(int(SR * 0.05), 0.001, 80), ring([900, 1500], 0.25, 20) * 0.4))
save("trap_fire", mix(bp(noise(0.7), 200, 2500) * env(int(SR * 0.7), 0.04, 4), hp(noise(0.7), 3000) * (rng.random(int(SR * 0.7)) > 0.95) * 0.6))
save("trap_web", mix(lp(noise(0.3), 900) * env(int(SR * 0.3), 0.01, 9), sweep(300, 180, 0.3) * env(int(SR * 0.3), 0.01, 10) * 0.3))

# Construction
for i in range(3):
    grains = np.zeros(int(SR * 0.5))
    for _ in range(9):
        g = lp(noise(0.05), rng.uniform(500, 1500)) * env(int(SR * 0.05), 0.001, 50)
        k = rng.integers(0, len(grains) - len(g))
        grains[k:k + len(g)] += g * rng.uniform(0.3, 1)
    save(f"dig_{i + 1}", mix(grains, lp(noise(0.2), 250) * env(int(SR * 0.2), 0.001, 12) * 1.5))
save("place", mix(lp(noise(0.12), 500) * env(int(SR * 0.12), 0.001, 30), np.sin(2 * np.pi * 110 * T(0.15)) * env(int(SR * 0.15), 0.001, 25)))
save("door_break", mix(bp(noise(0.4), 300, 3000) * env(int(SR * 0.4), 0.001, 10), ring([180, 260, 410], 0.4, 12) * 0.5))

# Récompenses / interface
t = T(0.35)
save("coin", mix(np.sin(2 * np.pi * 1318 * t) * env(len(t), 0.001, 12), pad(np.sin(2 * np.pi * 1760 * T(0.3)) * env(int(SR * 0.3), 0.001, 12), 0.07)))
t = T(1.2)
save("levelup", mix(*[pad(np.sin(2 * np.pi * f * T(0.7)) * env(int(SR * 0.7), 0.005, 5), k * 0.11) for k, f in enumerate([523, 659, 784, 1046])]))
save("ui_click", mix(bp(noise(0.03), 2000, 6000) * env(int(SR * 0.03), 0.0005, 120), np.sin(2 * np.pi * 900 * T(0.03)) * env(int(SR * 0.03), 0.0005, 150) * 0.5), 0.5)

# Cris et morts
def voice(f0, f1, sec, formants, rough=0.0):
    src = sweep(f0, f1, sec, "saw") + rough * noise(sec)
    return sum(bp(src, f * 0.85, f * 1.15) * g for f, g in formants)


for i in range(3):
    s = rng.uniform(0.85, 1.2)
    v = voice(190 * s, 110 * s, 0.45, [(700, 1.0), (1150, 0.6), (2600, 0.25)], 0.2)
    save(f"death_human_{i + 1}", v * env(len(v), 0.02, 5))
    v = voice(330 * s, 480 * s, 0.35, [(800, 1.0), (1300, 0.6), (2800, 0.3)], 0.1)
    save(f"flee_{i + 1}", v * np.sin(np.linspace(0, np.pi, len(v))))
    g = voice(80 * s, 55 * s, 0.6, [(300, 1.0), (650, 0.6), (1400, 0.2)], 0.8) * (0.7 + 0.3 * np.sin(2 * np.pi * 23 * T(0.6)))
    save(f"death_monster_{i + 1}", g * env(len(g), 0.03, 4))
    g = voice(95 * s, 120 * s, 0.5, [(350, 1.0), (700, 0.5)], 0.9) * (0.7 + 0.3 * np.sin(2 * np.pi * 31 * T(0.5)))
    save(f"growl_{i + 1}", g * np.sin(np.linspace(0, np.pi, len(g))))

# Cristal
save("core_hit", mix(ring([1480, 2210, 3130, 4400], 0.6, 7) * 0.5, hp(noise(0.05), 3000) * env(int(SR * 0.05), 0.001, 60)))
shards = np.zeros(int(SR * 2.2))
for _ in range(40):
    g = ring([rng.uniform(1500, 5000)], 0.3, rng.uniform(10, 25))
    k = rng.integers(0, int(SR * 1.2))
    shards[k:k + len(g)] += g * rng.uniform(0.2, 0.7)
save("core_break", mix(shards, lp(noise(2.2), 200) * env(int(SR * 2.2), 0.01, 2) * 3))

# Tonnerre
for i in range(2):
    n = int(SR * 3.5)
    rumble = lp(noise(3.5), 180) * env(n, 0.05, 1.2)
    crack = hp(noise(0.3), 1200) * env(int(SR * 0.3), 0.001, 14)
    save(f"thunder_{i + 1}", mix(crack * 0.6, rumble * 4, lp(noise(3.5), 600) * env(n, 0.001, 3)))

print("ok", len(os.listdir(OUT)), "fichiers")
