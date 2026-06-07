# -*- coding: utf-8 -*-
"""
弹珠神罚 - UI素材程序化生成器
"""

import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = r"F:\Study\GameDesign\Ball\Assets\UI\Generated"
os.makedirs(OUT, exist_ok=True)

# 配色
BG_DARK   = (5, 10, 25, 220)
BG_SOLID  = (5, 10, 25, 255)
CYAN      = (0, 200, 255, 255)
CYAN_DIM  = (0, 140, 200, 255)
AMBER     = (255, 149, 0, 255)
AMBER_DIM = (200, 100, 0, 255)
RED       = (255, 34, 0, 255)
PURPLE    = (180, 60, 255, 255)
PURPLE_DIM= (120, 30, 200, 255)
GOLD      = (255, 215, 0, 255)
GOLD_DIM  = (200, 160, 0, 255)
WHITE     = (255, 255, 255, 255)
BLACK     = (0, 0, 0, 0)

def make_scanline_bg(w, h, alpha=30):
    bg = Image.new("RGBA", (w, h), (5, 10, 25, 200))
    draw = ImageDraw.Draw(bg)
    for y in range(0, h, 3):
        draw.line([(0, y), (w, y)], fill=(0, 200, 255, alpha))
    return bg

def draw_double_border(draw, x, y, w, h, color_outer, color_inner, outer_w=2, inner_w=1, gap=3):
    draw.rectangle([x, y, x+w-1, y+h-1], outline=color_outer, width=outer_w)
    draw.rectangle([x+gap, y+gap, x+w-1-gap, y+h-1-gap], outline=color_inner, width=inner_w)

def draw_corner(draw, x, y, arm_len, color, rotation):
    """rotation: 0=TL, 1=TR, 2=BL, 3=BR"""
    w = 3
    if rotation == 0:
        draw.rectangle([x, y, x+arm_len, y+w], fill=color)
        draw.rectangle([x, y, x+w, y+arm_len], fill=color)
        draw.rectangle([x, y, x+6, y+6], fill=WHITE)
        draw.rectangle([x+arm_len-12, y, x+arm_len, y+w], fill=color)
        draw.rectangle([x, y+arm_len-12, x+w, y+arm_len], fill=color)
    elif rotation == 1:
        draw.rectangle([x, y, x+arm_len, y+w], fill=color)
        draw.rectangle([x+arm_len-w, y, x+arm_len, y+arm_len], fill=color)
        draw.rectangle([x+arm_len-6, y, x+arm_len, y+6], fill=WHITE)
        draw.rectangle([x, y, x+12, y+w], fill=color)
        draw.rectangle([x+arm_len-w, y+arm_len-12, x+arm_len, y+arm_len], fill=color)
    elif rotation == 2:
        draw.rectangle([x, y+arm_len-w, x+arm_len, y+arm_len], fill=color)
        draw.rectangle([x, y, x+w, y+arm_len], fill=color)
        draw.rectangle([x, y+arm_len-6, x+6, y+arm_len], fill=WHITE)
        draw.rectangle([x+arm_len-12, y+arm_len-w, x+arm_len, y+arm_len], fill=color)
        draw.rectangle([x, y, x+w, y+12], fill=color)
    elif rotation == 3:
        draw.rectangle([x, y+arm_len-w, x+arm_len, y+arm_len], fill=color)
        draw.rectangle([x+arm_len-w, y, x+arm_len, y+arm_len], fill=color)
        draw.rectangle([x+arm_len-6, y+arm_len-6, x+arm_len, y+arm_len], fill=WHITE)
        draw.rectangle([x, y+arm_len-w, x+12, y+arm_len], fill=color)
        draw.rectangle([x+arm_len-w, y, x+arm_len, y+12], fill=color)

def draw_corners(draw, w, h, border, color, arm_len=36):
    draw_corner(draw, border, border, arm_len, color, 0)
    draw_corner(draw, w-border-arm_len, border, arm_len, color, 1)
    draw_corner(draw, border, h-border-arm_len, arm_len, color, 2)
    draw_corner(draw, w-border-arm_len, h-border-arm_len, arm_len, color, 3)

