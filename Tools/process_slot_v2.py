"""Process v2 hollow UI frames: black->alpha, trim, resize."""
from pathlib import Path
from PIL import Image
import numpy as np

OUT = Path(r"F:\Study\GameDesign\Ball\Assets\UI\Sprites\SlotMachine")
MASTER = Path(r"F:\Study\GameDesign\Ball\Assets\Art\UI\Atlas\Master")

JOBS = [
    (
        Path(r"C:\Users\cai\.cursor\projects\f-Study-GameDesign-Ball\assets\slot_panel_frame_v2.png"),
        "slot_panel_frame.png",
        (580, 720),
        28,  # black threshold
    ),
    (
        Path(r"C:\Users\cai\.cursor\projects\f-Study-GameDesign-Ball\assets\slot_reel_frame_v2.png"),
        "slot_reel_frame_rare.png",  # cyan base; tint via USS or reuse for all
        (160, 220),
        28,
    ),
    (
        Path(r"C:\Users\cai\.cursor\projects\f-Study-GameDesign-Ball\assets\slot_claim_btn_v2.png"),
        "btn_claim_n.png",
        (320, 100),
        28,
    ),
]


def punch_black(im: Image.Image, thresh: int) -> Image.Image:
    arr = np.array(im.convert("RGBA"))
    rgb = arr[:, :, :3].astype(np.int16)
    # near-black -> transparent; keep neon lines
    dark = (rgb.max(axis=2) < thresh) & (rgb.mean(axis=2) < thresh * 0.85)
    arr[dark, 3] = 0
    return Image.fromarray(arr)


def trim_pad(im: Image.Image, pad: int = 4) -> Image.Image:
    bbox = im.getbbox()
    if not bbox:
        return im
    x0, y0, x1, y1 = bbox
    x0, y0 = max(0, x0 - pad), max(0, y0 - pad)
    x1, y1 = min(im.width, x1 + pad), min(im.height, y1 + pad)
    return im.crop((x0, y0, x1, y1))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    MASTER.mkdir(parents=True, exist_ok=True)

    for src, name, size, thresh in JOBS:
        im = punch_black(Image.open(src), thresh)
        im = trim_pad(im, 6)
        # save master full-res
        master_path = MASTER / name.replace(".png", "_v2_master.png")
        im.save(master_path)
        out = im.resize(size, Image.Resampling.LANCZOS)
        out_path = OUT / name
        out.save(out_path)
        print(f"{name}: {im.size} -> {size} alpha={out.split()[-1].getextrema()}")

    # also copy reel as common/neutral variants (same art, USS can tint)
    rare = OUT / "slot_reel_frame_rare.png"
    for alt in (
        "slot_reel_frame_common.png",
        "slot_reel_frame_epic.png",
        "slot_reel_frame_mystery.png",
        "slot_reel_frame_neutral.png",
    ):
        Image.open(rare).save(OUT / alt)
        print(f"copied {alt}")


if __name__ == "__main__":
    main()
