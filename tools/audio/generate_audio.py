#!/usr/bin/env python3
"""Procedural audio generator for Broomstick Bash (all original, no samples).

Main theme "Midnight Hop": A minor (harmonic-minor V), 4/4 swing (2:1 triplet feel),
128 BPM, 16 bars. Harpsichord comping, celesta lead, pizzicato counter-line, bassoon
oom-pah bass, brushed snare/woodblock.
Boss theme "Moonfang": E Phrygian, 7/8 (2+2+3) at 152 BPM (eighth = 456/min), 16 bars.
Driving low-string 8th ostinato, brass stabs, toms.
Stings: title (celesta run + harpsichord chord), game over (chromatic sag to Am),
victory (Picardy A major flourish).
Usage: python3 tools/audio/generate_audio.py [out_dir=Assets/Audio]  (numpy, scipy, ffmpeg+libvorbis)
"""
import os, sys, subprocess, tempfile
import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1031)
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio"
PEAK_DB = -2.5   # pre-encode ceiling; verify_audio.py checks decoded peaks <= -1.5 dBFS

def N(d): return int(d * SR)
def tt(n): return np.arange(n) / SR
def expd(n, tau): return np.exp(-np.arange(n) / (tau * SR))
def adsr(n, a, d, s, r):
    e = np.full(n, float(s)); a, d, r = N(a), N(d), min(N(r), n)
    a = min(a, n); e[:a] = np.linspace(0, 1, a, endpoint=False)
    dd = e[a:a+d]; e[a:a+d] = np.linspace(1, s, len(dd))
    if r: e[n-r:] *= np.linspace(1, 0, r)
    return e
def sos(kind, fc, o=2): return signal.butter(o, fc, kind, fs=SR, output='sos')
def lp(x, fc, o=2): return signal.sosfilt(sos('low', min(fc, SR*.45), o), x)
def hp(x, fc, o=2): return signal.sosfilt(sos('high', fc, o), x)
def bp(x, lo, hi, o=2): return signal.sosfilt(sos('band', [lo, min(hi, SR*.45)], o), x)
def noise(n): return rng.standard_normal(n)
def phase(f, n): return np.cumsum(np.broadcast_to(f, (n,)).astype(float)) / SR
def saw(f, n, off=0.): return 2*((phase(f, n)+off) % 1) - 1
def pulse(f, n, w=.5): return np.where((phase(f, n) % 1) < w, 1., -1.)
def sine(f, n): return np.sin(2*np.pi*phase(f, n))
def tvlp(x, cut, q=2):
    y = np.zeros_like(x); zi = None
    for i in range(0, len(x), 256):
        s = sos('low', float(np.clip(cut[i], 40, SR*.45)), q)
        if zi is None: zi = np.zeros((s.shape[0], 2))
        y[i:i+256], zi = signal.sosfilt(s, x[i:i+256], zi=zi)
    return y
def midi(m): return 440*2**((m-69)/12)

# ---------- instruments
def harpsichord(f, d, v=1.):
    n = N(d); x = saw(f, n) + .5*pulse(f*2, n, .3)
    x = tvlp(x, 1500 + 7000*expd(n, .05)) * expd(n, .35) * adsr(n, .001, 0, 1, .03)
    return x * v * .22
def celesta(f, d, v=1.):
    n = N(d); x = sine(f, n) + .3*sine(f*4, n)*expd(n, .08) + .15*sine(f*9.2, n)*expd(n, .03)
    return x * expd(n, .5) * adsr(n, .002, 0, 1, .05) * v * .3
def pizz(f, d, v=1.):
    n = N(d); x = tvlp(saw(f, n) + saw(f*1.004, n, .3), 400 + 3000*expd(n, .03))
    return x * expd(n, .12) * adsr(n, .002, 0, 1, .02) * v * .25
def bassoon(f, d, v=1.):
    n = N(d); vib = 1 + .003*np.sin(2*np.pi*5*tt(n))
    x = pulse(f*vib, n, .18) + .4*saw(f*vib, n)
    x = bp(x, 80, 1400) * adsr(n, .025, .08, .75, min(.06, d*.4))
    return x * v * .3
