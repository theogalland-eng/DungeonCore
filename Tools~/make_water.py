"""
Tuile de mer animée (8 images 16x16, raccordables) à partir de la tuile d'eau du pack neige.
Sortie : Assets/Art/Generated/sea_anim.png (128x16)
"""
import math, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
snow = Image.open(os.path.join(ROOT, 'Assets', 'imports', 'snow-expansion.png')).convert('RGBA')
# snow-expansion_176 : x = (176 % 22) * 16, y depuis le haut = (176 // 22) * 16
base = snow.crop(((176 % 22) * 16, (176 // 22) * 16, (176 % 22) * 16 + 16, (176 // 22) * 16 + 16))
N = 8
out = Image.new('RGBA', (16 * N, 16))
for f in range(N):
    fr = base.copy()
    px = fr.load()
    for y in range(16):
        for x in range(16):
            r, g, b, a = px[x, y]
            # vaguelettes : deux ondes qui se croisent, période 16 (raccord parfait)
            w1 = math.sin((x + y * 0.5 + f * 2) * 2 * math.pi / 16)
            w2 = math.sin((x * 0.5 - y + f * 2) * 2 * math.pi / 16)
            k = 1.0 + 0.06 * w1 + 0.04 * w2
            if w1 > 0.93 and (x + f) % 3 != 0:
                k += 0.28      # crête qui scintille
            px[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), a)
    out.paste(fr, (f * 16, 0))
dst = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'sea_anim.png')
out.save(dst)
print('ok', dst)
