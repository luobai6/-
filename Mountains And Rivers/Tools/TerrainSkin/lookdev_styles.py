#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
地块风格 look-dev：A / B / C 三条路线的同机位对照
=================================================

只出图，不改 Unity 资源。三种路线的差别被压缩成"顶面 + 侧壁怎么处理"这一件事，
因为现在的 TerrainTileSkin 只能按法线区分顶面和侧壁——这是现有 shader 能表达的边界，
超出这个范围的东西（网格线、装裱边框、投影）都还没做，图里也不会假装有。

  A 可读棋盘      顶面 = 现状皮肤，侧壁 = 同色族土层，靠约 15% 明度差读出台阶
  B 山水画卷      顶面 = 大尺度水洗，侧壁 = 直接复用顶面贴图（墙几乎消失在画里）
  C 装裱立体山水  顶面 = 与 A 相同，侧壁 = 宣纸/裱褙，台阶被强调成"画心下面的纸边"

用法：python lookdev_styles.py
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_terrain_skin as g      # noqa: E402  复用噪声、明度预算和 shader 复刻渲染

N = g.N
OUT = Path(__file__).resolve().parent / "out"
TEX = g.TEX_DIR


# ── B：大尺度水洗的顶面 ─────────────────────────────────────────────
def make_top_wash(seed=77001):
    rng = np.random.default_rng(seed)

    broad = g.fbm(N, rng, 3.20, octaves=3, decay=0.8)
    region = g.fbm(N, rng, 1.80, octaves=3, decay=0.9)
    mid = g.fbm(N, rng, 1.10, octaves=4, decay=0.85)
    veg = g.fbm(N, rng, 0.45, octaves=4, decay=0.92)
    fine = g.fbm(N, rng, 0.16, octaves=3, decay=0.8)

    damp = g.smoothstep(-0.55, 1.10, region)

    col = np.ones((N, N, 3), np.float64) * g.OCHRE
    col = g.blend(col, g.EARTH2, 0.30 * g.smoothstep(-1.0, 1.3, mid))
    moss = 0.52 * (0.20 + 0.80 * damp) * g.smoothstep(-0.70, 1.25, veg)
    col = g.blend(col, g.MOSS, moss)

    # 大块面为主，几乎没有高频：读成"一整幅画"而不是"一片地表"
    value = np.full((N, N), 0.680)
    value += 0.070 * broad
    value += 0.048 * mid
    value -= 0.045 * moss
    value -= 0.020 * damp
    value += 0.018 * fine

    lum = col.mean(axis=2, keepdims=True)
    col *= (value[..., None] / np.maximum(lum, 1e-6))
    return g.budget_luminance(col, mean_target=0.668, std_target=0.075, lo=0.40, hi=0.86)


# ── C：裱褙纸的侧壁 ─────────────────────────────────────────────────
def make_side_paper(seed=77002):
    rng = np.random.default_rng(seed)

    base = np.array([0.845, 0.800, 0.700])     # #D7CCB2 宣纸/裱褙的暖米白
    col = np.ones((N, N, 3), np.float64) * base
    col = g.blend(col, np.array([0.880, 0.845, 0.760]), 0.45)

    # 纸纤维：两个方向交叉的细纹，不形成任何形状
    fiber_a = g.bandpass(N, g.P(0.030), 1.6, rng, aniso=(1.0, 6.0))
    fiber_b = g.bandpass(N, g.P(0.045), 1.6, rng, aniso=(6.0, 1.0))
    mottle = g.fbm(N, rng, 0.90, octaves=3, decay=0.8)

    value = np.full((N, N), 0.822)
    value += 0.016 * mottle
    value += 0.010 * fiber_a
    value += 0.008 * fiber_b
    value += 0.006 * g.paper_grain(N, rng, 1.0)

    lum = col.mean(axis=2, keepdims=True)
    col *= (value[..., None] / np.maximum(lum, 1e-6))
    return g.budget_luminance(col, mean_target=0.822, std_target=0.014, lo=0.78, hi=0.88)


