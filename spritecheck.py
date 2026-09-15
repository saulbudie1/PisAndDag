#!/usr/bin/env python3
"""spritecheck.py - measure a sprite against the house style. Usage: spritecheck.py FILE..."""
import sys, math
from collections import Counter
from PIL import Image

def lum(c): return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]

def check(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    p = im.load()
    op = lambda x, y: 0 <= x < w and 0 <= y < h and p[x, y][3] > 0

    px    = [p[x, y] for y in range(h) for x in range(w)]
    solid = [q for q in px if q[3] == 255]
    semi  = [q for q in px if 0 < q[3] < 255]
    cols  = Counter(q[:3] for q in solid)
    if not solid:
        print(f"{path}: empty"); return

    rim_dark = rim_tot = 0
    edge = Counter()
    for y in range(h):
        for x in range(w):
            if p[x, y][3] == 0: continue
            nb = [(x-1, y), (x+1, y), (x, y-1), (x, y+1)]
            if all(op(*n) for n in nb): continue
            edge[p[x, y][:3]] += 1
            inw = [p[nx, ny][:3] for nx, ny in nb if op(nx, ny)]
            if inw:
                rim_tot += 1
                if lum(p[x, y][:3]) < max(lum(c) for c in inw) - 6: rim_dark += 1

    pts = [(x, y) for y in range(h) for x in range(w) if p[x, y][3] > 0]
    n = len(pts)
    mx = sum(q[0] for q in pts) / n; my = sum(q[1] for q in pts) / n
    sxx = sum((q[0]-mx)**2 for q in pts)/n; syy = sum((q[1]-my)**2 for q in pts)/n
    sxy = sum((q[0]-mx)*(q[1]-my) for q in pts)/n
    th = 0.5 * math.atan2(2*sxy, sxx - syy)
    axis = -math.degrees(th)

    tr = sxx + syy; det = sxx*syy - sxy*sxy
    l1 = tr/2 + math.sqrt(max(tr*tr/4 - det, 0)); l2 = tr/2 - math.sqrt(max(tr*tr/4 - det, 0))
    elong = math.sqrt(l1/l2) if l2 > 1e-9 else 99

    ppx, ppy = -math.sin(th), -math.cos(th)
    proj = [(((x-mx)*ppx + (y-my)*ppy), lum(p[x, y][:3])) for x, y in pts]
    lo = min(d for d, _ in proj); hi = max(d for d, _ in proj)
    B = 7; bands = [[] for _ in range(B)]
    for d, L in proj:
        bands[min(int((d-lo)/(hi-lo+1e-9)*B), B-1)].append(L)
    bavg = [sum(b)/len(b) if b else 0 for b in bands]
    peak = bavg.index(max(bavg))

    srt = sorted(cols, key=lum)
    print(f"\n{path}  {w}x{h}")
    flag = lambda ok: "ok " if ok else "!! "
    print(f"  {flag(not semi)}alpha        {len(semi)} semi-transparent px (want 0)")
    print(f"  {flag(w%2==0 and h%2==0)}box          {w}x{h} (want both even)")
    print(f"  {flag(4<=len(cols)<=28)}palette      {len(cols)} colours (Calamity daggers: 11-17 typical, 4-28 range)")
    print(f"  {flag(0.15<=rim_dark/max(rim_tot,1)<=0.55)}rim          {rim_dark/max(rim_tot,1)*100:.0f}% of perimeter darker than interior (want 17-48%; 100% = black-outlined, wrong)")
    if elong >= 1.6:
        print(f"  {flag(min(abs(axis-41),abs(axis-48),abs(abs(axis)-90))<12)}axis         {axis:+.0f} deg, elongation {elong:.1f} (want ~+41..+48 diagonal, or +-90 vertical)")
        print(f"  {flag(1<=peak<=5)}spine        highlight peaks in band {peak} of 0-6 across the blade (want 1-5, ideally 2-4; 0 or 6 = flat-lit edge)")
        print(f"     bands        {' '.join(f'{a:.0f}' for a in bavg)}")
    else:
        print(f"     axis         n/a - elongation {elong:.1f}, too round for the axis/spine rules to mean anything")
    print(f"     fill         {len(pts)/(w*h)*100:.0f}% of box")
    print(f"     darkest      rgb{srt[0]}   brightest rgb{srt[-1]}")
    print(f"     edge colours {len(edge)} distinct (a single flat colour = black-outline look)")

for f in sys.argv[1:]:
    check(f)
