# -*- coding: utf-8 -*-
# Genera dos chimes cortos y suaves para Ritmo:
#   break.wav  -> descendente (entra el descanso)
#   resume.wav -> ascendente  (vuelve la concentración)
import math, struct, os

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "Ritmo.App", "Assets")

def tone(freq, t, dur, attack=0.005):
    # campana: ataque rápido + decaída exponencial; + 2º armónico suave
    if t < 0 or t > dur:
        return 0.0
    env = math.exp(-t * 4.5)
    if t < attack:
        env *= t / attack
    s = math.sin(2*math.pi*freq*t) + 0.25*math.sin(2*math.pi*freq*2*t)
    return env * s

def render(notes, total):
    n = int(SR*total)
    buf = bytearray()
    for i in range(n):
        t = i/SR
        v = 0.0
        for (freq, start, dur) in notes:
            v += tone(freq, t-start, dur)
        v *= 0.30
        # fade out global para evitar clicks al final
        if t > total-0.02:
            v *= max(0.0, (total-t)/0.02)
        v = max(-1.0, min(1.0, v))
        buf += struct.pack("<h", int(v*32767))
    return buf

def save(path, data):
    n = len(data)
    with open(path, "wb") as f:
        f.write(b"RIFF")
        f.write(struct.pack("<I", 36+n))
        f.write(b"WAVEfmt ")
        f.write(struct.pack("<IHHIIHH", 16, 1, 1, SR, SR*2, 2, 16))
        f.write(b"data")
        f.write(struct.pack("<I", n))
        f.write(data)

G5, D5, C6, A5 = 783.99, 587.33, 1046.50, 880.00
# Descanso: dos notas descendentes (relax)
brk = render([(A5, 0.0, 0.45), (D5, 0.13, 0.50)], 0.62)
# Reanudar: dos notas ascendentes (a darle)
res = render([(D5, 0.0, 0.45), (A5, 0.13, 0.50)], 0.62)

os.makedirs(OUT, exist_ok=True)
save(os.path.join(OUT, "break.wav"), brk)
save(os.path.join(OUT, "resume.wav"), res)
print("OK:", os.path.abspath(os.path.join(OUT, "break.wav")))
print("OK:", os.path.abspath(os.path.join(OUT, "resume.wav")))