def draw_hex(draw, cx, cy, r, color):
    pts = []
    for i in range(6):
        angle = math.pi/6 + i * math.pi/3
        pts.append((cx + r*math.cos(angle), cy + r*math.sin(angle)))
    draw.polygon(pts, fill=color)

def draw_heart(draw, cx, cy, size, color, filled=True):
    s = size / 2
    if filled:
        draw.ellipse([cx-s, cy-s*0.7, cx, cy+s*0.3], fill=color)
        draw.ellipse([cx, cy-s*0.7, cx+s, cy+s*0.3], fill=color)
        draw.polygon([(cx-s, cy), (cx+s, cy), (cx, cy+s)], fill=color)
    else:
        draw.ellipse([cx-s, cy-s*0.7, cx, cy+s*0.3], outline=color, width=2)
        draw.ellipse([cx, cy-s*0.7, cx+s, cy+s*0.3], outline=color, width=2)
        draw.line([(cx-s+1, cy), (cx, cy+s-1)], fill=color, width=2)
        draw.line([(cx+s-1, cy), (cx, cy+s-1)], fill=color, width=2)

def save(img, name):
    img.save(os.path.join(OUT, name))
    print(f"  {name} ({img.width}x{img.height})")

print("="*60)
print("弹珠神罚 UI素材生成器")
print("="*60)

# ================================================================
# A. 面板类 (9-slice)
# ================================================================
print("\n[A] 面板类...")

