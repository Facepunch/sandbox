"""
Renders the buggy's exhaust backfire pops: backfire_1.wav .. backfire_6.wav, picked at random by backfire.sound.
Placeholders until a sound designer records real ones - replace the wavs and keep the event.

    python generate_backfire.py

Each pop is a sharp crack (the flame front leaving the pipe), a low boom from the pipe ringing, and a short rumbling
tail of hot gas.
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter, sosfilt

RATE = 44100
HERE = os.path.dirname(os.path.abspath(__file__))


def bandpass(x, lo, hi, order=2):
    sos = butter(order, [lo, hi], btype='bandpass', fs=RATE, output='sos')
    return sosfilt(sos, x)


def lowpass(x, hz, order=2):
    sos = butter(order, hz, btype='lowpass', fs=RATE, output='sos')
    return sosfilt(sos, x)


def resonator(x, hz, decay):
    """A single damped resonance (the exhaust pipe ringing)."""
    r = np.exp(-1.0 / (decay * RATE))
    w = 2 * np.pi * hz / RATE
    return lfilter([1 - r], [1, -2 * r * np.cos(w), r * r], x)


def pop(rng, length=0.3):
    n = int(length * RATE)
    t = np.arange(n) / RATE
    noise = rng.standard_normal(n)

    # crack: a few milliseconds of bright noise
    crack_len = rng.uniform(0.002, 0.005)
    crack = bandpass(noise, 1500, 9000) * np.exp(-t / crack_len) * rng.uniform(0.7, 1.0)

    # boom: the pipe rings, kicked by a pressure pulse
    pulse = np.exp(-t / 0.0015) * (t < 0.01)
    boom = resonator(pulse, rng.uniform(65, 110), rng.uniform(0.03, 0.06))
    boom += 0.5 * resonator(pulse, rng.uniform(190, 260), rng.uniform(0.015, 0.03))
    boom /= np.abs(boom).max() + 1e-9

    # tail: rumbling gas, a bit of crackle in it
    tail = lowpass(noise, rng.uniform(500, 900)) * np.exp(-t / rng.uniform(0.04, 0.09))
    tail /= np.abs(tail).max() + 1e-9
    sparks = (rng.random(n) < 0.004) * rng.standard_normal(n) * np.exp(-t / 0.05)
    sparks = bandpass(sparks, 2000, 7000)

    x = 0.9 * crack + 1.0 * boom + 0.35 * tail + 0.25 * sparks

    # a click-free start and a smooth end
    x *= np.minimum(1, t / 0.0005) * np.clip((length - t) / 0.05, 0, 1)

    # a little saturation, then peak at -1 dB
    x = np.tanh(x * 1.5)
    return x / np.abs(x).max() * 10 ** (-1 / 20)


if __name__ == '__main__':
    rng = np.random.default_rng(1337)
    for i in range(1, 7):
        x = pop(rng, length=rng.uniform(0.22, 0.32))
        wavfile.write(os.path.join(HERE, f'backfire_{i}.wav'), RATE, (x * 32767).astype(np.int16))
        print(f'backfire_{i}.wav')
