#!/usr/bin/env python3
"""
Generates every sound effect in the game from scratch.

Why synthesise rather than download: these are original, so there is no licence
to track, no attribution to get wrong, and no risk of shipping something we
cannot legally sell. They are also tiny, deterministic, and tweakable - if the
bell is too shrill, change a number here and re-run rather than hunting for a
different free sample.

    python3 tools/assets/generate_audio.py

Writes 16-bit mono WAVs into unity/Assets/AngryGuy/Resources/Audio/, which Unity
imports automatically and Resources.Load<AudioClip>("Audio/<name>") finds.
"""

import math
import os
import struct
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "unity", "Assets", "AngryGuy", "Resources", "Audio",
)

rng = np.random.default_rng(7)


# ---------------------------------------------------------------- helpers

def t(duration):
    return np.linspace(0, duration, int(RATE * duration), endpoint=False)


def env(samples, attack=0.005, decay=0.25, sustain=0.0, release=0.1):
    """Simple ADSR shaped to the length of `samples`."""
    n = len(samples)
    a = int(attack * RATE)
    d = int(decay * RATE)
    r = int(release * RATE)
    s = max(0, n - a - d - r)

    parts = [
        np.linspace(0, 1, a, endpoint=False) if a else np.array([]),
        np.linspace(1, sustain, d, endpoint=False) if d else np.array([]),
        np.full(s, sustain),
        np.linspace(sustain, 0, r, endpoint=False) if r else np.array([]),
    ]
    shape = np.concatenate(parts)
    if len(shape) < n:
        shape = np.pad(shape, (0, n - len(shape)))
    return samples * shape[:n]


def noise(duration):
    return rng.uniform(-1, 1, int(RATE * duration))


def sine(freq, duration, phase=0.0):
    return np.sin(2 * np.pi * freq * t(duration) + phase)


def saw(freq, duration):
    x = t(duration)
    return 2.0 * ((x * freq) % 1.0) - 1.0


def lowpass(samples, cutoff):
    """One-pole lowpass. Crude, but it is the difference between a click and a thud."""
    alpha = 1.0 - math.exp(-2.0 * math.pi * cutoff / RATE)
    out = np.empty_like(samples)
    acc = 0.0
    for i, v in enumerate(samples):
        acc += alpha * (v - acc)
        out[i] = acc
    return out


def highpass(samples, cutoff):
    return samples - lowpass(samples, cutoff)


def normalise(samples, peak=0.85):
    m = np.max(np.abs(samples))
    if m < 1e-9:
        return samples
    return samples / m * peak


def save(name, samples):
    samples = normalise(samples)
    data = np.clip(samples, -1.0, 1.0)
    pcm = (data * 32767).astype(np.int16)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())
    print("  {:<16} {:>6.2f}s  {:>6} bytes".format(name, len(pcm) / RATE, os.path.getsize(path)))


# ------------------------------------------------------------------ sounds

def footstep():
    body = lowpass(noise(0.11), 420)
    click = highpass(noise(0.02), 3000) * 0.25
    click = np.pad(click, (0, len(body) - len(click)))
    return env(body + click, attack=0.001, decay=0.05, release=0.05)


def bell():
    # Struck metal: a few inharmonic partials, long ring.
    d = 1.4
    out = np.zeros(int(RATE * d))
    for freq, amp in [(1180, 1.0), (2360, 0.45), (3120, 0.3), (4700, 0.18)]:
        out += sine(freq, d) * amp * np.exp(-np.linspace(0, 7, len(out)))
    return env(out, attack=0.001, decay=0.2, sustain=0.25, release=1.0)


def clink():
    d = 0.35
    out = sine(2400, d) * 0.6 + sine(3600, d) * 0.3 + highpass(noise(d), 4000) * 0.2
    return env(out * np.exp(-np.linspace(0, 16, int(RATE * d))), attack=0.0005, decay=0.08, release=0.2)


def crunch():
    d = 0.45
    body = lowpass(noise(d), 900) * np.exp(-np.linspace(0, 9, int(RATE * d)))
    snap = highpass(noise(0.06), 2500)
    snap = np.pad(snap, (0, len(body) - len(snap)))
    return env(body + snap * 0.8, attack=0.001, decay=0.12, release=0.25)


def splash():
    d = 0.6
    body = lowpass(noise(d), 1600)
    sweep = np.interp(np.arange(len(body)), [0, len(body)], [1.0, 0.25])
    return env(body * sweep, attack=0.01, decay=0.2, sustain=0.2, release=0.3)


def slip():
    d = 0.75
    n = int(RATE * d)
    # Rising squeak then a heavy landing.
    squeak = np.sin(2 * np.pi * np.cumsum(np.linspace(600, 1500, n)) / RATE) * 0.35
    squeak *= np.exp(-np.linspace(0, 5, n))
    thud = np.zeros(n)
    start = int(n * 0.55)
    hit = lowpass(noise(d * 0.45), 220)
    thud[start:start + len(hit)] = hit[: n - start] * 1.4
    return env(squeak + thud, attack=0.002, decay=0.3, sustain=0.2, release=0.25)


