#!/usr/bin/env python3
"""Synthesize every Pale Knight SFX + music loop as 44100 Hz 16-bit mono WAV.

Pure Python 3 standard library only (wave, struct, math, random).
No numpy dependency.

Usage:
    python3 tools/synth_sfx.py

Output:
    assets/sfx/*.wav  (16 SFX + 2 seamless music loops)

Seamless loops are built from whole-cycle partials AND finished with a
0.5 s equal-power crossfade of the tail over the head, so they loop
without clicks.
"""

import math
import os
import random
import struct
import wave

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "assets", "sfx"))

# Module-level RNG so noise() matches the required signature; reseeded per sound.
_RNG = random.Random(0)


# ---------------------------------------------------------------- helpers ---
def sine(phase):
    """Sine oscillator value for a phase in radians."""
    return math.sin(phase)


def saw(phase):
    """Band-unlimited sawtooth in [-1, 1] for a phase in radians."""
    p = (phase / (2.0 * math.pi)) % 1.0
    return 2.0 * p - 1.0


def noise():
    """White noise sample in [-1, 1]."""
    return _RNG.uniform(-1.0, 1.0)


def adsr(n, sr, a, d, s, r):
    """Attack/decay/sustain/release envelope; times in seconds."""
    na, nd, nr = int(a * sr), int(d * sr), int(r * sr)
    env = [0.0] * n
    for i in range(n):
        if na > 0 and i < na:
            env[i] = i / na
        elif nd > 0 and i < na + nd:
            env[i] = 1.0 - (1.0 - s) * (i - na) / nd
        elif i < n - nr:
            env[i] = s
        elif nr > 0:
            env[i] = s * max(0.0, (n - 1 - i) / nr)
    return env


def lowpass(samples, alpha):
    """One-pole lowpass; alpha in (0,1], smaller = darker."""
    out = [0.0] * len(samples)
    y = 0.0
    for i, x in enumerate(samples):
        y += alpha * (x - y)
        out[i] = y
    return out


def moving_avg(samples, window):
    """FIR lowpass via moving average (used for whooshes/rumbles)."""
    window = max(1, int(window))
    out = [0.0] * len(samples)
    acc = 0.0
    for i, x in enumerate(samples):
        acc += x
        if i >= window:
            acc -= samples[i - window]
        out[i] = acc / min(i + 1, window)
    return out


def normalize(samples, peak=0.8):
    """Scale so the peak hits `peak` (~ -2 dB for 0.8)."""
    m = max((abs(x) for x in samples), default=0.0)
    if m <= 0.0:
        return list(samples)
    g = peak / m
    return [x * g for x in samples]


