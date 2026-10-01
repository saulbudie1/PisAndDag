import zlib, struct

W = H = 32
O = (28, 22, 38)
BLADE = {-2: (214, 226, 238), -1: (255, 255, 255), 0: (176, 192, 210), 1: (128, 146, 168), 2: (88, 102, 128)}
GOLD = [(255, 222, 110), (232, 176, 48), (176, 118, 24), (120, 72, 16)]
GRIP = [(122, 72, 44), (86, 48, 30)]
GEM = [(255, 110, 110), (190, 30, 50)]

px = [[None] * W for _ in range(H)]
for y in range(H):
    for x in range(W):
        t, s = x - y, x + y - 31  # t: along blade axis, s: across it
        c = None
        if -11 <= t <= 25 and abs(s) <= min(2, (26 - t) // 2):
            c = BLADE[s]
        elif t in (-12, -13) and abs(s) <= 9 and not (abs(s) == 9 and t == -12):
            if abs(s) <= 1 and (t, s) != (-12, 1):
                c = GEM[0] if s < 0 or t == -12 else GEM[1]
            else:
                c = GOLD[0] if s < -1 and t == -12 else GOLD[1] if s < 0 or t == -12 else GOLD[2]
        elif -21 <= t <= -14 and abs(s) <= 1:
            c = GRIP[1] if s == 1 or ((t - s) // 2) % 2 else GRIP[0]
        elif -25 <= t <= -22 and abs(s) <= 2 and not (abs(s) == 2 and t in (-25, -22)):
            c = GOLD[0] if (s, t) == (-1, -23) else GOLD[1] if s <= 0 else GOLD[2]
        px[y][x] = c

# auto outline
out = [row[:] for row in px]
for y in range(H):
    for x in range(W):
        if px[y][x] is None and any(0 <= x+dx < W and 0 <= y+dy < H and px[y+dy][x+dx]
                                    for dx, dy in ((1,0),(-1,0),(0,1),(0,-1))):
            out[y][x] = O

def png(path, scale):
    rows = b''
    for y in range(H * scale):
        rows += b'\0' + b''.join(bytes((*out[y//scale][x//scale], 255)) if out[y//scale][x//scale] else b'\0\0\0\0'
                                 for x in range(W * scale))
    chunk = lambda k, d: struct.pack('>I', len(d)) + k + d + struct.pack('>I', zlib.crc32(k + d))
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W*scale, H*scale, 8, 6, 0, 0, 0)) \
         + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(data)

png('out/sword_32x32.png', 1)
png('out/sword_preview_16x.png', 16)
