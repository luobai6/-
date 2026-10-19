"""从黑底瀑布视频中抽取透明 PNG 帧，并生成 Unity 序列帧图集。"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image


def 黑底转透明(图像: Image.Image) -> Image.Image:
    """根据黑底亮度恢复透明度，并尽量解除黑底预乘造成的暗边。"""
    rgb = np.asarray(图像.convert("RGB"), dtype=np.float32) / 255.0
    强度 = rgb.max(axis=2)
    alpha = np.clip((强度 - 0.012) / 0.32, 0.0, 1.0)
    alpha = alpha * alpha * (3.0 - 2.0 * alpha)
    非零 = np.maximum(alpha[..., None], 1.0 / 255.0)
    rgb = np.clip(rgb / 非零, 0.0, 1.0)
    rgba = np.dstack((rgb, alpha))
    return Image.fromarray(np.round(rgba * 255.0).astype(np.uint8), "RGBA")


def 主函数() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("视频", type=Path)
    parser.add_argument("输出目录", type=Path)
    parser.add_argument("前缀")
    parser.add_argument("--数量", type=int, required=True)
    parser.add_argument("--图集", type=Path, required=True)
    args = parser.parse_args()

    args.输出目录.mkdir(parents=True, exist_ok=True)
    args.图集.parent.mkdir(parents=True, exist_ok=True)
    # 放在项目目录内，避免 Windows 沙箱阻止 ffmpeg 写入系统临时目录。
    临时目录 = Path(tempfile.mkdtemp(prefix="shanhe_waterfall_", dir=args.输出目录))
    try:
        subprocess.run(
            ["ffmpeg", "-v", "error", "-i", str(args.视频), str(临时目录 / "%05d.png")],
            check=True,
        )
        原始帧 = sorted(临时目录.glob("*.png"))
        if len(原始帧) < args.数量:
            raise RuntimeError(f"视频只有 {len(原始帧)} 帧，无法抽取 {args.数量} 帧")

        选取序号 = np.linspace(0, len(原始帧) - 1, args.数量).round().astype(int)
        输出帧: list[Image.Image] = []
        for 输出序号, 原始序号 in enumerate(选取序号):
            with Image.open(原始帧[int(原始序号)]) as src:
                宽, 高 = src.size
                裁切宽 = 高 // 2
                左 = (宽 - 裁切宽) // 2
                画面 = src.crop((左, 0, 左 + 裁切宽, 高)).resize(
                    (256, 512), Image.Resampling.LANCZOS
                )
                透明画面 = 黑底转透明(画面)
                路径 = args.输出目录 / f"{args.前缀}_{输出序号:02d}.png"
                透明画面.save(路径)
                输出帧.append(透明画面)

        列数 = 8
        行数 = (len(输出帧) + 列数 - 1) // 列数
        图集 = Image.new("RGBA", (列数 * 256, 行数 * 512), (0, 0, 0, 0))
        for i, frame in enumerate(输出帧):
            图集.alpha_composite(frame, ((i % 列数) * 256, (i // 列数) * 512))
        图集.save(args.图集)
        print(f"源视频帧数={len(原始帧)}")
        print("抽取序号=" + ",".join(str(int(i)) for i in 选取序号))
        print(f"图集={args.图集} 尺寸={图集.size[0]}x{图集.size[1]}")
    finally:
        shutil.rmtree(临时目录, ignore_errors=True)


if __name__ == "__main__":
    主函数()