def write_wav(path, samples):
    """Write 16-bit mono WAV at SR. Returns duration in seconds."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = bytearray()
    for x in samples:
        v = int(x * 32767.0)
        if v > 32767:
            v = 32767
        elif v < -32768:
            v = -32768
        data += struct.pack("<h", v)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(data))
    return len(samples) / SR


# --------------------------------------------------------------- building ---
def sweep_fn(f0, f1, dur):
    """Exponential frequency sweep f0 -> f1 over dur seconds."""
    ratio = f1 / f0
    return lambda t: f0 * (ratio ** (t / dur))


def osc(dur, freq, wavefn=sine, amp=1.0, phase0=0.0):
    """Tone of `dur` seconds; freq is Hz or a function f(t). Phase-continuous."""
    n = int(dur * SR)
    out = [0.0] * n
    ph = phase0
    for i in range(n):
        t = i / SR
        f = freq(t) if callable(freq) else freq
        ph += 2.0 * math.pi * f / SR
        out[i] = amp * wavefn(ph)
    return out


def noise_burst(dur, decay, lp_alpha=None, ma_window=None, amp=1.0):
    """White noise with exponential decay; optional lowpass."""
    n = int(dur * SR)
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        out[i] = amp * noise() * math.exp(-t * decay)
    if lp_alpha is not None:
        out = lowpass(out, lp_alpha)
    if ma_window is not None:
        out = moving_avg(out, ma_window)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, x in enumerate(p):
            out[i] += x
    return out


def apply_env(samples, env_fn):
    return [x * env_fn(i / SR) for i, x in enumerate(samples)]


def seamless_loop(samples, xf_sec=0.5):
    """Equal-power crossfade of the tail over the head.

    Generates the mix slightly longer than needed, then blends the extra
    tail (samples n..n+xf) over the head (0..xf) with the head gain rising
    0 -> 1 and the tail gain falling 1 -> 0. Output length is len - xf, and
    the loop point joins two adjacent original samples, so it is seamless.
    """
    xf = int(xf_sec * SR)
    n = len(samples) - xf
    out = list(samples[:n])
    for i in range(xf):
        g_head = math.sin(0.5 * math.pi * i / xf) ** 2  # 0 -> 1
        g_tail = 1.0 - g_head                            # 1 -> 0
        out[i] = samples[i] * g_head + samples[i + n] * g_tail
    return out


# ------------------------------------------------------------------- SFX ---
def s_jump():
    dur = 0.16
    f = sweep_fn(300.0, 620.0, dur)
    s = mix(osc(dur, f, sine, 1.0),
            osc(dur, lambda t: 2.0 * f(t), sine, 0.25))
    s = apply_env(s, lambda t: min(1.0, t / 0.005) * math.exp(-t * 14.0))
    return normalize(s)


def s_dash():
    dur = 0.28
    n = int(dur * SR)
    nz = moving_avg([noise() for _ in range(n)], 28)
    swell = [math.sin(math.pi * i / n) ** 1.2 for i in range(n)]
    s = [nz[i] * swell[i] for i in range(n)]
    body = osc(dur, sweep_fn(420.0, 140.0, dur), sine, 0.35)
    body = apply_env(body, lambda t: math.sin(math.pi * t / dur) ** 1.5)
    return normalize(mix(s, body))


def s_slash():
    dur = 0.12
    burst = noise_burst(dur, decay=38.0, lp_alpha=0.45, amp=1.0)
    ping = apply_env(osc(dur, 2200.0, sine, 0.55),
                     lambda t: math.exp(-t * 32.0))
    return normalize(mix(burst, ping))


def s_hit():
    dur = 0.12
    thud = apply_env(osc(dur, sweep_fn(165.0, 105.0, dur), sine, 1.0),
                     lambda t: math.exp(-t * 30.0))
    click = noise_burst(0.012, decay=400.0, amp=0.7)
    click += [0.0] * (len(thud) - len(click))
    return normalize(mix(thud, click))


def s_enemy_die():
    dur = 0.32
    f = sweep_fn(420.0, 70.0, dur)
    s = mix(osc(dur, f, sine, 1.0),                      # square-ish stack
            osc(dur, lambda t: 3.0 * f(t), sine, 0.32),
            osc(dur, lambda t: 5.0 * f(t), sine, 0.16))
    s = apply_env(s, lambda t: min(1.0, t / 0.008) * math.exp(-t * 7.5))
    return normalize(s)


def s_player_hurt():
    dur = 0.28
    f = sweep_fn(220.0, 55.0, dur)
    s = mix(osc(dur, f, saw, 0.8),
            osc(dur, lambda t: f(t) * 1.045, saw, 0.8),  # detuned pair
            noise_burst(dur, decay=9.0, lp_alpha=0.2, amp=0.5))
    s = apply_env(s, lambda t: min(1.0, t / 0.003) * math.exp(-t * 9.0))
    k = 2.2  # distortion
    s = [math.tanh(k * x) / math.tanh(k) for x in s]
    return normalize(s)


def s_heal():
    dur = 0.7
    out = [0.0] * int(dur * SR)
    for t0, fq in ((0.0, 659.26), (0.18, 880.0), (0.36, 1318.51)):  # E5 A5 E6
        tone = apply_env(osc(0.55, fq, sine, 0.7),
                         lambda t: min(1.0, t / 0.012) * math.exp(-t * 5.5))
        sparkle = apply_env(osc(0.55, fq * 2.0, sine, 0.18),
                            lambda t: math.exp(-t * 9.0))
        start = int(t0 * SR)
        for i, x in enumerate(mix(tone, sparkle)):
            if start + i < len(out):
                out[start + i] += x
    return normalize(out)


def s_spell_cast():
    dur = 0.6
    # Ethereal whoosh-chime: rising sweep 300 -> 900 Hz + harmonics + noise whoosh.
    f = sweep_fn(300.0, 900.0, dur)
    tone = mix(osc(dur, f, sine, 1.0),
               osc(dur, lambda t: 2.0 * f(t), sine, 0.30),
               osc(dur, lambda t: 3.0 * f(t), sine, 0.12))
    tone = apply_env(tone, lambda t: math.sin(math.pi * t / dur) ** 1.2)
    n = int(dur * SR)
    whoosh = moving_avg([noise() for _ in range(n)], 40)
    whoosh = apply_env(whoosh, lambda t: math.sin(math.pi * t / dur) ** 2.0)
    shimmer = apply_env(osc(dur, sweep_fn(1800.0, 3600.0, dur), sine, 0.25),
                        lambda t: math.exp(-t * 4.0))
    return normalize(mix(tone, whoosh, shimmer))


def s_bench():
    dur = 1.4
    parts = []
    for fq in (110.0, 164.81, 220.0, 277.18):  # A2 E3 A3 C#4
        p = osc(dur, fq, sine, 0.5)
        p2 = osc(dur, fq * 2.0, sine, 0.12)
        lfo = [0.92 + 0.08 * math.sin(2 * math.pi * 0.4 * i / SR)
               for i in range(len(p))]
        parts.append([(p[i] + p2[i]) * lfo[i] for i in range(len(p))])
    s = mix(*parts)
    env = adsr(len(s), SR, a=0.35, d=0.25, s=0.85, r=0.45)  # slow attack pad
    s = [s[i] * env[i] for i in range(len(s))]
    return normalize(s)


def s_ui_click():
    dur = 0.07
    s = apply_env(osc(dur, 1200.0, sine, 1.0), lambda t: math.exp(-t * 55.0))
    return normalize(s)


def s_ui_move():
    dur = 0.05
    s = apply_env(osc(dur, 800.0, sine, 1.0), lambda t: math.exp(-t * 70.0))
    return normalize(s, peak=0.45)  # quieter


def s_geo():
    dur = 0.12
    f = sweep_fn(2500.0, 2750.0, dur)
    a = apply_env(osc(dur, f, sine, 1.0), lambda t: math.exp(-t * 28.0))
    b = apply_env(osc(dur, lambda t: 2.0 * f(t), sine, 0.35),
                  lambda t: math.exp(-t * 40.0))
    return normalize(mix(a, b))


def s_gate():
    dur = 0.7
    n = int(dur * SR)
    nz = moving_avg([noise() for _ in range(n)], 90)
    s = mix(nz, osc(dur, 45.0, sine, 0.5))  # stone rumble + sub
    s = apply_env(s, lambda t: min(1.0, t / 0.30) *
                  math.exp(-max(0.0, t - 0.35) * 4.0))
    return normalize(s)


def s_roar():
    dur = 1.3
    def f0(t):
        return 62.0 * (1.0 + 0.12 * math.sin(2 * math.pi * 5.3 * t))
    s = mix(osc(dur, f0, saw, 0.7),
            osc(dur, lambda t: f0(t) * 1.07, saw, 0.6),
            osc(dur, lambda t: f0(t) * 0.5, saw, 0.8),
            [g * (0.6 + 0.4 * math.sin(2 * math.pi * 25.0 * i / SR))
             for i, g in enumerate(noise_burst(dur, decay=1.2,
                                               lp_alpha=0.07, amp=0.9))])
    s = apply_env(s, lambda t: min(1.0, t / 0.08) *
                  (1.0 if t < dur - 0.3 else max(0.0, (dur - t) / 0.3)))
    k = 2.0
    s = [math.tanh(k * x) / math.tanh(k) for x in s]  # distortion
    return normalize(s)


def s_slam():
    dur = 0.5
    boom = apply_env(osc(dur, sweep_fn(95.0, 42.0, dur), sine, 1.0),
                     lambda t: math.exp(-t * 8.0))
    crash = noise_burst(dur, decay=11.0, lp_alpha=0.28, amp=0.75)
    click = noise_burst(0.006, decay=500.0, amp=0.8)
    click += [0.0] * (len(boom) - len(click))
    return normalize(mix(boom, crash, click))


def s_stagger():
    dur = 0.5
    f0 = sweep_fn(640.0, 210.0, dur)
    parts = [osc(dur, lambda t, r=r: r * f0(t), sine, am)
             for r, am in ((1.0, 0.7), (2.76, 0.4), (5.40, 0.22))]  # metallic
    s = mix(*parts)
    s = [s[i] * (0.62 + 0.38 * math.sin(2 * math.pi * 27.0 * i / SR))
         * math.exp(-(i / SR) * 6.5) for i in range(len(s))]
    return normalize(s)


def s_boss_die():
    dur = 1.6
    crash = apply_env(noise_burst(dur, decay=3.2, lp_alpha=0.22, amp=1.0),
                      lambda t: min(1.0, t / 0.02))
    tone = apply_env(osc(dur, sweep_fn(300.0, 38.0, dur), sine, 0.9),
                     lambda t: min(1.0, t / 0.015) * math.exp(-t * 2.4))
    sub = apply_env(osc(dur, 55.0, sine, 0.45), lambda t: math.exp(-t * 3.6))
    return normalize(mix(crash, tone, sub))


# ----------------------------------------------------------------- music ---
def m_ambient_drone():
    loop = 12.0
    dur = loop + 0.5
    # Whole-cycle partials (f * 12 is an integer) -> loop-safe. The +1/6 Hz
    # detune partners beat slowly (2 beats per loop) for a detuned feel.
    partials = [
        (55.0, 0.50), (55.0 + 1.0 / 6.0, 0.16),
        (82.5, 0.34), (82.5 + 1.0 / 6.0, 0.12),
        (110.0, 0.40), (110.0 + 1.0 / 6.0, 0.14),
        (165.0, 0.10), (220.0, 0.08),
    ]
    n = int(dur * SR)
    s = [0.0] * n
    for fq, am in partials:
        for i, x in enumerate(osc(dur, fq, sine, am)):
            s[i] += x
    lfo = [0.86 + 0.14 * math.sin(2 * math.pi * i / SR / loop)  # 1 cycle/loop
           for i in range(n)]
    s = [s[i] * lfo[i] for i in range(n)]
    air = lowpass([noise() for _ in range(n)], 0.02)  # faint airy noise
    air = [air[i] * 0.06 * (0.7 + 0.3 * math.sin(2 * math.pi * i / SR / loop))
           for i in range(n)]
    s = seamless_loop(mix(s, air), 0.5)
    return normalize(s, peak=0.6)  # deliberately quiet / meditative


def m_boss_music():
    loop = 16.0
    dur = loop + 0.5
    n = int(dur * SR)
    # Whole-cycle quantized partials (f * 16 integer). DIS is E3 + 8 cents,
    # quantized, for the dissonant layer (beats at 0.75 Hz = 12 cycles/loop).
    E3 = round(164.81 * loop) / loop
    DIS = round(164.81 * 2.0 ** (8.0 / 1200.0) * loop) / loop
    drone = mix(osc(dur, 55.0, sine, 0.34),
                osc(dur, 110.0, sine, 0.22),
                osc(dur, E3, sine, 0.17),
                osc(dur, DIS, sine, 0.12),
                osc(dur, 55.0, saw, 0.10))
    pulse = [0.55 + 0.45 * (0.5 - 0.5 * math.cos(2 * math.pi * 2.0 * i / SR))
             for i in range(n)]  # 2 Hz throb, whole cycles
    drone = [drone[i] * pulse[i] for i in range(n)]
    drone = [x * 0.9 for x in lowpass(drone, 0.35)]

    # Driving 100 BPM percussive tick (every 0.6 s), accent every 4th.
    ticks = [0.0] * n
    t = 0.3
    k = 0
    while t < dur:
        accent = (k % 4 == 0)
        start = int(t * SR)
        tn = int(0.035 * SR)
        for i in range(tn):
            if start + i >= n:
                break
            tt = i / SR
            ticks[start + i] += noise() * math.exp(-tt * 130.0) * \
                (1.3 if accent else 0.85)
        if accent:  # low thump under the accent
            th = apply_env(osc(0.09, 170.0, sine, 0.5),
                           lambda tt: math.exp(-tt * 45.0))
            for i, x in enumerate(th):
                if start + i < n:
                    ticks[start + i] += x
        t += 0.6
        k += 1
    lp = lowpass(ticks, 0.06)
    ticks = [(ticks[i] - lp[i]) * 0.8 for i in range(n)]  # clicky highpass

    # Slow rising tension tone in the second half: 220 -> 440 Hz over 8 s,
    # faded out before the crossfade tail region.
    tension = [0.0] * n
    ph = 0.0
    i0, i1 = int(8.0 * SR), int(15.8 * SR)
    for i in range(i0, i1):
        tt = i / SR
        fq = 220.0 * 2.0 ** ((tt - 8.0) / 8.0)
        ph += 2.0 * math.pi * fq / SR
        amp_env = min(1.0, (tt - 8.0) / 2.0) * min(1.0, max(0.0, (15.8 - tt) / 0.8))
        tension[i] = 0.16 * math.sin(ph) * amp_env

    s = seamless_loop(mix(drone, ticks, tension), 0.5)
    return normalize(s)


# ------------------------------------------------------------------ main ---
SOUNDS = [
    ("jump.wav", s_jump, 0.16),
    ("dash.wav", s_dash, 0.28),
    ("slash.wav", s_slash, 0.12),
    ("hit.wav", s_hit, 0.12),
    ("enemy_die.wav", s_enemy_die, 0.32),
    ("player_hurt.wav", s_player_hurt, 0.28),
    ("heal.wav", s_heal, 0.7),
    ("spell_cast.wav", s_spell_cast, 0.6),
    ("bench.wav", s_bench, 1.4),
    ("ui_click.wav", s_ui_click, 0.07),
    ("ui_move.wav", s_ui_move, 0.05),
    ("geo.wav", s_geo, 0.12),
    ("gate.wav", s_gate, 0.7),
    ("roar.wav", s_roar, 1.3),
    ("slam.wav", s_slam, 0.5),
    ("stagger.wav", s_stagger, 0.5),
    ("boss_die.wav", s_boss_die, 1.6),
    ("ambient_drone.wav", m_ambient_drone, 12.0),
    ("boss_music.wav", m_boss_music, 16.0),
]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    total = 0
    for idx, (name, fn, expect) in enumerate(SOUNDS):
        _RNG.seed(20260910 + idx)  # deterministic per sound
        samples = fn()
        path = os.path.join(OUT_DIR, name)
        dur = write_wav(path, samples)
        total += os.path.getsize(path)
        flag = ""
        if expect is not None and abs(dur - expect) > 0.01:
            flag = "  <-- DURATION MISMATCH"
        print(f"{name:18s} {dur:7.3f}s{flag}")
    print(f"{len(SOUNDS)} files, {total} bytes total in {OUT_DIR}")


if __name__ == "__main__":
    main()