# panel_main.png (1000x1500, 9-slice border 44px)
w, h, b = 1000, 1500, 44
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 20)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, CYAN, CYAN_DIM, 2, 1, 4)
draw_corners(draw, w, h, b, CYAN, 40)
draw_hex(draw, b+2, h//2, 8, CYAN)
draw_hex(draw, w-b-2, h//2, 8, CYAN)
save(img, "panel_main.png")

# panel_hud.png (360x110, 9-slice border 22px)
w, h, b = 360, 110, 22
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, CYAN, CYAN_DIM, 1, 1, 2)
draw_corners(draw, w, h, b, CYAN, 18)
draw.rectangle([b-8, h//2-22, b-2, h//2+22], fill=CYAN)
draw.rectangle([w-b+2, h//2-22, w-b+8, h//2+22], fill=CYAN)
save(img, "panel_hud.png")

# panel_card_common.png (320x480, 9-slice border 26px, blue)
w, h, b = 320, 480, 26
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, CYAN, CYAN_DIM, 2, 1, 3)
draw_corners(draw, w, h, b, CYAN, 24)
draw.line([(b+28, b+26), (w-b-28, b+26)], fill=CYAN_DIM, width=1)
save(img, "panel_card_common.png")

# panel_card_rare.png (purple)
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, PURPLE, PURPLE_DIM, 2, 1, 3)
draw_corners(draw, w, h, b, PURPLE, 24)
draw.line([(b+28, b+26), (w-b-28, b+26)], fill=PURPLE_DIM, width=1)
save(img, "panel_card_rare.png")

# panel_card_epic.png (gold)
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, GOLD, GOLD_DIM, 2, 1, 3)
draw_corners(draw, w, h, b, GOLD, 24)
diamond = [(w//2, b+4), (w//2+9, b+18), (w//2, b+32), (w//2-9, b+18)]
draw.polygon(diamond, fill=GOLD)
draw.line([(b+28, b+26), (w//2-19, b+26)], fill=GOLD_DIM, width=1)
draw.line([(w//2+19, b+26), (w-b-28, b+26)], fill=GOLD_DIM, width=1)
save(img, "panel_card_epic.png")

# panel_dialog.png (900x560, 9-slice border 32px)
w, h, b = 900, 560, 32
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, CYAN, CYAN_DIM, 2, 1, 4)
draw_corners(draw, w, h, b, CYAN, 30)
save(img, "panel_dialog.png")

# panel_result.png (1000x1400, 9-slice border 44px)
w, h, b = 1000, 1400, 44
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
scanline = make_scanline_bg(w-2*b, h-2*b, 18)
img.paste(scanline, (b, b), scanline)
draw_double_border(draw, b, b, w-2*b, h-2*b, CYAN, CYAN_DIM, 2, 1, 5)
draw_corners(draw, w, h, b, CYAN, 40)
draw.rectangle([b+40, b-3, w-b-40, b+6], fill=CYAN)
save(img, "panel_result.png")

# ================================================================
# B. 按钮类
# ================================================================
print("\n[B] 按钮类...")

# btn_flipper_left.png (440x250)
w, h = 440, 250
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
m = 15
pts = [(m, h-m), (w//4, m), (w-m, m), (w-m//2, h-m)]
glow_pts = [(x+8, y+8) for x, y in pts]
glow = Image.new("RGBA", (w+20, h+20), BLACK)
gd = ImageDraw.Draw(glow)
gd.polygon(glow_pts, fill=AMBER[:3]+(30,))
glow = glow.filter(ImageFilter.GaussianBlur(12))
img.paste(glow, (-10, -10), glow)
draw.polygon(pts, fill=(20,8,0,220))
draw.polygon(pts, outline=AMBER, width=2)
draw.line([(w//4+10, m+25), (w-m-10, m+25)], fill=AMBER, width=2)
save(img, "btn_flipper_left.png")

# btn_flipper_right.png
img_r = img.transpose(Image.FLIP_LEFT_RIGHT)
save(img_r, "btn_flipper_right.png")

# btn_circle.png (128x128)
w, h = 128, 128
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
cx, cy, r = w//2, h//2, 50
for g in range(8, 0, -1):
    a = int(50*(g/8))
    draw.ellipse([cx-r-g, cy-r-g, cx+r+g, cy+r+g], outline=AMBER[:3]+(a,), width=2)
draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=(20,8,0,200))
draw.ellipse([cx-r, cy-r, cx+r, cy+r], outline=AMBER, width=3)
draw.ellipse([cx-r+10, cy-r+10, cx+r-10, cy+r-10], outline=AMBER_DIM, width=1)
draw.ellipse([cx-4, cy-4, cx+4, cy+4], fill=AMBER)
save(img, "btn_circle.png")

# ================================================================
# C. 图标类
# ================================================================
print("\n[C] 图标类...")

# icon_heart_full.png (96x96)
w, h = 96, 96
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
glow_h = Image.new("RGBA", (w, h), BLACK)
gd = ImageDraw.Draw(glow_h)
draw_heart(gd, w//2, h//2-4, 48, AMBER[:3]+(40,))
glow_h = glow_h.filter(ImageFilter.GaussianBlur(8))
img.paste(glow_h, (0,0), glow_h)
draw_heart(draw, w//2, h//2-4, 48, AMBER)
save(img, "icon_heart_full.png")

# icon_heart_empty.png
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
draw_heart(draw, w//2, h//2-4, 48, AMBER_DIM, filled=False)
save(img, "icon_heart_empty.png")

# icon_skill_execute.png (128x128) - lightning bolt
w, h = 128, 128
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
bolt = [(w//2+2, 14), (w//2-18, h//2-10), (w//2+4, h//2-10), (w//2-20, h-14), (w//2+20, h//2+10), (w//2, h//2+10)]
glow_b = Image.new("RGBA", (w+16, h+16), BLACK)
gbd = ImageDraw.Draw(glow_b)
gb_pts = [(x+8, y+8) for x, y in bolt]
gbd.polygon(gb_pts, fill=CYAN[:3]+(35,))
glow_b = glow_b.filter(ImageFilter.GaussianBlur(8))
img.paste(glow_b, (-8, -8), glow_b)
draw.polygon(bolt, fill=CYAN[:3]+(80,))
draw.polygon(bolt, outline=CYAN, width=2)
save(img, "icon_skill_execute.png")

# icon_skill_shield.png (128x128)
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
cx, cy = w//2, h//2+6
shield = [(cx, cy-44), (cx+32, cy-30), (cx+28, cy-2), (cx+18, cy+22), (cx, cy+36), (cx-18, cy+22), (cx-28, cy-2), (cx-32, cy-30)]
draw.polygon(shield, fill=CYAN[:3]+(60,))
draw.polygon(shield, outline=CYAN, width=2)
draw.line([(cx, cy-18), (cx, cy+8)], fill=CYAN, width=3)
draw.line([(cx-12, cy-4), (cx, cy+8)], fill=CYAN, width=2)
draw.line([(cx+12, cy-4), (cx, cy+8)], fill=CYAN, width=2)
save(img, "icon_skill_shield.png")

# icon_star_full.png (64x64)
w, h = 64, 64
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
cx, cy = w//2, h//2
star = []
for i in range(5):
    a = -math.pi/2 + i*2*math.pi/5
    star.append((cx+22*math.cos(a), cy+22*math.sin(a)))
    a += math.pi/5
    star.append((cx+9*math.cos(a), cy+9*math.sin(a)))
draw.polygon(star, fill=GOLD)
draw.polygon(star, outline=WHITE[:3]+(150,), width=1)
save(img, "icon_star_full.png")

# icon_star_empty.png
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
draw.polygon(star, outline=GOLD_DIM, width=1)
save(img, "icon_star_empty.png")

# ================================================================
# D. 场景物件
# ================================================================
print("\n[D] 场景物件...")

# bumper_body.png (256x256)
w, h = 256, 256
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
cx, cy, r = w//2, h//2, 100
for g in range(12, 0, -2):
    a = int(40*(g/12))
    draw.ellipse([cx-r-g, cy-r-g, cx+r+g, cy+r+g], outline=CYAN[:3]+(a,), width=3)
draw.ellipse([cx-r, cy-r, cx+r, cy+r], outline=CYAN, width=3)
draw.ellipse([cx-r+16, cy-r+16, cx+r-16, cy+r-16], outline=CYAN_DIM, width=2)
draw.ellipse([cx-16, cy-16, cx+16, cy+16], fill=CYAN[:3]+(80,))
draw.ellipse([cx-16, cy-16, cx+16, cy+16], outline=CYAN, width=2)
draw.line([(cx, cy-r+20), (cx, cy+r-20)], fill=CYAN_DIM, width=1)
draw.line([(cx-r+20, cy), (cx+r-20, cy)], fill=CYAN_DIM, width=1)
for a in [0, math.pi/2, math.pi, 3*math.pi/2]:
    bx, by = cx+(r+3)*math.cos(a), cy+(r+3)*math.sin(a)
    tri = [(bx, by), (bx+8*math.cos(a+2.5), by+8*math.sin(a+2.5)), (bx+8*math.cos(a-2.5), by+8*math.sin(a-2.5))]
    draw.polygon(tri, fill=CYAN)
save(img, "bumper_body.png")

# flipper_left_body.png (540x110)
w, h = 540, 110
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
pts = [(20, h-15), (20, 15), (w-20, h//2-8), (w-20, h//2+8)]
glow_f = Image.new("RGBA", (w+20, h+20), BLACK)
gd = ImageDraw.Draw(glow_f)
gd.polygon([(x+10, y+10) for x, y in pts], fill=AMBER[:3]+(25,))
glow_f = glow_f.filter(ImageFilter.GaussianBlur(10))
img.paste(glow_f, (-10, -10), glow_f)
draw.polygon(pts, fill=(26,14,0,230))
draw.polygon(pts, outline=AMBER, width=2)
draw.line([(24, 20), (w-24, h//2-4)], fill=(255,180,80,200), width=3)
draw.ellipse([14, h//2-6, 26, h//2+6], fill=WHITE[:3]+(200,))
draw.ellipse([14, h//2-6, 26, h//2+6], outline=AMBER, width=1)
for gx in range(80, w-80, 60):
    draw.line([(gx, h//2-25), (gx, h//2+25)], fill=AMBER_DIM[:3]+(60,), width=1)
save(img, "flipper_left_body.png")

# flipper_right_body.png
save(img.transpose(Image.FLIP_LEFT_RIGHT), "flipper_right_body.png")

# ================================================================
# E. 结算文字
# ================================================================
print("\n[E] 结算文字...")

def draw_text(draw, text, w, h, glow_color, core_color):
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 130)
    except:
        font = ImageFont.load_default()
    for g in range(12, 0, -2):
        a = int(50*(g/12))
        gc = glow_color[:3] + (a,)
        draw.text((w//2+g%4-2, h//2+g%3-1), text, fill=gc, font=font, anchor="mm")
    draw.text((w//2, h//2), text, fill=core_color, font=font, anchor="mm")

w, h = 900, 280
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
draw_text(draw, "VICTORY", w, h, CYAN, WHITE)
save(img, "text_victory.png")

img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
draw_text(draw, "DEFEAT", w, h, RED, (255,180,150,255))
save(img, "text_defeat.png")

img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
draw_text(draw, "NEW RECORD!", w, h, GOLD, WHITE)
save(img, "text_new_record.png")

# ================================================================
# F. 装饰元素
# ================================================================
print("\n[F] 装饰元素...")

# corner_bracket.png (88x88)
w, h = 88, 88
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
arm = 60
for g in range(4, 0, -1):
    a = int(80*(g/4))
    gc = CYAN[:3]+(a,)
    draw.rectangle([0, 0, arm+g, 3], fill=gc)
    draw.rectangle([0, 0, 3, arm+g], fill=gc)
draw.rectangle([0, 0, arm, 3], fill=CYAN)
draw.rectangle([0, 0, 3, arm], fill=CYAN)
draw.rectangle([0, 0, 6, 6], fill=WHITE)
draw.rectangle([arm-12, 0, arm, 3], fill=CYAN)
draw.rectangle([0, arm-12, 3, arm], fill=CYAN)
save(img, "corner_bracket.png")

# arrow_chevron.png (64x64)
w, h = 64, 64
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
chevrons = [(20, CYAN_DIM, 2), (32, CYAN, 3)]
for ys, col, lw in chevrons:
    a_pts = [(w//2, ys+18), (w//2-12, ys), (w//2-12, ys+4), (w//2-2, ys+20), (w//2+2, ys+20), (w//2+12, ys+4), (w//2+12, ys)]
    draw.polygon(a_pts, fill=col)
save(img, "arrow_chevron.png")

# cd_ring.png (256x256)
w, h = 256, 256
img = Image.new("RGBA", (w, h), BLACK)
draw = ImageDraw.Draw(img)
cx, cy = w//2, h//2
for r in [110, 100]:
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], outline=CYAN_DIM[:3]+(180,), width=4)
draw.ellipse([cx-94, cy-94, cx+94, cy+94], fill=BG_SOLID[:3]+(200,))
for deg in range(0, 360, 30):
    a = math.radians(deg - 90)
    x1 = cx + 90*math.cos(a)
    y1 = cy + 90*math.sin(a)
    x2 = cx + 98*math.cos(a)
    y2 = cy + 98*math.sin(a)
    draw.line([(x1, y1), (x2, y2)], fill=CYAN_DIM, width=2)
draw.ellipse([cx-20, cy-20, cx+20, cy+20], fill=CYAN[:3]+(40,))
draw.ellipse([cx-20, cy-20, cx+20, cy+20], outline=CYAN_DIM, width=1)
save(img, "cd_ring.png")

# ================================================================
# 完成
# ================================================================
print("\n" + "="*60)
print("全部素材生成完毕!")
print("="*60)

for i, f in enumerate(sorted(os.listdir(OUT)), 1):
    fp = os.path.join(OUT, f)
    sz = os.path.getsize(fp)
    print(f"  [{i:2d}] {f} ({sz/1024:.1f} KB)")