def strings(f, d, v=1.):
    n = N(d); x = sum(saw(f*(1+k), n, i*.21) for i, k in enumerate((-.005, 0, .006)))
    return tvlp(x, 500 + 1800*expd(n, .06)) * adsr(n, .006, .05, .7, min(.04, d*.3)) * v * .2
def brass(f, d, v=1., br=1.):
    n = N(d); x = sum(saw(f*(1+k), n, i*.17) for i, k in enumerate((-.006, 0, .007)))
    e = adsr(n, .025, .12, .7, min(.08, d*.4))
    return tvlp(x, 300 + br*4000*expd(n, .12) + 1000*e) * e * v * .22
def membrane(f0, d, f1=None, tau=.15, click=.3):
    n = N(d); f1 = f1 or f0*.6; f = f1 + (f0-f1)*expd(n, .03)
    return sine(f, n)*expd(n, tau) + click*lp(noise(n), 3000)*expd(n, .004)
def woodblock(f=900, d=.08):
    n = N(d); return (sine(f, n) + .5*sine(f*2.7, n)) * expd(n, .015)
def brush(d=.12, v=1.):
    n = N(d); return bp(noise(n), 2000, 9000) * expd(n, .03) * v * .35
def hat(d=.05):
    n = N(d); return hp(noise(n), 7000) * expd(n, .012) * .3

def reverb(x, wet=.2, dur=1.6, tau=.4, circular=False):
    n = N(dur); ir = lp(noise(n)*expd(n, tau), 5000); ir[:N(.01)] = 0
    ir /= np.sqrt(np.sum(ir**2)); ir *= .5
    if circular:
        y = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(ir, len(x))))
        return x*(1-wet) + 2*wet*y
    y = signal.fftconvolve(x, ir); o = np.zeros(len(y)); o[:len(x)] = x*(1-wet); return o + 2*wet*y

class Track:
    def __init__(s, dur): s.L = N(dur); s.L -= s.L % 2; s.b = np.zeros((s.L, 2))
    def put(s, x, pos, g=1., pan=0.):
        i = int(pos*SR); idx = (i + np.arange(len(x))) % s.L
        for c, gc in ((0, np.sqrt((1-pan)/2)), (1, np.sqrt((1+pan)/2))):
            np.add.at(s.b[:, c], idx, x*g*gc*1.414)
    def done(s, wet=.2):
        y = np.stack([reverb(s.b[:, c], wet, circular=True) for c in (0, 1)], 1)
        y -= y.mean(0); return y / np.abs(y).max() * 10**(PEAK_DB/20)

# ---------- music
def main_theme():
    bpm = 128; beat = 60/bpm; sw = beat*2/3          # swung "and"
    bars = 16; T = Track(bars*4*beat)
    # A minor: i  iv  V(E7)  i | VI  iv  V  i ...  (two-bar chords where marked)
    prog = [(57, 'm'), (62, 'm'), (52, '7'), (57, 'm'), (53, 'M'), (62, 'm'), (52, '7'), (57, 'm')] * 2
    ints = {'m': (0, 3, 7), 'M': (0, 4, 7), '7': (0, 4, 7, 10)}
    for b, (r, q) in enumerate(prog):
        s = b*4*beat
        for k in range(4):                                  # bassoon oom-pah, root/5th
            m = r - 24 + (0 if k % 2 == 0 else 7)
            T.put(bassoon(midi(m), beat*.55), s + k*beat, 1.0, -.2)
        for k in range(4):                                  # harpsichord on swung offbeats
            for j, iv in enumerate(ints[q]):
                T.put(harpsichord(midi(r + iv), .25), s + k*beat + sw + j*.006, .7, .3)
        for k in range(4):                                  # percussion
            T.put(woodblock(1100 if k % 2 else 800), s + k*beat, .25, .5)
            T.put(hat(), s + k*beat + sw, .6, .6)
            if k in (1, 3): T.put(brush(), s + k*beat, 1, -.3)
    # celesta lead: playful hopping minor motif, swung, phrase = 2 bars
    A = [(0, 76), (sw/beat, 72), (1, 69), (1.5+1/6, 71), (2, 72), (3, 76), (3+sw/beat, 75), (3.5+1/6, 76)]
    B = [(0, 77), (sw/beat, 76), (1, 74), (2, 72), (2+sw/beat, 71), (3, 68), (3.5+1/6, 69)]
    C = [(0, 72), (.5+1/6, 74), (1, 76), (2, 81), (2.5+1/6, 80), (3, 76), (3.5+1/6, 71)]
    D = [(0, 69), (1, 68), (1.5+1/6, 69), (2, 71), (3, 69)]
    phrases = [A, B, C, D, A, B, C, D]
    for p, ph in enumerate(phrases):
        for i, (pos, m) in enumerate(ph):
            nxt = ph[i+1][0] if i+1 < len(ph) else 4
            T.put(celesta(midi(m), max(.3, (nxt-pos)*beat*1.4)), p*8*beat + (p % 2)*0 + pos*beat*2, .9, .1)
    # pizzicato counter-line on second half (bars 8-15), answering a beat later
    for p in range(4, 8):
        for i, (pos, m) in enumerate(phrases[p]):
            T.put(pizz(midi(m-12), .3), p*8*beat + pos*beat*2 + beat, .7, -.4)
    return T.done(.2)

