"""Deterministically synthesize the Rebound Protocol slot UI sound family."""
import math
import os
import random
import struct
import wave

RATE = 48000
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio", "SlotMachine")


def env(t, duration, attack=0.006, release=0.05):
    if t < 0 or t > duration:
        return 0.0
    return min(1.0, t / max(attack, 1e-5)) * min(1.0, (duration-t) / max(release, 1e-5))


def synth(name, duration, voices, seed=31):
    """voices are callable(time, deterministic noise generator) components."""
    rng = random.Random(seed)
    n = round(RATE * duration)
    raw = [0.0] * n
    for i in range(n):
        t = i / RATE
        noise = rng.uniform(-1.0, 1.0)
        raw[i] = sum(v(t, noise) for v in voices)
    peak = max(abs(v) for v in raw) or 1.0
    gain = min(0.92 / peak, 3.0)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x*gain))*32767)) for x in raw))


def write_sequence_preview():
    """Render the exact current V2 cue order, retaining the approved old reel ticks."""
    cues = [
        ("slot_hover_tron_v2.wav", .00, .72*.42),
        ("slot_press_tron_v2.wav", .25, .72*.64),
        ("slot_spin_start_tron_v2.wav", .63, .72*.70),
        ("slot_reel_tick_01.wav", 1.10, .72*.48),
        ("slot_reel_tick_02.wav", 1.58, .72*.48),
        ("slot_reel_tick_03.wav", 2.06, .72*.48),
        ("slot_reel_stop_tron_v2_01.wav", 2.51, .72*.76),
        ("slot_reel_stop_tron_v2_02.wav", 2.93, .72*.76),
        ("slot_reel_stop_tron_v2_03.wav", 3.35, .72*.76),
        ("slot_roll_confirm_tron_v2.wav", 3.80, .72*.72),
        ("slot_special_reveal_tron_v2.wav", 4.52, .72*.72),
    ]
    items=[]
    total=0
    for filename, offset, level in cues:
        with wave.open(os.path.join(OUT,filename),"rb") as src:
            assert src.getframerate()==RATE and src.getnchannels()==1 and src.getsampwidth()==2
            samples=struct.unpack("<"+"h"*src.getnframes(),src.readframes(src.getnframes()))
        start=round(offset*RATE)
        items.append((start,samples,level))
        total=max(total,start+len(samples))
    output=[0.0]*total
    for start,samples,level in items:
        for i,value in enumerate(samples): output[start+i]+=value/32768*level
    os.makedirs(OUT,exist_ok=True)
    with wave.open(os.path.join(OUT,"slot_audio_cyber_v2_preview.wav"),"wb") as dst:
        dst.setnchannels(1); dst.setsampwidth(2); dst.setframerate(RATE)
        dst.writeframes(b"".join(struct.pack("<h",int(max(-1,min(1,x)) * 32767)) for x in output))


def ping(freq, duration, volume=0.3, decay=14, bend=0.0, start=0.0, partial=0.18):
    def voice(t, _):
        x = t - start
        if x < 0 or x > duration:
            return 0.0
        phase = 2*math.pi*(freq*x + bend*x*x/(2*duration))
        return volume*math.exp(-decay*x)*(math.sin(phase) + partial*math.sin(phase*2.01))
    return voice


def sweep(duration, lo, hi, volume=0.12, decay=5.0, start=0.0):
    def voice(t, _):
        x=t-start
        if x < 0 or x > duration:
            return 0.0
        f=lo*(hi/lo)**(x/duration)
        return volume*math.exp(-decay*x)*math.sin(2*math.pi*(lo*duration/math.log(hi/lo)*( (hi/lo)**(x/duration)-1) if hi != lo else lo*x))
    return voice


def noise_hit(start, duration, volume, brightness=0.78, release=0.025):
    last=[0.0]
    def voice(t, noise):
        x=t-start
        if x < 0 or x > duration:
            return 0.0
        # One-pole colour control: retain the soft machine air, tame high hiss.
        last[0] += brightness*(noise-last[0])
        return last[0]*volume*env(x,duration,0.001,release)
    return voice


def tron_bite(start, duration, root, volume=0.2, glide=0.5, fm_depth=65.0, decay=13.0):
    """Tight FM servo bite: dark, precise, metallic, never a soft bell."""
    def voice(t, _):
        x = t - start
        if x < 0 or x > duration:
            return 0.0
        f = root * (1 + glide * math.exp(-16 * x))
        mod = fm_depth * math.exp(-12*x) * math.sin(2*math.pi*(root*2.73)*x)
        phase = 2*math.pi*f*x + mod*x
        body = math.sin(phase) + .22*math.sin(phase*2.03) + .07*math.sin(phase*3.97)
        return volume * math.exp(-decay*x) * body
    return voice