def voice(duration, base, wobble, growl=0.0, brightness=1.0):
    """
    A person-shaped noise. Not speech - just enough formant content that the
    player reads it as a human being annoyed rather than a synth beep.
    """
    n = int(RATE * duration)
    x = np.linspace(0, duration, n, endpoint=False)
    pitch = base * (1.0 + wobble * np.sin(2 * np.pi * 5.5 * x))
    glottal = 2.0 * ((np.cumsum(pitch) / RATE) % 1.0) - 1.0

    out = np.zeros(n)
    for f, a in [(700 * brightness, 1.0), (1220 * brightness, 0.5), (2600 * brightness, 0.22)]:
        out += lowpass(glottal, f) * a

    if growl > 0:
        out += lowpass(rng.uniform(-1, 1, n), 800) * growl

    return out


def grumble():
    d = 0.9
    out = voice(d, base=95, wobble=0.05, growl=0.25, brightness=0.8)
    return env(out, attack=0.04, decay=0.3, sustain=0.5, release=0.35)


def shout():
    d = 1.1
    out = voice(d, base=165, wobble=0.12, growl=0.35, brightness=1.25)
    n = len(out)
    out *= np.concatenate([np.linspace(0.3, 1.0, int(n * 0.15)), np.ones(n - int(n * 0.15))])
    return env(out, attack=0.01, decay=0.25, sustain=0.7, release=0.4)


def huh():
    """The "hm?" an NPC makes when something catches their attention."""
    d = 0.45
    n = int(RATE * d)
    x = np.linspace(0, d, n, endpoint=False)
    pitch = np.linspace(150, 230, n)
    glottal = 2.0 * ((np.cumsum(pitch) / RATE) % 1.0) - 1.0
    out = lowpass(glottal, 900) + lowpass(glottal, 1700) * 0.4
    return env(out, attack=0.02, decay=0.15, sustain=0.5, release=0.2)


def door():
    d = 0.7
    n = int(RATE * d)
    creak = np.sin(2 * np.pi * np.cumsum(np.linspace(320, 210, n)) / RATE)
    creak *= (0.5 + 0.5 * np.sin(2 * np.pi * 22 * np.linspace(0, d, n))) * 0.5
    clack = np.zeros(n)
    hit = lowpass(noise(0.12), 700)
    clack[int(n * 0.72):int(n * 0.72) + len(hit)] = hit[: n - int(n * 0.72)]
    return env(creak + clack, attack=0.01, decay=0.3, sustain=0.3, release=0.25)


def pickup():
    d = 0.16
    out = sine(880, d) * 0.6 + sine(1320, d) * 0.3
    return env(out, attack=0.002, decay=0.06, release=0.08)


def success():
    d = 1.0
    out = np.zeros(int(RATE * d))
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        part = sine(f, d) * np.exp(-np.linspace(0, 5, len(out)))
        delay = int(RATE * 0.09 * i)
        out[delay:] += part[: len(out) - delay] * (0.9 - i * 0.12)
    return env(out, attack=0.005, decay=0.3, sustain=0.3, release=0.5)


def failure():
    d = 1.1
    n = int(RATE * d)
    sweep = np.sin(2 * np.pi * np.cumsum(np.linspace(420, 90, n)) / RATE)
    buzz = saw(70, d) * 0.35
    return env(sweep * 0.7 + buzz, attack=0.01, decay=0.4, sustain=0.35, release=0.45)


def sting_suspicion():
    """Short tense riser for "someone is on to you"."""
    d = 0.7
    n = int(RATE * d)
    riser = np.sin(2 * np.pi * np.cumsum(np.linspace(220, 520, n)) / RATE)
    shimmer = highpass(noise(d), 5000) * 0.15
    return env(riser * 0.8 + shimmer, attack=0.05, decay=0.2, sustain=0.6, release=0.3)


def sting_payoff():
    """A trap just went off somewhere. Bright, satisfying, short."""
    d = 0.8
    out = sine(392, d) * 0.5 + sine(587.33, d) * 0.4 + sine(784, d) * 0.3
    out *= np.exp(-np.linspace(0, 4.5, len(out)))
    return env(out, attack=0.003, decay=0.25, sustain=0.2, release=0.4)


def blip():
    d = 0.08
    return env(sine(1500, d), attack=0.001, decay=0.03, release=0.04)


def tension_loop():
    """
    A two-second bed that can be looped and faded in as suspicion rises.
    Low pulse plus a slow dissonant pad.
    """
    d = 2.0
    n = int(RATE * d)
    x = np.linspace(0, d, n, endpoint=False)

    pulse = np.zeros(n)
    for beat in range(4):
        start = int(n * beat / 4)
        hit = lowpass(noise(0.2), 140) * np.exp(-np.linspace(0, 8, int(RATE * 0.2)))
        pulse[start:start + len(hit)] += hit[: n - start]

    pad = (sine(110, d) * 0.4 + sine(116.5, d) * 0.3) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.5 * x))
    return normalise(pulse * 0.8 + pad * 0.5, peak=0.6)


SOUNDS = {
    "footstep": footstep,
    "bell": bell,
    "clink": clink,
    "crunch": crunch,
    "splash": splash,
    "slip": slip,
    "grumble": grumble,
    "shout": shout,
    "huh": huh,
    "door": door,
    "pickup": pickup,
    "success": success,
    "failure": failure,
    "sting_suspicion": sting_suspicion,
    "sting_payoff": sting_payoff,
    "blip": blip,
    "tension_loop": tension_loop,
}


if __name__ == "__main__":
    print("Generating audio into", OUT)
    for name, fn in SOUNDS.items():
        save(name, fn())
    print("done -", len(SOUNDS), "clips")