def boss_theme():
    e8 = 60/456; bar = 7*e8; bars = 16; T = Track(bars*bar)
    roots = [40, 40, 41, 40, 40, 40, 43, 41] * 2        # E E F E | E E G F (Phrygian b2 pull)
    groups = (0, 2, 4)                                   # 2+2+3 accents
    for b, r in enumerate(roots):
        s = b*bar
        for k in range(7):                               # low string ostinato
            m = r + (12 if k in (3, 6) else 0)
            T.put(strings(midi(m), e8*.9, 1.1 if k in groups else .7), s + k*e8, 1, -.2)
            T.put(strings(midi(m-12), e8*.9, .6), s + k*e8, 1, .2)
        for k in groups: T.put(membrane(95 if k == 0 else 140, .3, 55, .1, .4), s + k*e8, 1 if k == 0 else .6, (k-2)/4)
        T.put(membrane(70, .5, 40, .25, .5), s, .8)
        for k in range(7): T.put(hat(.04), s + k*e8, .5, .5)
        if b % 4 == 3:
            for k in range(7): T.put(membrane(220-18*k, .18, 120, .06, .3), s + k*e8, .5, (k-3)/4)
        # brass: stab chord on the '3' group, tritone-tinged
        if b % 2 == 0:
            for m in (r+12, r+15, r+19): T.put(brass(midi(m+12), e8*2.6, .5), s + 4*e8, 1, (m-r-15)/8)
        else:
            for m in (r+13, r+16, r+19): T.put(brass(midi(m+12), e8*1.8, .5), s + 2*e8, 1)
    # brass melody (bars 8-15): growling Phrygian line
    mel = [(0, 64, 4), (4, 65, 3), (7, 67, 2), (9, 65, 2), (11, 64, 3), (14, 62, 2), (16, 64, 5), (21, 71, 7),
           (28, 70, 4), (32, 67, 3), (35, 65, 4), (39, 64, 2), (41, 65, 3), (44, 64, 12)]
    for pos, m, d in mel:
        T.put(brass(midi(m), d*e8*.95, .8, 1.3), 8*bar + pos*e8, 1, .1)
        T.put(brass(midi(m-12), d*e8*.95, .4, .8), 8*bar + pos*e8, 1, -.1)
    return T.done(.15)

def finish_stereo(x, wet=.3, fout=.5):
    y = reverb(x, wet, 2.0, .5); y = y[np.argmax(np.abs(y) > 1e-4):]
    y = y - y.mean()
    a, b = N(.002), N(fout); y[:a] *= np.linspace(0, 1, a); y[-b:] *= np.linspace(1, 0, b)**2; y[0] = y[-1] = 0
    y = y / np.abs(y).max() * 10**(PEAK_DB/20)
    return np.stack([y, .97*y + .03*np.roll(y, 300)], 1)

