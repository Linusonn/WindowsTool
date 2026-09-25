# -*- coding: utf-8 -*-
"""生成 WindowsTool 图标：深空圆角底 + 星点 + 青紫渐变电源符号(启停意象)

大尺寸带星点，<=32px 用无星点简化版（避免小图标糊成噪点）。
"""
import io
import struct

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
OUT = r"C:\Users\Linusonn\Documents\Project\WindowsTool\app.ico"

CYAN = np.array([34, 211, 238], np.float32)
PURPLE = np.array([167, 139, 250], np.float32)

# ---------- 背景：径向渐变 + 圆角 ----------
yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
cx, cy = S * 0.5, S * 0.44
dist = np.clip(np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (S * 0.72), 0, 1)[..., None]
bg = Image.fromarray(
    (np.array([18, 30, 56], np.float32) * (1 - dist)
     + np.array([3, 4, 10], np.float32) * dist).astype(np.uint8), "RGB").convert("RGBA")

mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.235), fill=255)
base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
base.paste(bg, (0, 0), mask)

# ---------- 星点 ----------
def add_stars(img):
    rng = np.random.default_rng(20260925)
    d = ImageDraw.Draw(img)
    tints = [(255, 255, 255), (34, 211, 238), (167, 139, 250), (240, 171, 252)]
    placed = 0
    while placed < 70:
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        if (x - S * 0.5) ** 2 + (y - S * 0.5) ** 2 < (S * 0.31) ** 2:
            continue
        if mask.getpixel((int(min(x, S - 1)), int(min(y, S - 1)))) < 128:
            continue
        r = rng.uniform(S * 0.004, S * 0.012)
        a = int(rng.uniform(90, 235))
        c = tints[rng.integers(0, len(tints))]
        d.ellipse([x - r, y - r, x + r, y + r], fill=(int(c[0]), int(c[1]), int(c[2]), a))
        placed += 1
    return img

# ---------- 电源符号 ----------
sym = Image.new("L", (S, S), 0)
sd = ImageDraw.Draw(sym)
w = int(S * 0.075)
sd.arc([S * 0.245, S * 0.265, S * 0.755, S * 0.775], start=-56, end=236, fill=255, width=w)
sd.rounded_rectangle([S * 0.5 - w * 0.35, S * 0.155, S * 0.5 + w * 0.35, S * 0.50],
                     radius=w * 0.35, fill=255)

# 青色光晕
glow_layer = Image.new("RGBA", (S, S), (34, 211, 238, 255))
glow_layer.putalpha(sym.filter(ImageFilter.GaussianBlur(S * 0.034)).point(lambda p: int(p * 0.45)))

# 青→紫渐变着色
diag = np.clip((np.arange(S)[:, None] * 0.45 + np.arange(S)[None, :] * 0.55) / (S - 1), 0, 1)
grad_img = Image.fromarray(
    (CYAN * (1 - diag[..., None]) + PURPLE * diag[..., None]).astype(np.uint8), "RGB").convert("RGBA")
grad_img.putalpha(sym)

# 边缘微光
edge = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(edge).rounded_rectangle([3, 3, S - 4, S - 4], radius=int(S * 0.232),
                                       outline=(70, 105, 150, 130), width=int(S * 0.007))


def compose(img):
    img = Image.alpha_composite(img, glow_layer)
    img = Image.alpha_composite(img, grad_img)
    return Image.alpha_composite(img, edge)


final_stars = compose(add_stars(base.copy()))
final_plain = compose(base.copy())

# ---------- 手写 ICO（不同尺寸用不同版本）----------
def build_ico(pairs, path):
    pngs = []
    for _size, im in pairs:
        b = io.BytesIO()
        im.save(b, format="PNG")
        pngs.append(b.getvalue())
    n = len(pngs)
    offset = 6 + 16 * n
    dirs = b""
    for (size, _im), png in zip(pairs, pngs):
        wh = 0 if size >= 256 else size
        dirs += struct.pack("<BBBBHHII", wh, wh, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    with open(path, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, n) + dirs + b"".join(pngs))


def rs(img, n):
    return img.resize((n, n), Image.LANCZOS)


build_ico([
    (256, rs(final_stars, 256)),
    (128, rs(final_stars, 128)),
    (64, rs(final_stars, 64)),
    (48, rs(final_stars, 48)),
    (32, rs(final_plain, 32)),
    (24, rs(final_plain, 24)),
    (16, rs(final_plain, 16)),
], OUT)

final_stars.resize((512, 512), Image.LANCZOS).save(OUT.replace(".ico", "_preview.png"))
rs(final_plain, 64).save(OUT.replace(".ico", "_small.png"))
print("saved:", OUT)
