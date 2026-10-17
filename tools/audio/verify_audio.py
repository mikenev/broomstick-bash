#!/usr/bin/env python3
"""Decode every OGG under Assets/Audio; report duration/size/DC/peak/RMS, edges, and loop seams.
Exits 1 if any decoded peak > -1.5 dBFS or any *_loop seam jump exceeds the clip's p99 sample step."""
import glob, os, subprocess, sys, numpy as np
bad = 0
for p in sorted(glob.glob('Assets/Audio/**/*.ogg', recursive=True)):
    ch = int(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'stream=channels', '-of', 'csv=p=0', p]).split()[0])
    x = np.frombuffer(subprocess.check_output(['ffmpeg', '-v', 'error', '-i', p, '-f', 'f32le', '-']), '<f4').reshape(-1, ch)
    m = x.mean(1); pk = 20*np.log10(np.abs(x).max())
    s = f"{os.path.relpath(p, 'Assets/Audio'):30s} {len(x)/44100:6.3f}s {os.path.getsize(p)/1024:6.1f}KB ch={ch} dc={m.mean():+.5f} peak={pk:5.1f}dBFS rms={np.sqrt(np.mean(m**2)):.3f}"
    if pk > -1.5: bad = 1; s += ' PEAK!'
    if '_loop' in p:
        seam = np.abs(x[0]-x[-1]).max(); p99 = np.percentile(np.abs(np.diff(x, axis=0)), 99)
        s += f' seam={seam:.4f} p99step={p99:.4f}' + (' SEAM!' if seam > p99 else '')
        bad |= seam > p99
    else:
        s += f' first={np.abs(x[0]).max():.4f} last={np.abs(x[-1]).max():.4f}'
    print(s)
sys.exit(int(bad))
