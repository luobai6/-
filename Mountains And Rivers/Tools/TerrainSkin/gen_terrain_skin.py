#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
山河未尽 · 通用地块皮肤生成器
================================

为「地块本体」生成顶面 / 侧壁两张 2048² 皮肤，并按 TerrainTileSkin 的数学关系
离线复刻一遍渲染，出一张同机位的对照预览。

四条硬约束（来自 Assets/Art/Map/Terrain/Shaders/TerrainTileSkin.shader）：
  1. 采样是世界坐标 + 镜像重复。_WorldScale=4 → 一张图覆盖 4×4 格。
     镜像不需要画无缝边，但图案会被翻倍复制，所以不能有能认出来的大特征。
  2. 顶面与侧壁共用同一套世界尺寸 → 特征的世界大小一致，下面所有噪声周期都按
     "世界单位" 写，换分辨率 / 换 _WorldScale 时外观不变。
  3. 侧壁只吃到贴图竖直方向的 每级高/WorldScale = 0.5/4 = 1/8，
     所以层理必须按 0.2 世界单位一条来画，一条大岩层是看不到的。
  4. shader 没有接光照，贴图里的明度就是最终明度；侧壁还要再乘 _SideTint × _SideDarken，
     所以侧壁贴图不能自己烤暗。

美术规则：
  - 色板直接来自现有山体：浅石青底、青灰罩染、少量深石青、暖石过渡。
  - 只做低频晕染，不做能认出来的草丛、石块、砂砾或规则土层。
  - 顶面负责“安静且连续”，坡面负责“由暖石过渡到青灰”，不能抢过山体。
  - 当前色值是小样基线，最终以 Unity Game 视图中的山体对照为准继续调整。