def make_side_silk(seed=77003):
    """古铜色裱绢：暗、闷、有织纹——装裱的另一种常见选择。"""
    rng = np.random.default_rng(seed)

    base = np.array([0.470, 0.385, 0.315])     # #786250 古铜绢
    col = np.ones((N, N, 3), np.float64) * base
    col = g.blend(col, np.array([0.510, 0.425, 0.350]), 0.40)

    weave_a = g.bandpass(N, g.P(0.022), 1.8, rng, aniso=(1.0, 8.0))
    weave_b = g.bandpass(N, g.P(0.022), 1.8, rng, aniso=(8.0, 1.0))
    mottle = g.fbm(N, rng, 0.70, octaves=4, decay=0.85)

    value = np.full((N, N), 0.482)
    value += 0.030 * mottle
    value += 0.012 * weave_a
    value += 0.012 * weave_b

    lum = col.mean(axis=2, keepdims=True)
    col *= (value[..., None] / np.maximum(lum, 1e-6))
    return g.budget_luminance(col, mean_target=0.482, std_target=0.026, lo=0.40, hi=0.58)


def main():
    OUT.mkdir(parents=True, exist_ok=True)

    top_a = np.asarray(Image.open(TEX / "T_Terrain_Top_Ink_A.png").convert("RGB")).astype(np.float64) / 255.0
    side_a = np.asarray(Image.open(TEX / "T_Terrain_Side_Earth_A.png").convert("RGB")).astype(np.float64) / 255.0

    top_b = make_top_wash()
    side_b = top_b                                   # 墙直接用顶面图：整幅画连续
    side_c = make_side_paper()
    side_c2 = make_side_silk()

    Image.fromarray(g.to_uint8(top_b)).save(OUT / "lookdev_top_wash.png")
    Image.fromarray(g.to_uint8(side_c)).save(OUT / "lookdev_side_paper.png")
    Image.fromarray(g.to_uint8(side_c2)).save(OUT / "lookdev_side_silk.png")

    FAR, NEAR = 2.8, 1.10
    TARGET_FAR, TARGET_NEAR = (1.0, 0.25, 1.0), (2.15, 0.55, 2.05)
    WHITE = (1.0, 1.0, 1.0)

    styles = [
        ("A  readable chessboard",
         "top: ink terrain / wall: same-family earth, ~15% darker",
         top_a, side_a, 0.85),
        ("B  scroll painting",
         "top: large washes / wall: reuses the top map, the step almost vanishes",
         top_b, side_b, 0.96),
        ("C1  mounted relief - paper",
         "top: ink terrain / wall: bright mounting paper",
         top_a, side_c, 1.00),
        ("C2  mounted relief - silk",
         "top: ink terrain / wall: dark bronze mounting silk",
         top_a, side_c2, 1.00),
    ]

    rows = []
    for title, sub, top, side, darken in styles:
        far = g.render(top, side, WHITE, darken, TARGET_FAR, FAR, px=520, ss=2)
        near = g.render(top, side, WHITE, darken, TARGET_NEAR, NEAR, px=520, ss=2)
        rows.append((far, near))

    panels_far, panels_near = [], []
    for (far, near), (title, sub, *_rest) in zip(rows, styles):
        panels_far.append(g.label(far, title, sub))
        panels_near.append(g.label(near, "close-up", ""))

    w, h = panels_far[0].size
    cols = len(panels_far)
    sheet = Image.new("RGB", (w * cols + 16 * (cols - 1), h * 2 + 20), (12, 12, 12))
    for c in range(cols):
        sheet.paste(panels_far[c], (c * (w + 16), 0))
        sheet.paste(panels_near[c], (c * (w + 16), h + 20))
    sheet.save(OUT / "lookdev_styles.png", optimize=True)

    for name, img in (("B top", top_b), ("C1 side", side_c), ("C2 side", side_c2)):
        lum = img.mean(axis=2)
        print(f"{name}: mean={img.mean(axis=(0, 1)).round(3)} "
              f"lum p50={np.median(lum):.3f} std={lum.std():.3f}")
    print("written:", OUT / "lookdev_styles.png")


if __name__ == "__main__":
    main()
