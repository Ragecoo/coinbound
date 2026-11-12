"""Original deterministic 48-second stereo ambience; all oscillators/events wrap at the seam."""
from pathlib import Path
import wave
import numpy as np

RATE = 22050
SECONDS = 48
COUNT = RATE * SECONDS
rng = np.random.default_rng(721904)
t = np.arange(COUNT) / RATE
freq = np.fft.rfftfreq(COUNT, 1 / RATE)


def noise(low, high):
    spectrum = np.fft.rfft(rng.standard_normal(COUNT))
    envelope = (freq / max(low, 1)) ** 2 / (1 + (freq / max(low, 1)) ** 2)
    envelope *= np.exp(-(freq / high) ** 2)
    result = np.fft.irfft(spectrum * envelope, n=COUNT)
    return result / np.std(result)


stereo = np.zeros((COUNT, 2))
shared_air = noise(35, 240)
shared_water = noise(700, 3100)
for channel in range(2):
    breath = .75 + .15 * np.sin(2 * np.pi * t / 48 + channel * .6)
    breath += .1 * np.sin(2 * np.pi * t / 16 + 1.2)
    stereo[:, channel] = (shared_air * .019 + noise(70, 480) * .007) * breath
    stereo[:, channel] += shared_water * .004 + noise(900, 4400) * .002
    # Almost inaudible room resonance, with whole cycles over the loop.
    for hz, gain in [(55, .003), (82.5, .002), (110, .001)]:
        stereo[:, channel] += np.sin(2 * np.pi * hz * t + channel * .07) * gain * breath


def event(sample, time, pan, gain):
    indices = (int(time * RATE) + np.arange(len(sample))) % COUNT
    stereo[indices, 0] += sample * gain * np.sqrt((1 - pan) / 2)
    stereo[indices, 1] += sample * gain * np.sqrt((1 + pan) / 2)


# Sparse water drops with diffuse, dark echoes; no rhythmic repeating beat.
for moment in [2.7, 7.1, 13.8, 19.2, 25.6, 32.3, 39.5, 45.8]:
    dt = np.arange(int(RATE * .65)) / RATE
    base = rng.uniform(550, 900)
    drop = np.sin(2 * np.pi * (base * dt + 60 * (1 - np.exp(-dt * 25))))
    drop *= np.exp(-dt * 14) * (1 - np.exp(-dt * 500))
    pan = rng.uniform(-.6, .6)
    event(drop, moment, pan, .055)
    for delay, gain in [(.17, .016), (.33, .008), (.57, .004)]:
        event(drop, moment + delay, -pan, gain)

# Small candle ticks sit underneath the air, without sharp loud transients.
for _ in range(130):
    dt = np.arange(int(RATE * .028)) / RATE
    tick = rng.standard_normal(len(dt)) * np.exp(-dt * 220) * (1 - np.exp(-dt * 1400))
    event(tick, rng.uniform(0, SECONDS), rng.choice([-.72, .72]), rng.uniform(.004, .012))

# The periodic synthesis already wraps; a short splice removes the final sample discontinuity.
edge = 256
bridge = (stereo[0] + stereo[-1]) * .5
fade = np.linspace(0, 1, edge)[:, None]
stereo[:edge] = bridge * (1 - fade) + stereo[:edge] * fade
stereo[-edge:] = stereo[-edge:] * (1 - fade) + bridge * fade
pcm = np.round(np.clip(stereo, -1, 1) * 32767).astype('<i2')
assert np.max(np.abs(pcm[0].astype(int) - pcm[-1].astype(int))) == 0
out = Path(__file__).resolve().parents[1] / 'Assets/Coinbound/Resources/Audio/menu-ambience.wav'
out.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(out), 'wb') as stream:
    stream.setnchannels(2)
    stream.setsampwidth(2)
    stream.setframerate(RATE)
    stream.writeframes(pcm.tobytes())
print(f'{out}: {SECONDS}s stereo, peak {np.max(np.abs(stereo)):.3f}, seamless endpoints')
