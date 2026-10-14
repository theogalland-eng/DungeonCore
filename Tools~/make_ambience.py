"""
Nappes d'ambiance procédurales (en attendant de vraies musiques) :
  amb_dungeon.wav : bourdon grave de la roche, souffle de caverne, gouttes d'eau qui résonnent
  amb_island.wav  : ressac de la mer, vent sur l'île
  amb_danger.wav  : battements de cœur et nappe dissonante (intrus dans le donjon)
Boucles parfaites (tout est périodique sur la durée de la boucle, réverbération circulaire).
Sortie : Assets/Resources/Audio/Ambience/*.wav  (mono 22 kHz, 16 bits)

    python "Tools~/make_ambience.py"
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfiltfilt

SR = 22050
LOOP = 48.0                      # secondes
N = int(SR * LOOP)
t = np.arange(N) / SR
rng = np.random.default_rng(7)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio", "Ambience")


def lfo(cycles, phase=0.0):
    """Oscillation lente qui fait un nombre entier de cycles sur la boucle (donc sans raccord)."""
    return np.sin(2 * np.pi * cycles * t / LOOP + phase)


def tone(freq, amp=1.0):
    # fréquence arrondie pour un nombre entier de périodes sur la boucle
    f = round(freq * LOOP) / LOOP
    return amp * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28))


def circular(sos, x):
    # filtrage sur trois copies de la boucle : le raccord fin → début reste parfait
    if len(x) != N:
        return sosfiltfilt(sos, x)
    return sosfiltfilt(sos, np.tile(x, 3))[N:2 * N]


def band(x, lo, hi):
    return circular(butter(2, [lo, hi], btype="band", fs=SR, output="sos"), x)


def lowpass(x, hi):
    return circular(butter(2, hi, btype="low", fs=SR, output="sos"), x)


def looped_noise(color="pink"):
    """Bruit coloré rendu périodique (spectre à phases aléatoires, donc circulaire)."""
    spec = np.fft.rfft(rng.standard_normal(N))
    f = np.fft.rfftfreq(N, 1 / SR)
    f[0] = 1
    spec *= 1 / np.sqrt(f) if color == "pink" else 1 / f
    x = np.fft.irfft(spec, N)
    return x / np.max(np.abs(x))


def reverb(x, seconds=2.5, wet=0.3, bright=3000):
    """Réverbération par convolution circulaire (la queue reboucle sur le début)."""
    n = int(SR * seconds)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, bright)
    ir /= np.sqrt(np.sum(ir ** 2))
    pad = np.zeros(N)
    pad[:n] = ir
    y = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(pad), N)
    return (1 - wet) * x + wet * y / (np.max(np.abs(y)) + 1e-9) * np.max(np.abs(x))


def place(events, dur, maker):
    """Pose des sons courts (gouttes, battements) en bouclant sur la fin."""
    out = np.zeros(N)
    for start in events:
        s = maker()
        i0 = int(start * SR) % N
        idx = (np.arange(len(s)) + i0) % N
        np.add.at(out, idx, s)
    return out


def drip():
    n = int(SR * 0.09)
    k = np.arange(n) / SR
    f = rng.uniform(900, 1500)
    sweep = np.sin(2 * np.pi * (f * k - 2200 * k * k))
    return sweep * np.exp(-k * 55) * rng.uniform(0.25, 0.6)


def save(name, x, peak=0.7):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print(name, f"{len(x) / SR:.0f}s")


# ── Donjon : la montagne respire ──
drone = (tone(73.4, 1) + tone(73.9, 0.7)) * (0.7 + 0.3 * lfo(3))
drone += (tone(110, 0.55) + tone(110.6, 0.4)) * (0.6 + 0.4 * lfo(2, 1))
drone += tone(146.8, 0.3) * (0.5 + 0.5 * lfo(4, 2))
drone += tone(174.6, 0.22) * (0.4 + 0.6 * lfo(5, 3))     # tierce mineure : plus sombre
drone = lowpass(drone, 900)
wind = band(looped_noise("pink"), 90, 520) * (0.45 + 0.35 * lfo(2, 0.5) + 0.2 * lfo(7, 1.3))
drips = place(np.sort(rng.uniform(0, LOOP, 26)), 0.09, drip)
rumble = lowpass(looped_noise("brown"), 60) * (0.5 + 0.5 * lfo(1, 2.5)) ** 3
dungeon = 0.55 * drone / np.max(np.abs(drone)) + 0.35 * wind / np.max(np.abs(wind)) + 0.5 * drips + 0.35 * rumble
save("amb_dungeon", reverb(dungeon, 3.2, 0.38, 2500))

# ── Île : ressac et vent ──
waves = np.zeros(N)
for k, cycles in enumerate((6, 7, 9)):
    ph = rng.uniform(0, 6.28)
    env = np.clip(np.sin(2 * np.pi * cycles * t / LOOP + ph), 0, None) ** 3   # vague qui monte puis se brise
    waves += env * (1.0 - 0.25 * k)
surf = lowpass(looped_noise("pink"), 1100) * (0.25 + waves)
foam = band(looped_noise("pink"), 2500, 6000) * (waves ** 2) * 0.35
air = band(looped_noise("pink"), 200, 900) * (0.3 + 0.2 * lfo(3, 0.7))
island = reverb(surf + foam + 0.5 * air, 1.5, 0.2, 5000)
island = np.tanh(island / np.max(np.abs(island)) * 3.0)      # vagues plus régulières (compression douce)
save("amb_island", island, 0.6)


# ── Danger : cœur qui bat, nappe dissonante ──
def thump(f0, amp, n):
    k = np.arange(n) / SR
    f = f0 * (1 + 1.2 * np.exp(-k * 30))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-k * 18) * amp


bpm = 72
period = LOOP / round(LOOP * bpm / 60)
beats = np.zeros(N)
for i in range(round(LOOP / period)):
    s = i * period
    beats += place([s], 0.3, lambda: thump(52, 1.0, int(SR * 0.3)))
    beats += place([s + 0.26], 0.3, lambda: thump(47, 0.7, int(SR * 0.3)))
tension = (tone(146.8, 0.4) + tone(155.6, 0.35) + tone(220, 0.2) + tone(233.1, 0.15)) * (0.5 + 0.5 * lfo(12))
tension = lowpass(tension, 1200) * (0.6 + 0.4 * lfo(2))
scrape = band(looped_noise("pink"), 400, 1600) * np.clip(lfo(5, 1), 0, None) ** 4 * 0.5
danger = 0.9 * beats / np.max(np.abs(beats)) + 0.35 * tension / np.max(np.abs(tension)) + 0.25 * scrape
save("amb_danger", reverb(danger, 2.0, 0.3, 2000), 0.75)

# ── Pluie battante (tempête) ──
patter = band(looped_noise("pink"), 1500, 7000) * (0.7 + 0.3 * lfo(5, 0.3))
body = lowpass(looped_noise("pink"), 900) * (0.6 + 0.4 * lfo(3, 1.1))
drops_hi = place(np.sort(rng.uniform(0, LOOP, 400)), 0.02,
                 lambda: np.sin(2 * np.pi * rng.uniform(2500, 5000) * np.arange(int(SR * 0.02)) / SR) * np.exp(-np.arange(int(SR * 0.02)) / SR * 250) * rng.uniform(0.1, 0.4))
save("amb_rain", reverb(0.6 * patter / np.max(np.abs(patter)) + 0.5 * body / np.max(np.abs(body)) + 0.4 * drops_hi, 1.2, 0.2, 6000), 0.6)