def sting(kind):
    x = np.zeros(N(3.2))
    def add(s, p, g=1.):
        i = int(p*SR); e = min(len(x), i+len(s)); x[i:e] += s[:e-i]*g
    if kind == 'title':
        for i, m in enumerate((69, 72, 76, 79, 81, 84, 88)): add(celesta(midi(m), .8), i*.07)
        for m in (45, 57, 60, 64, 69): add(harpsichord(midi(m), 1.8), .55)
        add(bassoon(midi(33), 1.2), .55, .8); add(woodblock(), .55, .4)
    elif kind == 'gameover':
        for i, m in enumerate((64, 63, 62, 61)): add(bassoon(midi(m-12), .32), i*.3, .9); add(pizz(midi(m), .3), i*.3, .6)
        for m in (57, 60, 64): add(celesta(midi(m), 1.6), 1.2)
        add(bassoon(midi(33), 1.4), 1.2); add(membrane(70, .8, 40, .3), 1.2, .7)
    else:
        for i, m in enumerate((69, 71, 72, 74, 76, 78, 80)): add(pizz(midi(m), .2), i*.06, .8)
        for m in (57, 61, 64, 69, 73): add(harpsichord(midi(m), 1.8), .45); add(celesta(midi(m+12), 1.8), .45, .6)
        add(bassoon(midi(33), 1.2), .45); add(brush(.2), .45); add(woodblock(1200), .45, .5)
    return finish_stereo(x)

# ---------- sfx (mono)
def sweep_noise(n, f0, f1, lo=.5, hi=2.):
    c = f0 * (f1/f0)**(np.arange(n)/n); y = np.zeros(n); x = noise(n)
    return tvlp(hp(x, 200), c*hi) - tvlp(hp(x, 200), c*lo)*.7
def flap_whoosh():
    n = N(.38); x = sweep_noise(n, 400, 1600) * np.sin(np.pi*np.arange(n)/n)**2
    return x + .3*membrane(90, .38, 60, .05, 0)
def spell_cast():
    n = N(.6); f = 600*2**(2.2*np.arange(n)/n)
    x = sine(f, n)*.5 + .3*sine(f*1.5, n) + .25*hp(noise(n), 6000)*(np.sin(2*np.pi*24*tt(n)) > .3)
    return x * adsr(n, .01, .1, .7, .3)
def enemy_pop():
    n = N(.25); f = 900*expd(n, .03) + 200
    return sine(f, n)*expd(n, .06) + .5*bp(noise(n), 1000, 6000)*expd(n, .01)
def kid_sparkle():
    x = np.zeros(N(.9))
    for i, m in enumerate((84, 88, 91, 96, 91, 100)):
        s = celesta(midi(m), .5); i0 = N(i*.07); x[i0:i0+len(s)] += s[:len(x)-i0]
    n = len(x); return x + .1*hp(noise(n), 8000)*expd(n, .3)
def hit_ouch():
    # clearly synthetic "ow": square through two moving formant bandpasses
    n = N(.4); f = 260*(1 + .4*expd(n, .05)) * (1 - .25*np.arange(n)/n); src = pulse(f, n, .3)
    v = bp(src, 500, 900) + .6*bp(src, 900, 1500)*np.linspace(1, .2, n)
    return v*adsr(n, .01, .1, .7, .15) + .8*membrane(120, .4, 70, .05, .6)
def car_honk():
    n = N(.55); x = sum(pulse(f*(1+.002*np.sin(2*np.pi*6*tt(n))), n, .4) for f in (415, 523))
    return bp(x, 300, 3000) * adsr(n, .01, .05, .9, .05) * .5
def bat_screech():
    n = N(.35); f = 3200 + 1200*np.sin(2*np.pi*18*tt(n)) * np.linspace(1, .3, n)
    return (sine(f, n) + .3*saw(f/2, n)*.3) * adsr(n, .005, .05, .6, .15)
def ghost_wail():
    n = N(1.3); f = midi(67)*(1 + .25*np.sin(np.pi*np.arange(n)/n)) * (1 + .015*np.sin(2*np.pi*6*tt(n)))
    x = sine(f, n) + .4*sine(f*2.01, n) + .1*bp(noise(n), 800, 3000)
    return x * adsr(n, .25, .2, .8, .5) * .5