用法：python gen_terrain_skin.py
"""

from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont

# ── 全局尺度 ────────────────────────────────────────────────────────
N = 2048                 # 贴图边长
WORLD_SCALE = 4.0        # 与材质 _WorldScale 必须一致：一张图覆盖 4×4 格
PIXELS_PER_UNIT = N / WORLD_SCALE

PROJECT = Path(__file__).resolve().parents[2]          # Mountains And Rivers
ASSETS = PROJECT / "Assets"
TEX_DIR = ASSETS / "Art" / "Map" / "Terrain" / "Textures"
OUT_DIR = Path(__file__).resolve().parent / "out"
REF_DIR = Path(__file__).resolve().parent / "reference"


def P(world_period):
    """把"世界单位周期"换成傅里叶域的 f0（整张图的周期数）。"""
    return WORLD_SCALE / world_period


# ── 调色板（sRGB 0~1，从现有山体提取）────────────────────────────────
GROUND_BASE = np.array([0.722, 0.698, 0.561])  # #B8B28F 浅石青灰地面
COOL_WASH = np.array([0.478, 0.553, 0.498])    # #7A8D7F 青灰罩染
DEEP_STONE = np.array([0.298, 0.412, 0.412])   # #4C6969 深石青
WARM_STONE = np.array([0.765, 0.702, 0.557])   # #C3B38E 暖石过渡
INK = np.array([0.239, 0.247, 0.196])          # #3D3F32 山体墨色，只作极少量压边
SIDE_BASE = WARM_STONE * 0.48 + COOL_WASH * 0.52


# ── 噪声 ────────────────────────────────────────────────────────────
def bandpass(n, f0, width, rng, aniso=(1.0, 1.0)):
    """周期性的带通噪声（傅里叶域构造，天然首尾相接）。

    f0 是「整张图里的周期数」，用 P(世界周期) 换算。
    aniso=(1, 4) 把能量压到低频 fy → 得到横向条纹。
    """
    fx = np.fft.fftfreq(n)[None, :]
    fy = np.fft.fftfreq(n)[:, None]
    r = np.sqrt((fx * aniso[0]) ** 2 + (fy * aniso[1]) ** 2)
    r = np.maximum(r, 1e-9)
    amp = np.exp(-0.5 * (np.log2(r / (f0 / n)) / width) ** 2)
    amp[0, 0] = 0.0
    spec = amp * np.exp(1j * rng.uniform(0.0, 2.0 * np.pi, (n, n)))
    idx = (-np.arange(n)) % n                      # Hermitian 对称 → 输出必为实数
    spec = 0.5 * (spec + np.conj(spec[np.ix_(idx, idx)]))
    field = np.fft.ifft2(spec).real
    return (field - field.mean()) / (field.std() + 1e-9)


def fbm(n, rng, world_period, octaves=5, decay=0.85, aniso=(1.0, 1.0)):
    """多倍频叠加：没有单一主导尺度，所以看不出"图案"，只有质地。"""
    out = np.zeros((n, n), np.float64)
    amp, total = 1.0, 0.0
    for i in range(octaves):
        f = P(world_period / (2.0 ** i))
        if f > n * 0.42:
            break
        out += amp * bandpass(n, f, 1.15, rng, aniso)
        total += amp
        amp *= decay
    return out / max(total, 1e-9)


def noise1d(n, f0, width, rng):
    f = np.fft.fftfreq(n)
    amp = np.exp(-0.5 * (np.log2(np.maximum(np.abs(f), 1e-9) / (f0 / n)) / width) ** 2)
    amp[0] = 0.0
    spec = amp * np.exp(1j * rng.uniform(0.0, 2.0 * np.pi, n))
    spec = 0.5 * (spec + np.conj(spec[(-np.arange(n)) % n]))
    v = np.fft.ifft(spec).real
    return (v - v.mean()) / (v.std() + 1e-9)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def wrap_erode(mask, r):
    """周期性腐蚀，用来取"水痕边缘"。"""
    out = mask.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r + 1:
                out = np.minimum(out, np.roll(np.roll(mask, dy, 0), dx, 1))
    return out


def wet_edge(mask, r=3, blur=2.0):
    e = np.clip(mask - wrap_erode(mask, r), 0.0, 1.0)
    img = Image.fromarray((e * 255.0).astype(np.uint8))
    img = img.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(img).astype(np.float64) / 255.0


def paper_grain(n, rng, amount=0.016):
    g = 0.55 * bandpass(n, P(0.030), 1.6, rng) + 0.45 * bandpass(n, P(0.060), 1.4, rng)
    return g * amount


def blend(base, color, alpha):
    """alpha 可以是标量或与画面同尺寸的权重图。"""
    a = np.asarray(alpha, np.float64)
    if a.ndim == 0:
        return base + (np.asarray(color) - base) * float(a)
    return base + (np.asarray(color) - base) * a[..., None]


def budget_luminance(rgb, mean_target, std_target, lo, hi):
    """把整体明度搬进美术预算区间：先对齐均值，再按目标标准差缩放起伏，最后硬夹。

    不能直接往 0.5 做对比收缩——那样会先把画面压到底部夹断，整张图变成一块平色。
    """
    lum = rgb.mean(axis=2, keepdims=True)
    dev = lum - lum.mean()
    scale = np.clip(std_target / max(float(dev.std()), 1e-6), 0.0, 6.0)
    target = np.clip(mean_target + dev * scale, lo, hi)
    return np.clip(rgb * (target / np.maximum(lum, 1e-6)), 0.0, 1.0)


def to_uint8(rgb):
    return (np.clip(rgb, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


# ── 顶面 ────────────────────────────────────────────────────────────
def make_top(seed=20261016):
    rng = np.random.default_rng(seed)

    broad = fbm(N, rng, 3.20, octaves=3, decay=0.72)       # 跨数格的大范围明暗
    cool_region = fbm(N, rng, 1.75, octaves=3, decay=0.74) # 青灰水墨罩染
    warm_region = fbm(N, rng, 1.10, octaves=3, decay=0.68) # 暖石留白
    deep_region = fbm(N, rng, 0.82, octaves=3, decay=0.65) # 极少量深石青
    paper = fbm(N, rng, 0.18, octaves=3, decay=0.62)       # 很轻的纸面起伏

    cool_mask = 0.28 * smoothstep(-0.45, 1.35, cool_region)
    cool_mask *= 0.72 + 0.28 * smoothstep(-1.0, 1.2, broad)
    warm_mask = 0.12 * smoothstep(-0.35, 1.45, warm_region)
    deep_mask = 0.065 * smoothstep(0.72, 1.85, deep_region) * cool_mask

    col = np.ones((N, N, 3), np.float64) * GROUND_BASE
    col = blend(col, COOL_WASH, cool_mask)
    col = blend(col, WARM_STONE, warm_mask)
    col = blend(col, DEEP_STONE, deep_mask)

    value = np.full((N, N), 0.646)
    value += 0.020 * broad
    value -= 0.025 * cool_mask
    value += 0.012 * warm_mask
    value += 0.010 * paper

    lum = col.mean(axis=2, keepdims=True)
    col *= (value[..., None] / np.maximum(lum, 1e-6))

    col += paper_grain(N, rng, 0.004)[..., None]
    return budget_luminance(col, mean_target=0.646, std_target=0.034, lo=0.54, hi=0.74)


# ── 侧壁 ────────────────────────────────────────────────────────────
def make_side(seed=20261017):
    rng = np.random.default_rng(seed)
    broad = fbm(N, rng, 2.40, octaves=3, decay=0.70, aniso=(1.0, 1.8))
    cool_region = fbm(N, rng, 1.10, octaves=3, decay=0.68, aniso=(1.0, 1.6))
    warm_region = fbm(N, rng, 1.45, octaves=2, decay=0.65, aniso=(1.0, 1.7))
    paper = fbm(N, rng, 0.20, octaves=3, decay=0.60)

    cool_mask = 0.34 * smoothstep(-0.55, 1.25, cool_region)
    warm_mask = 0.16 * smoothstep(-0.30, 1.35, warm_region)

    col = np.ones((N, N, 3), np.float64) * SIDE_BASE
    col = blend(col, COOL_WASH, cool_mask)
    col = blend(col, WARM_STONE, warm_mask)

    value = np.full((N, N), 0.604)
    value += 0.018 * broad
    value -= 0.018 * cool_mask
    value += 0.009 * warm_mask
    value += 0.008 * paper

    lum = col.mean(axis=2, keepdims=True)
    col *= (value[..., None] / np.maximum(lum, 1e-6))
    col += paper_grain(N, rng, 0.004)[..., None]
    return budget_luminance(col, mean_target=0.604, std_target=0.028, lo=0.52, hi=0.69)


# ── shader 数学的离线复刻 ────────────────────────────────────────────
def srgb_to_linear(c):
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92,
                    1.055 * np.clip(c, 0, 1) ** (1.0 / 2.4) - 0.055)


def mirror_repeat(uv):
    # 与 shader 的 MirrorRepeat 一致：frac() 对负数同样返回 [0,1)
    f = uv * 0.5 - np.floor(uv * 0.5)
    return 1.0 - np.abs(f * 2.0 - 1.0)


def sample_bilinear(tex, uv):
    h, w, _ = tex.shape
    x = (uv[..., 0] % 1.0) * w - 0.5
    y = (uv[..., 1] % 1.0) * h - 0.5
    x0 = np.floor(x).astype(np.int64)
    y0 = np.floor(y).astype(np.int64)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    x0 %= w
    y0 %= h
    x1 = (x0 + 1) % w
    y1 = (y0 + 1) % h
    c00, c10 = tex[y0, x0], tex[y0, x1]
    c01, c11 = tex[y1, x0], tex[y1, x1]
    return (c00 * (1 - fx) + c10 * fx) * (1 - fy) + (c01 * (1 - fx) + c11 * fx) * fy


TILES = [(0, 0, 0), (1, 0, 0), (2, 0, 0),
         (0, 1, 0), (1, 1, 0), (2, 1, 0),
         (0, 2, 0), (1, 2, 1), (2, 2, 2)]
TILE_SIZE, STEP, BASE_Y = 1.0, 0.5, -0.25
TOP_BLEND = (0.55, 0.85)
DEF_TINT = np.ones(3)
SUN_OFFSET = np.array([0.0, 5.55, -6.2])
BG = np.array([0.42, 0.42, 0.40], np.float64)


def render(tex_top, tex_side, side_tint, side_darken, cam_target, ortho_size, px=700, ss=2):
    top_lin = srgb_to_linear(tex_top).astype(np.float32)
    side_lin = srgb_to_linear(tex_side).astype(np.float32)
    side_tint_lin = srgb_to_linear(np.array(side_tint, np.float32))

    target = np.array(cam_target, np.float64)
    cam_pos = target + SUN_OFFSET
    fwd = target - cam_pos
    fwd /= np.linalg.norm(fwd)
    right = np.cross(np.array([0.0, 1.0, 0.0]), fwd)
    right /= np.linalg.norm(right)
    up = np.cross(fwd, right)

    res = px * ss
    gx, gy = np.meshgrid(np.linspace(-1, 1, res), np.linspace(1, -1, res))
    origin = (cam_pos[None, None, :]
              + right[None, None, :] * (gx * ortho_size)[..., None]
              + up[None, None, :] * (gy * ortho_size)[..., None])

    best_t = np.full((res, res), np.inf)
    best_axis = np.zeros((res, res), np.int8)

    for tx, tz, h in TILES:
        top_y = h * STEP
        center = np.array([tx * TILE_SIZE, (BASE_Y + top_y) * 0.5, tz * TILE_SIZE])
        half = np.array([TILE_SIZE * 0.5, (top_y - BASE_Y) * 0.5, TILE_SIZE * 0.5])
        lo, hi = center - half, center + half

        t_near = np.full((res, res), -np.inf)
        t_far = np.full((res, res), np.inf)
        axis = np.zeros((res, res), np.int8)
        miss = np.zeros((res, res), bool)
        for a in range(3):
            d = fwd[a]
            if abs(d) < 1e-9:
                # 射线在该轴上是常数：origin 必须落在板内，否则必然错过
                miss |= (origin[..., a] < lo[a]) | (origin[..., a] > hi[a])
                continue
            t1 = (lo[a] - origin[..., a]) / d
            t2 = (hi[a] - origin[..., a]) / d
            enter, exit_ = np.minimum(t1, t2), np.maximum(t1, t2)
            closer = enter > t_near
            t_near = np.where(closer, enter, t_near)
            axis = np.where(closer, a, axis)
            t_far = np.minimum(t_far, exit_)

        hit = (~miss) & (t_far >= np.maximum(t_near, 0.0)) & (t_near > 0.0) & (t_near < best_t)
        best_t = np.where(hit, t_near, best_t)
        best_axis = np.where(hit, axis, best_axis)

    hitmask = np.isfinite(best_t)
    t_safe = np.where(hitmask, best_t, 0.0)
    p = origin + fwd[None, None, :] * t_safe[..., None]

    normal = np.zeros((res, res, 3), np.float32)
    for a in range(3):
        normal[..., a] = np.where(best_axis == a, -np.sign(fwd[a]), 0.0)

    uv_top = mirror_repeat(p[..., [0, 2]] / WORLD_SCALE)
    side_xy = np.where((np.abs(normal[..., 0]) > np.abs(normal[..., 2]))[..., None],
                       p[..., [2, 1]], p[..., [0, 1]])
    uv_side = mirror_repeat(side_xy / WORLD_SCALE)

    c_top = sample_bilinear(top_lin, uv_top)
    c_side = sample_bilinear(side_lin, uv_side) * side_tint_lin * side_darken

    top_factor = smoothstep(TOP_BLEND[0], TOP_BLEND[1], np.abs(normal[..., 1]))[..., None]
    col_lin = (c_side + (c_top - c_side) * top_factor) * DEF_TINT
    img = np.where(hitmask[..., None], linear_to_srgb(col_lin), BG)
    return img.reshape(px, ss, px, ss, 3).mean(axis=(1, 3))     # 超采样后降采样


def label(img, text, sub=None):
    out = Image.fromarray(to_uint8(img))
    bar = Image.new("RGB", (out.width, 58), (26, 26, 26))
    d = ImageDraw.Draw(bar)
    try:
        f1 = ImageFont.load_default(size=24)
        f2 = ImageFont.load_default(size=17)
    except TypeError:
        f1 = f2 = ImageFont.load_default()
    d.text((16, 6), text, fill=(238, 238, 232), font=f1)
    if sub:
        d.text((16, 34), sub, fill=(158, 158, 150), font=f2)
    canvas = Image.new("RGB", (out.width, out.height + bar.height))
    canvas.paste(out, (0, 0))
    canvas.paste(bar, (0, out.height))
    return canvas


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    REF_DIR.mkdir(parents=True, exist_ok=True)

    top_path = TEX_DIR / "T_Terrain_Top_Ink_A.png"
    side_path = TEX_DIR / "T_Terrain_Side_Earth_A.png"

    # 对照用的"改前"必须取自留档，否则第二次运行会拿上一版自己的产物当原件
    ref_top = REF_DIR / "T_Terrain_Top_Ink_A_original.png"
    ref_side = REF_DIR / "T_Terrain_Side_Earth_A_original.png"
    for src, dst in ((top_path, ref_top), (side_path, ref_side)):
        if not dst.exists():
            Image.open(src).save(dst)

    old_top = np.asarray(Image.open(ref_top).convert("RGB")).astype(np.float64) / 255.0
    old_side = np.asarray(Image.open(ref_side).convert("RGB")).astype(np.float64) / 255.0

    new_top = make_top()
    new_side = make_side()

    Image.fromarray(to_uint8(new_top)).save(top_path, optimize=True)
    Image.fromarray(to_uint8(new_side)).save(side_path, optimize=True)
    Image.fromarray(to_uint8(new_top)).save(OUT_DIR / "T_Terrain_Top_Ink_A_preview.png")
    Image.fromarray(to_uint8(new_side)).save(OUT_DIR / "T_Terrain_Side_Earth_A_preview.png")

    OLD = (0.82, 0.80, 0.75), 0.72, 2.0      # 旧材质：_SideTint、_SideDarken、_WorldScale
    NEW = (1.0, 1.0, 1.0), 0.85, WORLD_SCALE
    TARGET = (1.0, 0.25, 1.0)
    NEAR = (2.15, 0.55, 2.05)

    def draw(maps, mat, cam, size):
        return render(maps[0], maps[1], mat[0], mat[1], cam, size)

    panels = [
        label(draw((old_top, old_side), OLD, TARGET, 2.8),
              "BEFORE   old maps / Side 0.59x / WorldScale 2",
              "3x3 tiles, ortho 42 deg, ortho size 2.8"),
        label(draw((new_top, new_side), NEW, TARGET, 2.8),
              "AFTER   cool-stone wash / Side 0.85x / WorldScale 4",
              "working palette, tune again after Unity comparison"),
        label(draw((old_top, old_side), OLD, NEAR, 1.10),
              "BEFORE   close-up", "black rock plates on every wall"),
        label(draw((new_top, new_side), NEW, NEAR, 1.10),
              "AFTER   close-up", "quiet wash, no regular strata or tile detail"),
    ]

    w, h = panels[0].size
    sheet = Image.new("RGB", (w * 2 + 24, h * 2 + 24), (12, 12, 12))
    for i, panel in enumerate(panels):
        sheet.paste(panel, ((i % 2) * (w + 24), (i // 2) * (h + 24)))
    sheet.save(OUT_DIR / "preview_before_after.png", optimize=True)

    for name, img in (("top", new_top), ("side", new_side)):
        lum = img.mean(axis=2)
        print(f"{name}: mean={img.mean(axis=(0, 1)).round(3)} "
              f"lum min/p50/p99/max={lum.min():.3f}/{np.median(lum):.3f}/"
              f"{np.percentile(lum, 99):.3f}/{lum.max():.3f} std={lum.std():.3f}")
    print("old: top lum p50=%.3f  side lum p50=%.3f"
          % (np.median(old_top.mean(axis=2)), np.median(old_side.mean(axis=2))))
    print("written:", top_path.name, side_path.name, "preview_before_after.png")


if __name__ == "__main__":
    main()
