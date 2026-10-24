# Self-authored looping "idle groove" VMD for MMDDeskPet (no third-party motion data).
# CC0 1.0 by Ewan Yuan. Sign conventions checked in this runtime:
# 左腕 rz<0 lowers, 右腕 rz>0 lowers; 左ひじ ry>0 / 右ひじ ry<0 bend forward.
#
# Uka's bind pose is an A-pose (arms about 33 degrees below horizontal). A groove
# whose arm keys only wobble by about 11 degrees leaves that silhouette in place.
# These offsets (about 32 degrees at the shoulder and 35 at the elbow) are what
# move Uka's arms, and they are ordinary local rotations on 左腕 / 右腕 / 左ひじ /
# 右ひじ, so a generic model still plays the same tracks.
#
# python3 tools/gen_vmd.py assets/default/motions/mmddeskpet_groove_20s.vmd 3
# The third argument is the number of 8-second loops. 3 loops is 24 seconds and
# meets itself, which is the file stored under the existing groove name.
import struct, math, sys
FPS = 30
BPM = 120
BEATS_LOOP = 16
STEP = 2
out = sys.argv[1] if len(sys.argv) > 1 else "groove.vmd"
LOOPS = int(sys.argv[2]) if len(sys.argv) > 2 else 3

def q(rx=0, ry=0, rz=0):
    rx, ry, rz = [math.radians(a) / 2 for a in (rx, ry, rz)]
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    return (sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz, cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz)

# 64-byte MMD bezier. Bytes 2 and 3 are the physics toggle, not a curve point.
# 0x63, 0x0F is the recognized kinematic value. 20, 20 is unknown and makes the
# loader warn on every key ("Unknown physics toggle info: 5140").
row = [20] * 8 + [107] * 8
interp = bytearray(sum([row[i:] + [0] * i for i in range(4)], []))
interp[2] = 0x63
interp[3] = 0x0F
interp = bytes(interp)

def name(s, n):
    b = s.encode("cp932")
    return b[:n].ljust(n, b"\0")

def smooth(x):
    x = max(0, min(1, x))
    return x * x * (3 - 2 * x)

N = int(LOOPS * BEATS_LOOP * 60 / BPM * FPS)
frames = []
for f in range(0, N + 1, STEP):
    t = f / FPS
    beat = t * BPM / 60
    b = beat * math.pi
    ph = beat % BEATS_LOOP
    wB = smooth((ph - 7) / 1.0) if ph < 12 else 1 - smooth((ph - 15) / 1.0)
    bounce = (1 + math.cos(2 * b)) / 2
    sw = math.sin(b)
    sw2 = math.sin(b + 0.6)
    k = [
        ("センター", (0.7 * sw, -0.55 * bounce, 0), q()),
        ("上半身", (0, 0, 0), q(rx=3 + 2 * bounce, ry=9 * sw2, rz=-5 * sw)),
        ("下半身", (0, 0, 0), q(ry=-4 * sw, rz=3 * sw)),
        ("首", (0, 0, 0), q(rz=3 * sw2)),
        ("頭", (0, 0, 0), q(rx=-3 + 4 * bounce, rz=7 * math.sin(b + 1.0))),
    ]
    # A: arms lowered and swinging, elbows bent. B: alternate arm raise.
    la = -32 + 10 * sw
    ra = 32 + 10 * sw
    le = 35 + 15 * bounce
    re = -(35 + 15 * bounce)
    upL = max(0, math.sin(b)) ** 2
    upR = max(0, -math.sin(b)) ** 2
    la = la * (1 - wB) + wB * (-30 + 40 * upL)
    ra = ra * (1 - wB) + wB * (30 - 40 * upR)
    le = le * (1 - wB) + wB * (30 + 40 * upL)
    re = re * (1 - wB) + wB * -(30 + 40 * upR)
    k += [
        ("左腕", (0, 0, 0), q(ry=-8 * sw, rz=la)),
        ("右腕", (0, 0, 0), q(ry=-8 * sw, rz=ra)),
        ("左ひじ", (0, 0, 0), q(ry=le)),
        ("右ひじ", (0, 0, 0), q(ry=re)),
    ]
    for n, p, r in k:
        frames.append((n, f, p, r))

morph = []
for f0 in range(40, N, 110):
    morph += [("まばたき", f0, 0.0), ("まばたき", f0 + 3, 1.0), ("まばたき", f0 + 6, 0.0)]
for f0 in range(0, N + 1, 30):
    morph += [("笑い", f0, 0.35)]

d = b"Vocaloid Motion Data 0002".ljust(30, b"\0") + name("MMDDeskPet demo", 20) + struct.pack("<I", len(frames))
for n, f, p, r in frames:
    d += name(n, 15) + struct.pack("<I3f4f", f, *p, *r) + interp
d += struct.pack("<I", len(morph))
for n, f, w in morph:
    d += name(n, 15) + struct.pack("<If", f, w)
d += struct.pack("<IIII", 0, 0, 0, 0)
open(out, "wb").write(d)
print(out, len(d), "bytes", N / FPS, "s")