def werewolf_roar():
    n = N(1.4); f = 90*(1 + .3*np.sin(np.pi*np.arange(n)/n)); src = saw(f, n) + .5*saw(f*1.51, n)
    am = 1 + .5*np.sin(2*np.pi*28*tt(n))       # growl ring-mod: obviously synthetic
    x = (bp(src, 150, 700) + .6*bp(src, 900, 2200)) * am + .3*lp(noise(n), 1200)
    return x * adsr(n, .08, .2, .8, .5)
def fireball_throw():
    n = N(.5); x = sweep_noise(n, 300, 2500) * adsr(n, .02, .1, .8, .25)
    return x + .3*lp(noise(n), 500)*adsr(n, .01, .1, .5, .3)
def fireball_impact():
    n = N(.9); x = membrane(80, .9, 40, .2, 1.)
    x += .8*tvlp(noise(n), 4000*expd(n, .15) + 150)*expd(n, .25)
    x += .2*hp(noise(n), 3000)*expd(n, .4)*(rng.random(n) > .97)  # crackle
    return x
def boss_hurt():
    r = werewolf_roar()[:N(.5)] * adsr(N(.5), .005, .05, .7, .2)
    return r + .6*membrane(150, .5, 80, .06, .8)
def boss_defeat():
    n = N(2.2); f = 140*expd(n, 1.0) + 40; src = saw(f, n) * (1 + .5*np.sin(2*np.pi*22*tt(n)))
    x = bp(src, 100, 900) * adsr(n, .05, .3, .7, 1.0)
    x += .8*membrane(60, 2.2, 30, .5, .8)
    for i, m in enumerate((69, 73, 76, 81)):
        s = celesta(midi(m+12), .9); i0 = N(1.0 + i*.1); x[i0:i0+len(s)] += .6*s[:n-i0]
    return x
def ui_click():
    return woodblock(1400, .06)*.8 + .2*bp(noise(N(.06)), 3000, 8000)*expd(N(.06), .002)

# ---------- write
def edge(x, fout=.03):
    x = x - x.mean(); a, b = N(.002), N(fout)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b)**2; x[0] = x[-1] = 0; return x
def loud(x, target):
    x = x * target/np.sqrt(np.mean(x**2)); c = 10**(PEAK_DB/20); pk = np.abs(x).max()
    if pk > c: x = np.tanh(x/c*.9)/np.tanh(.9)*c
    return x
def write_ogg(path, x, q):
    os.makedirs(os.path.dirname(path), exist_ok=True); ch = 1 if x.ndim == 1 else 2
    with tempfile.NamedTemporaryFile(suffix='.f32', delete=False) as f: f.write(np.clip(x, -1, 1).astype('<f4').tobytes())
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'f32le', '-ar', str(SR), '-ac', str(ch), '-i', f.name,
                    '-c:a', 'libvorbis', '-q:a', str(q), path], check=True); os.unlink(f.name)

SFX = dict(flap_whoosh=flap_whoosh, spell_cast=spell_cast, enemy_pop=enemy_pop, kid_sparkle=kid_sparkle,
           hit_ouch=hit_ouch, car_honk=car_honk, bat_screech=bat_screech, ghost_wail=ghost_wail,
           werewolf_roar=werewolf_roar, fireball_throw=fireball_throw, fireball_impact=fireball_impact,
           boss_hurt=boss_hurt, boss_defeat=boss_defeat, ui_click=ui_click)
QUIET = {'ui_click': .07, 'flap_whoosh': .09, 'bat_screech': .09}

def main():
    write_ogg(f'{OUT}/Music/main_theme_loop.ogg', main_theme(), 4)
    write_ogg(f'{OUT}/Music/boss_theme_loop.ogg', boss_theme(), 4)
    for k in ('title', 'gameover', 'victory'): write_ogg(f'{OUT}/Music/{k}_sting.ogg', sting(k), 4)
    for name, fn in SFX.items():
        x = fn(); x = x[np.argmax(np.abs(x) > 1e-3):]
        x = edge(loud(edge(x), QUIET.get(name, .12)))
        write_ogg(f'{OUT}/SFX/{name}.ogg', x, 3)

if __name__ == '__main__': main()