def tron_sub(start, duration, volume=.25, start_hz=112, end_hz=42, decay=8.0):
    def voice(t, _):
        x=t-start
        if x < 0 or x > duration:
            return 0.0
        f=start_hz*(end_hz/start_hz)**(x/duration)
        phase=2*math.pi*start_hz*duration/math.log(start_hz/end_hz)*(1-(end_hz/start_hz)**(x/duration))
        return volume*math.exp(-decay*x)*(math.sin(phase)+.28*math.sin(phase*2.01))
    return voice


# Shared identity: glassy icy-cyan notes around a compact fifth-based pitch set,
# tiny amber click attacks, and a soft restrained low-mid electronic bed.
synth("slot_hover", 0.15, [ping(1174, .13, .25, 18, 300), ping(1760, .09, .09, 21, 170, .012)])
synth("slot_press", .23, [ping(659, .2, .3, 12, -240), ping(988, .16, .16, 14, -170, .018), ping(164, .12, .10, 22, -30, .005)])
synth("slot_spin_start", .42, [sweep(.38, 185, 820, .16, 2.2), ping(523, .22, .19, 13, 260, .035), noise_hit(.01,.23,.11,.32,.13), ping(262,.3,.09,8,-80,.02)])
for idx, note in enumerate((740, 880, 1047)):
    synth(f"slot_reel_tick_{idx+1:02d}", .11, [noise_hit(0,.045,.24,.22,.023), ping(note,.095,.2,32,80,.002,.1), ping(note*2,.06,.055,40,60,.002)])
def reel_lock(root):
    return [noise_hit(0,.05,.22,.28,.03), ping(root,.29,.34,9,-130,.004),
            ping(root*1.5,.22,.19,12,-160,.018), ping(146.8,.25,.12,15,-24,.003)]


synth("slot_reel_stop", .34, reel_lock(587))
for idx, note in enumerate((587, 659, 784)):
    synth(f"slot_reel_stop_{idx+1:02d}", .34, reel_lock(note), seed=31+idx)
synth("slot_roll_confirm", .60, [sweep(.52, 165, 660, .13, 2.8), ping(440,.43,.22,5,280,.07), ping(659,.37,.2,6,220,.12), noise_hit(.015,.3,.07,.20,.23)])
synth("slot_special_reveal", .92, [ping(523,.78,.23,2.5,90,.015), ping(659,.72,.21,3.1,100,.075), ping(784,.67,.2,3.4,80,.14), ping(1047,.59,.14,4,100,.23), sweep(.44,240,1047,.07,4,.02), noise_hit(.025,.13,.08,.16,.12)])

# V2 replaces the soft, cheerful menu-style cues with an original cybernetic,
# cinematic machine palette: dry transients, tense falling servos, dark bass,
# restrained electrical harmonics. Keep the existing reel tick WAVs unchanged.
synth("slot_hover_tron_v2", .082, [tron_bite(0,.07,1420,.20,.12,26,42), tron_bite(.012,.045,2130,.065,.08,32,55)])
synth("slot_press_tron_v2", .20, [tron_sub(0,.17,.35,146,48,13), noise_hit(0,.024,.26,.12,.016), tron_bite(.004,.15,515,.16,-.56,92,24), tron_bite(.008,.052,1780,.10,.08,45,67)])
synth("slot_spin_start_tron_v2", .46, [sweep(.42,830,118,.22,2.4), tron_bite(0,.36,214,.17,.8,110,7), tron_sub(.025,.32,.24,92,38,8), noise_hit(0,.28,.12,.14,.19), tron_bite(.045,.065,1640,.11,.22,50,42), tron_bite(.13,.055,1210,.075,.18,45,45)])
for idx, note in enumerate((470, 560, 665)):
    synth(f"slot_reel_stop_tron_v2_{idx+1:02d}", .205,
          [noise_hit(0,.032,.27,.16,.016), tron_bite(.002,.15,note,.32,.34,96,24),
           tron_bite(.012,.095,note*1.49,.12,.16,110,35), tron_sub(.002,.1,.13,72,45,24)], seed=56+idx)
synth("slot_roll_confirm_tron_v2", .48, [tron_sub(0,.42,.30,118,42,5.8), sweep(.39,210,760,.16,1.9), tron_bite(.015,.36,286,.21,.8,125,5.3), noise_hit(.015,.24,.12,.22,.13), tron_bite(.035,.075,1540,.12,.2,52,38)])
synth("slot_special_reveal_tron_v2", .88, [tron_sub(0,.75,.30,105,52,2.1),
      tron_bite(.04,.68,196,.22,.06,30,2.6), tron_bite(.14,.53,294,.16,.07,55,3.3),
      tron_bite(.25,.43,441,.13,.09,78,3.8), tron_bite(.39,.32,588,.095,.1,90,5.0),
      sweep(.42,148,880,.09,2.8,.06), noise_hit(.025,.12,.08,.18,.095)])
write_sequence_preview()
print("Wrote the original SFX set, 8 cybernetic V2 cues, and a listen-through preview to", os.path.abspath(OUT))
