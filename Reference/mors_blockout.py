#!/usr/bin/env python3
"""Mors block-out v2: proportions, palette, shading model. Paint over this."""
from PIL import Image

W, H = 26, 82
CX = 13

# Rule 5: dark ends land cool (purple-navy), light ends push warm/cream.
COAT  = [(40,38,50),(58,56,68),(78,77,88),(100,99,108),(124,123,128)]
SKIN  = [(96,88,92),(128,118,114),(160,150,142),(190,180,168),(210,201,186)]
BOOT  = [(30,27,36),(48,43,52),(66,60,64)]
LEATH = [(54,42,42),(80,64,56)]
HAIR  = [(40,37,44),(58,54,58)]
RAMPS = {'coat':COAT,'sleeve':COAT,'skin':SKIN,'boot':BOOT,'leather':LEATH,'hair':HAIR}

lab = [[None]*W for _ in range(H)]
def put(x,y,n):
    if 0 <= x < W and 0 <= y < H: lab[y][x] = n
def span(y,x0,x1,n):
    for x in range(x0,x1+1): put(x,y,n)

# ------------------------------------------------- silhouette
# ~8.5 heads tall instead of 7.5, and the extra length is all in the neck.

# head y3-13
for y,hw in {3:3,4:4,5:4,6:4,7:4,8:4,9:4,10:4,11:4,12:3,13:2}.items():
    span(y, CX-hw, CX+hw-1, 'skin')
for x,depth in {CX-4:7, CX-3:8, CX-2:6, CX-1:7, CX:6, CX+1:8, CX+2:7, CX+3:6}.items():
    for y in range(3, depth+1): put(x, y, 'hair')

# neck y14-18 — 5px where a person gets 2. The first thing that is wrong.
for y in range(14,19): span(y, CX-2, CX+1, 'skin')

# coat body y19-64: shoulders 16, nipped waist 12, hem 16
def body(y):
    if y < 24: return 4 + (y-19)                 # shoulder slopes over five rows
    if y < 34: return 8 - (y-24)//6
    if y < 50: return 6
    return 6 + (y-50)//7                      # flare to the hem
for y in range(19,65):
    b = body(y); span(y, CX-b, CX+b-1, 'coat')

for x,cut in {CX-6:2, CX-5:1, CX-1:1, CX+2:1, CX+4:2, CX+5:1}.items():
    for y in range(65-cut, 65): put(x, y, None)

# sleeves hang outside the body line, narrower, so the arms read
for y in range(21,55):
    b = body(y)
    span(y, CX-b-2, CX-b-1, 'sleeve'); span(y, CX+b, CX+b+1, 'sleeve')

# hands y55-62 — a full hand lower than they should be, and a surgeon's:
# long, narrow, one joint more than the job needs.
for y in range(55,63):
    b = body(y); w = 2 if y < 60 else 1
    span(y, CX-b-2, CX-b-3+w, 'skin'); span(y, CX+b, CX+b-1+w, 'skin')

# belt + sheaths y41-46. Six slots, four of them empty.
span(41, CX-7, CX+6, 'leather')
FULL = {-6, 1}
for sx in range(-6, 7, 3):
    for y in range(42, 48 if sx in FULL else 45):
        span(y, CX+sx, CX+sx+1, 'leather')

# legs y65-75
for y in range(65,76):
    span(y, CX-5, CX-2, 'coat'); span(y, CX+1, CX+4, 'coat')
# boots y74-81 — too small for the height, barely forward of the ankle
for y in range(74,82):
    span(y, CX-6, CX-2, 'boot'); span(y, CX+1, CX+5, 'boot')

# ------------------------------------------------- shading
# Rule 6: a bright band ALONG the figure, not a gradient across it.
LIT = 0.30          # lit band sits 30% across, light from the upper left
im = Image.new("RGBA",(W,H),(0,0,0,0)); px = im.load()
for y in range(H):
    xs = [x for x in range(W) if lab[y][x]]
    if not xs: continue
    lo, hi = min(xs), max(xs)
    for x in xs:
        n = lab[y][x]; r = RAMPS[n]
        t = (x - lo) / max(hi - lo, 1)
        # The lit side falls only part-way down the ramp, so its edge runs bright
        # into transparency (rule 2). The shadow side goes the whole way.
        d = (LIT - t) / LIT * 0.45 if t < LIT else (t - LIT) / (1 - LIT)
        i = round((len(r)-1) * (1 - d))
        if n == 'sleeve': i = max(i-1, 0)          # sleeves sit back from the body
        px[x,y] = r[max(0,min(i,len(r)-1))] + (255,)

# eyes: no iris ring, set a half-step too wide, focused past you
for ex in (CX-3, CX+2):
    px[ex,9] = SKIN[4]+(255,); px[ex,10] = (46,42,52,255)

# ------------------------------------------------- rim
# Rule 2: darken the SHADOW-side perimeter only, and snap to the ramp's own
# dark end rather than multiplying, so no new colours enter the palette.
out = im.copy(); o = out.load()
for y in range(H):
    for x in range(W):
        if px[x,y][3] == 0: continue
        xs = [k for k in range(W) if lab[y][k]]
        t = (x - min(xs)) / max(max(xs) - min(xs), 1)
        if t < 0.34: continue                      # lit edge stays bright
        if (x+1 >= W or px[x+1,y][3] == 0) or (y+1 >= H or px[x,y+1][3] == 0):
            o[x,y] = RAMPS[lab[y][x]][0] + (255,)
out.save("Mors_blockout.png")
print("wrote", out.size)
