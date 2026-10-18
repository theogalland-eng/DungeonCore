"""
Couches musicales dynamiques (MusicManager) : jouées en boucle par-dessus les morceaux, leur volume suit la tension.
Sans tempo marqué, pour ne jamais se heurter au rythme des musiques (Gemini Music) :

  layer_war.wav   : tambours de guerre lointains, irréguliers, et grondement — monte avec l'intensité des combats
  layer_dread.wav : nappe grave et dissonante, cordes en trémolo — monte quand le cristal est menacé ou face à un boss

Sortie : Assets/Resources/Audio/Layers/*.wav (mono 22 kHz)

    python "Tools~/make_music_layers.py"
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 22050
LOOP = 32.0
N = int(SR * LOOP)
t = np.arange(N) / SR
rng = np.random.default_rng(21)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio", "Layers")


def lp(x, hi, order=2):
    return sosfilt(butter(order, hi, btype="low", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def loop_add(buf, x, at):
    """Ajoute x à partir de at, en repliant la fin au début (boucle sans couture)."""
    i = int(at * SR) % N
    k = min(len(x), N - i)
    buf[i:i + k] += x[:k]
    if k < len(x):
        buf[:len(x) - k] += x[k:]


def save(name, x, peak=0.8):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))


def reverb(x, sec=2.0, mix=0.3):
    """Réverbération simple : convolution par un bruit à décroissance exponentielle (en boucle)."""
    n = int(SR * sec)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (SR * sec / 5))
    ir = lp(ir, 3000)
    ir /= np.sqrt(np.sum(ir ** 2))
    X = np.fft.rfft(x)
    H = np.fft.rfft(ir, len(x))
    wet = np.fft.irfft(X * H, len(x))   # convolution circulaire : la queue repasse au début
    return x * (1 - mix) + wet * mix * 3


# ── Tambours de guerre : coups graves (taiko), parfois doublés, à intervalles irréguliers
def taiko(f0=58, sec=1.4, gain=1.0):
    n = int(SR * sec)
    k = np.arange(n) / SR
    f = f0 * (1 + 0.6 * np.exp(-k * 18))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-k * 3.2)
    skin = lp(rng.standard_normal(n), 900) * np.exp(-k * 30) * 0.5
    return (body + skin) * gain


war = np.zeros(N)
pos = 0.3
while pos < LOOP - 0.2:
    g = rng.uniform(0.55, 1.0)
    loop_add(war, taiko(rng.uniform(50, 66), gain=g), pos)
    if rng.random() < 0.35:   # coup doublé
        loop_add(war, taiko(rng.uniform(62, 75), 0.9, g * 0.6), pos + rng.uniform(0.22, 0.32))
    pos += rng.choice([1.1, 1.3, 1.6, 2.1, 2.6])
rumble = lp(rng.standard_normal(N), 70) * (0.5 + 0.5 * np.sin(2 * np.pi * t / LOOP * 2) ** 2)
war = reverb(war, 2.6, 0.35) + rumble * 3
save("layer_war", war)

# ── Effroi : quintes et seconde mineure graves, trémolo, battements lents
dread = np.zeros(N)
for f, a in ((55, 1.0), (82.4, 0.6), (58.3, 0.45), (110, 0.35), (116.5, 0.25)):
    # périodes entières sur la boucle pour qu'elle se referme sans clic
    fr = round(f * LOOP) / LOOP
    tone = sum(np.sin(2 * np.pi * fr * h * t) / h for h in (1, 2, 3, 4))
    trem = 0.75 + 0.25 * np.sin(2 * np.pi * (round(7 * LOOP) / LOOP) * t + rng.uniform(0, 6))
    swell = 0.6 + 0.4 * np.sin(2 * np.pi * t / LOOP * rng.integers(1, 3) + rng.uniform(0, 6))
    dread += tone * trem * swell * a
air = bp(rng.standard_normal(N), 300, 1200) * 0.15 * (0.5 + 0.5 * np.sin(2 * np.pi * t / LOOP * 4))
dread = reverb(lp(dread, 1400) + air, 3.0, 0.4)
save("layer_dread", dread, 0.7)
print("couches générées dans", os.path.abspath(OUT))
