"""Builds the item models, textures and icons in ../assets.

Usage: python -I tools/make_assets.py [--preview <folder>]

Each item is a low-poly OBJ with one material that draws from one painted texture (<item>.png), so the wear and
detail live in the texture and the triangle count stays close to the game's own props. Every vertex carries an
explicit normal: shared normals shade rounded parts smoothly, split ones keep edges crisp. The players' faces use
the overlay's key layout and the pixel glyphs from src/Ui/PixelIcons.cs, so the item and the on-screen player match.
The inventory icon is rendered from the same mesh and texture. --preview also writes front, side, top and
three-quarter renders for checking part alignment. Units are metres. +Z is the front, +Y is up.
"""

import argparse
import math
import os
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "assets")
GLYPHS = os.path.join(HERE, "..", "src", "Ui", "PixelIcons.cs")
# The framework multiplies the texture by Kd; textures are painted to allow for it.
KD = 0.8
# The templates' _GlossMapScale (earmuffs 0.9, handheld console 0.933): game smoothness = texture alpha x this.
GLOSS_MAP_SCALE = 0.9


def unit(v):
    v = np.asarray(v, dtype=float)
    length = np.linalg.norm(v)
    return v / length if length > 0 else v


class Region:
    """A rectangle of the texture atlas, in pixels from the top-left."""

    def __init__(self, atlas, x, y, w, h):
        self.atlas, self.x, self.y, self.w, self.h = atlas, x, y, w, h

    def uv(self, s, t):
        return ((self.x + s * self.w) / self.atlas, 1.0 - (self.y + t * self.h) / self.atlas)

    def box(self, s0, t0, s1, t1):
        return (self.x + s0 * self.w, self.y + t0 * self.h, self.x + s1 * self.w, self.y + t1 * self.h)


class Mesh:
    def __init__(self):
        self.positions, self.uvs, self.normals, self.faces = [], [], [], []

    def vertex(self, position, uv, normal):
        self.positions.append(tuple(float(c) for c in position))
        self.uvs.append((float(uv[0]), float(uv[1])))
        self.normals.append(tuple(float(c) for c in unit(normal)))
        return len(self.positions) - 1

    def tri(self, a, b, c):
        self.faces.append((a, b, c))

    def quad(self, a, b, c, d):
        self.tri(a, b, c)
        self.tri(a, c, d)

    def inverted(self):
        """Faces whose winding disagrees with their normals; the game culls and lights by these."""
        count = 0
        for a, b, c in self.faces:
            p = [np.array(self.positions[i]) for i in (a, b, c)]
            facing = np.cross(p[1] - p[0], p[2] - p[0])
            if np.linalg.norm(facing) > 1e-12 and facing @ np.sum([self.normals[i] for i in (a, b, c)], axis=0) <= 0:
                count += 1
        return count

    def write(self, name):
        with open(os.path.join(ASSETS, name + ".mtl"), "w", newline="\n") as f:
            f.write("newmtl body\nKd %.2f %.2f %.2f\nmap_Kd %s.png\n" % (KD, KD, KD, name))
        with open(os.path.join(ASSETS, name + ".obj"), "w", newline="\n") as f:
            f.write("mtllib %s.mtl\n" % name)
            for v in self.positions:
                f.write("v %.5f %.5f %.5f\n" % v)
            for t in self.uvs:
                f.write("vt %.5f %.5f\n" % t)
            for n in self.normals:
                f.write("vn %.4f %.4f %.4f\n" % n)
            f.write("usemtl body\n")
            for face in self.faces:
                f.write("f " + " ".join("%d/%d/%d" % (i + 1, i + 1, i + 1) for i in face) + "\n")


# ------------------------------------------------------------------------------------------- geometry

def rounded_rect(w, h, r, segments, center=(0.0, 0.0)):
    """Counter-clockwise outline points with their exact outward normals."""
    cx, cy = center
    points = []
    for x, y, start in [(w / 2 - r, -h / 2 + r, -90), (w / 2 - r, h / 2 - r, 0),
                        (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180)]:
        for i in range(segments + 1):
            a = math.radians(start + 90 * i / segments)
            points.append(((cx + x + r * math.cos(a), cy + y + r * math.sin(a)), (math.cos(a), math.sin(a))))
    return points


def slab(mesh, outline, z0, z1, bevel, front, back, side, back_cap=True):
    """A rounded outline extruded along Z with bevelled faces. front and back map (x, y) to UV; side maps
    (perimeter fraction, depth fraction). Without back_cap the walls run down to z0, for parts set into a face."""
    n = len(outline)
    inner = [((p[0] - nx * bevel * 1.4, p[1] - ny * bevel * 1.4), (nx, ny)) for p, (nx, ny) in outline]
    center = np.mean([p for p, _ in outline], axis=0)

    def face(z_cap, z_ring, uv, sign):
        middle = mesh.vertex((center[0], center[1], z_cap), uv(*center), (0, 0, sign))
        ring = [mesh.vertex((p[0], p[1], z_cap), uv(*p), (0, 0, sign)) for p, _ in inner]
        # Bevel rows share one 45-degree normal per corner, so corners stay round and edges catch light.
        top = [mesh.vertex((p[0], p[1], z_cap), uv(*p), (nx, ny, sign)) for p, (nx, ny) in inner]
        low = [mesh.vertex((p[0], p[1], z_ring), uv(*p), (nx, ny, sign)) for p, (nx, ny) in outline]
        for i in range(n):
            j = (i + 1) % n
            if sign > 0:
                mesh.tri(middle, ring[i], ring[j])
                mesh.quad(low[i], low[j], top[j], top[i])
            else:
                mesh.tri(middle, ring[j], ring[i])
                mesh.quad(low[j], low[i], top[i], top[j])

    face(z1, z1 - bevel, front, 1)
    if back_cap:
        face(z0, z0 + bevel, back, -1)
    top_z, low_z = z1 - bevel, (z0 + bevel if back_cap else z0)
    lengths = [0.0]
    for i in range(n):
        lengths.append(lengths[-1] + math.dist(outline[i][0], outline[(i + 1) % n][0]))
    tops, lows = [], []
    for k in range(n + 1):
        (x, y), (nx, ny) = outline[k % n]
        s = lengths[k] / lengths[-1]
        tops.append(mesh.vertex((x, y, top_z), side(s, 0.0), (nx, ny, 0)))
        lows.append(mesh.vertex((x, y, low_z), side(s, 1.0), (nx, ny, 0)))
    for k in range(n):
        mesh.quad(lows[k], lows[k + 1], tops[k + 1], tops[k])


def frame(axis):
    w = unit(axis)
    helper = np.array([0.0, 1.0, 0.0]) if abs(w[1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    u = unit(np.cross(helper, w))
    return u, np.cross(w, u), w


def _profile_rings(profile):
    """Expands (r, a[, 'hard']) points into rings of (r, a, radial normal, axial normal, t). A hard point
    becomes two rings so the surface creases there."""
    points = [(p[0], p[1]) for p in profile]
    lengths = [0.0]
    for i in range(1, len(points)):
        lengths.append(lengths[-1] + math.dist(points[i - 1], points[i]))
    normals = []
    for i in range(len(points) - 1):
        dr, da = points[i + 1][0] - points[i][0], points[i + 1][1] - points[i][1]
        normals.append(unit((da, -dr)))
    rings = []
    for i, p in enumerate(profile):
        t = lengths[i] / lengths[-1]
        before = normals[i - 1] if i > 0 else None
        after = normals[i] if i < len(normals) else None
        if len(p) > 2 and before is not None and after is not None:
            rings.append((p[0], p[1], before, t, False))
            rings.append((p[0], p[1], after, t, True))
        else:
            n = unit((before if before is not None else 0) + (after if after is not None else 0))
            rings.append((p[0], p[1], n, t, False))
    return rings


def lathe(mesh, center, axis, profile, segments, region, scale=(1.0, 1.0)):
    """Revolves a profile of (radius, distance along axis) points. Profiles run along the axis with the
    outside on the left; a radius of zero closes the shape at a single shared vertex."""
    u, v, w = frame(axis)
    c = np.asarray(center, dtype=float)
    rings = []
    for r, a, (nr, na), t, _ in _profile_rings(profile):
        if r == 0:
            pole = mesh.vertex(c + w * a, region.uv(0.5, t), w * (1 if na >= 0 else -1))
            rings.append([pole] * (segments + 1))
            continue
        ring = []
        for i in range(segments + 1):
            th = 2 * math.pi * i / segments
            radial = u * math.cos(th) * scale[0] + v * math.sin(th) * scale[1]
            normal_radial = u * math.cos(th) / scale[0] + v * math.sin(th) / scale[1]
            ring.append(mesh.vertex(c + w * a + radial * r, region.uv(i / segments, t), unit(normal_radial) * nr + w * na))
        rings.append(ring)
    expanded = _profile_rings(profile)
    for j in range(len(rings) - 1):
        if expanded[j + 1][4]:
            continue  # the two rings of one hard point
        for i in range(segments):
            a, b, c2, d = rings[j][i], rings[j][i + 1], rings[j + 1][i + 1], rings[j + 1][i]
            if a == b:
                mesh.tri(a, c2, d)
            elif c2 == d:
                mesh.tri(a, b, c2)
            else:
                mesh.quad(a, b, c2, d)


def sweep(mesh, path, section, region, caps=True):
    """Sweeps a closed counter-clockwise section of (point, normal) pairs along a path, with
    parallel-transported frames. region maps (around, along)."""
    path = [np.asarray(p, dtype=float) for p in path]
    tangents = []
    for i in range(len(path)):
        a, b = path[max(0, i - 1)], path[min(len(path) - 1, i + 1)]
        tangents.append(unit(b - a))
    normal = unit(np.cross(tangents[0], [0, 0, 1])) if abs(tangents[0][2]) < 0.9 else unit(np.cross(tangents[0], [0, 1, 0]))
    lengths = [0.0]
    for i in range(1, len(path)):
        lengths.append(lengths[-1] + np.linalg.norm(path[i] - path[i - 1]))
    rings, frames = [], []
    for i, p in enumerate(path):
        if i > 0:
            normal = unit(normal - tangents[i] * (normal @ tangents[i]))
        binormal = np.cross(tangents[i], normal)
        frames.append((normal, binormal))
        ring = []
        count = len(section)
        for k in range(count + 1):
            (x, y), (nx, ny) = section[k % count]
            ring.append(mesh.vertex(p + normal * x + binormal * y, region.uv(k / count, lengths[i] / lengths[-1]),
                                    normal * nx + binormal * ny))
        rings.append(ring)
    for j in range(len(path) - 1):
        for k in range(len(section)):
            mesh.quad(rings[j][k], rings[j][k + 1], rings[j + 1][k + 1], rings[j + 1][k])
    if caps:
        for end, sign in ((0, -1), (len(path) - 1, 1)):
            normal, binormal = frames[end]
            facing = tangents[end] * sign
            middle = mesh.vertex(path[end], region.uv(0.5, end / max(1, len(path) - 1)), facing)
            ring = [mesh.vertex(path[end] + normal * x + binormal * y, region.uv(0.5, end / max(1, len(path) - 1)), facing)
                    for (x, y), _ in section]
            for k in range(len(section)):
                l = (k + 1) % len(section)
                mesh.tri(middle, ring[l], ring[k]) if sign < 0 else mesh.tri(middle, ring[k], ring[l])


def curve(points, steps=3):
    """A Catmull-Rom path through the given points, for cables that bend instead of kinking."""
    p = [np.asarray(points[0], float)] + [np.asarray(q, float) for q in points] + [np.asarray(points[-1], float)]
    out = []
    for i in range(1, len(p) - 2):
        for k in range(steps):
            t = k / steps
            out.append(0.5 * (2 * p[i] + (p[i + 1] - p[i - 1]) * t + (2 * p[i - 1] - 5 * p[i] + 4 * p[i + 1] - p[i + 2]) * t * t
                              + (3 * p[i] - p[i - 1] - 3 * p[i + 1] + p[i + 2]) * t ** 3))
    out.append(p[-2])
    return out


def circle(radius, sides):
    return [((radius * math.cos(2 * math.pi * k / sides), radius * math.sin(2 * math.pi * k / sides)),
             (math.cos(2 * math.pi * k / sides), math.sin(2 * math.pi * k / sides))) for k in range(sides)]


def arc(center, radius, start, end, steps, z=0.0):
    return [(center[0] + radius * math.cos(math.radians(start + (end - start) * i / steps)),
             center[1] + radius * math.sin(math.radians(start + (end - start) * i / steps)), z) for i in range(steps + 1)]


# ------------------------------------------------------------------------------------------- painting

class Painter:
    """One square texture atlas with helpers for worn, grimy surfaces."""

    def __init__(self, size, seed):
        self.size = size
        self.image = Image.new("RGB", (size, size), (40, 40, 40))
        self.draw = ImageDraw.Draw(self.image, "RGBA")
        self.random = np.random.default_rng(seed)
        # Smoothness, saved as the texture's alpha. The templates' Standard materials read smoothness from albedo
        # alpha (_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A, scaled by _GlossMapScale 0.9 to 0.93), so opaque alpha
        # renders everything near mirror gloss. Values here are the smoothness wanted in game.
        self.gloss = Image.new("L", (size, size), self.gloss_byte(0.08))

    @staticmethod
    def gloss_byte(smoothness):
        return int(round(min(1.0, smoothness / GLOSS_MAP_SCALE) * 255))

    def smooth(self, box, smoothness):
        """Sets the smoothness of a pixel box, with a little grime variation."""
        x0, y0, x1, y1 = [int(round(c)) for c in box]
        w, h = max(1, x1 - x0), max(1, y1 - y0)
        n = 1 + (self.noise(w, h, max(4, w // 8)) - 0.5) * 0.5
        values = np.clip(smoothness * n / GLOSS_MAP_SCALE, 0, 1)
        self.gloss.paste(Image.fromarray((values * 255).astype(np.uint8)), (x0, y0))

    def noise(self, w, h, cell):
        gw, gh = max(2, w // cell + 2), max(2, h // cell + 2)
        grid = Image.fromarray((self.random.random((gh, gw)) * 255).astype(np.uint8))
        return np.asarray(grid.resize((gw * cell, gh * cell), Image.BICUBIC), np.float32)[:h, :w] / 255.0

    def fill(self, region, rgb, variation=0.08, grain=0.05, streaks=False):
        w, h = int(round(region.w)), int(round(region.h))
        n = (self.noise(w, h, max(4, w // 6)) - 0.5) * variation * 2 + (self.noise(w, h, 6) - 0.5) * variation
        n += (self.random.random((h, w)) - 0.5) * grain
        if streaks:  # brushed metal, or leather grain along one direction
            n += (np.repeat(self.random.random((h, 1)), w, axis=1) - 0.5) * grain * 1.5
        colour = np.clip(np.array(rgb, np.float32) / KD ** 0.45 * (1 + n[..., None]), 0, 1)
        self.image.paste(Image.fromarray((colour * 255).astype(np.uint8)), (int(region.x), int(region.y)))

    def rect(self, box, rgba):
        self.draw.rectangle([round(c) for c in box], fill=rgba)

    def scuffs(self, region, count, rgba, length=0.08):
        for _ in range(count):
            x, y = self.random.random() * region.w + region.x, self.random.random() * region.h + region.y
            angle = self.random.random() * math.pi
            size = (0.3 + self.random.random()) * length * region.w
            self.draw.line([(x, y), (x + math.cos(angle) * size, y + math.sin(angle) * size)], fill=rgba, width=1)

    def wear(self, region, inset, strength, rgb=(255, 255, 255)):
        """Lighter rubbed patches along a region's border, where hands and pockets wear a case."""
        w, h = int(region.w), int(region.h)
        yy, xx = np.mgrid[0:h, 0:w]
        edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
        mask = np.clip(1 - edge / inset, 0, 1) * (self.noise(w, h, 5) > 0.55)
        layer = np.zeros((h, w, 4), np.uint8)
        layer[..., :3] = rgb
        layer[..., 3] = (mask * strength * 255).astype(np.uint8)
        self.image.paste(Image.fromarray(layer), (int(region.x), int(region.y)), Image.fromarray(layer[..., 3]))

    def grime(self, region, boxes, spread, strength):
        """Dirt packed into the seams around raised parts."""
        w, h = int(region.w), int(region.h)
        mask = Image.new("L", (w, h), 0)
        draw = ImageDraw.Draw(mask)
        for box in boxes:
            draw.rectangle([box[0] - region.x - spread, box[1] - region.y - spread,
                            box[2] - region.x + spread, box[3] - region.y + spread], fill=255)
        for box in boxes:
            draw.rectangle([box[0] - region.x, box[1] - region.y, box[2] - region.x, box[3] - region.y], fill=0)
        mask = mask.filter(ImageFilter.GaussianBlur(spread / 2))
        dirt = np.asarray(mask, np.float32) / 255 * strength * (0.5 + self.noise(w, h, 4))
        layer = Image.new("RGB", (w, h), (24, 20, 14))
        self.image.paste(layer, (int(region.x), int(region.y)), Image.fromarray(np.clip(dirt * 255, 0, 255).astype(np.uint8)))

    def text(self, xy, text, size, rgba, anchor="la", bold=True, spacing=0):
        font = load_font(size, bold)
        x, y = xy
        if spacing:
            widths = [self.draw.textlength(ch, font=font) + spacing for ch in text]
            total = sum(widths) - spacing
            x -= total / 2 if anchor[0] == "m" else 0
            for ch, width in zip(text, widths):
                self.draw.text((x, y), ch, font=font, fill=rgba, anchor="l" + anchor[1])
                x += width
        else:
            self.draw.text((x, y), text, font=font, fill=rgba, anchor=anchor)

    def glyph(self, name, center, pixel, rgba, worn=0.0):
        rows = GLYPH_MAPS[name]
        w, h = len(rows[0]) * pixel, len(rows) * pixel
        x0, y0 = center[0] - w / 2, center[1] - h / 2
        for r, row in enumerate(rows):
            for c, ch in enumerate(row):
                if ch != "X":
                    continue
                fade = 1 - worn * self.random.random() * 2
                box = (round(x0 + c * pixel), round(y0 + r * pixel), round(x0 + (c + 1) * pixel) - 1, round(y0 + (r + 1) * pixel) - 1)
                self.draw.rectangle(box, fill=rgba[:3] + (int(rgba[3] * max(0.25, fade)),))

    def save(self, path):
        texture = self.image.copy()
        texture.putalpha(self.gloss)
        texture.save(path, optimize=True)


def load_font(size, bold):
    for name in (("arialbd.ttf", "DejaVuSans-Bold.ttf") if bold else ("arial.ttf", "DejaVuSans.ttf")):
        try:
            return ImageFont.truetype(name, max(6, int(size)))
        except OSError:
            continue
    return ImageFont.load_default()


def load_glyphs():
    with open(GLYPHS, encoding="utf-8") as f:
        source = f.read()
    maps = {m.group(1): re.findall(r'"([.X]+)"', m.group(2))
            for m in re.finditer(r"internal static readonly string\[\] (\w+) =\s*\{(.*?)\};", source, re.S)}
    maps["Previous"] = [row[::-1] for row in maps["Next"]]
    maps["Back"] = [row[::-1] for row in maps["Forward"]]
    return maps


GLYPH_MAPS = load_glyphs()


# ---------------------------------------------------------------------------------------------- items

# The overlay's layout, in its 360 x 580 units (see PlayerWindow): keys, screen and speaker slots.
FACE = (360.0, 580.0)
TRANSPORT_KEYS = [("Previous", 23, 372, 92, 68), ("Pause", 125, 372, 110, 68), ("Next", 245, 372, 92, 68)]
FUNCTION_KEYS = [("Menu", 24, 2), ("Shuffle", 88, 2), ("Repeat", 152, 2), ("VolumeDown", 216, 2), ("VolumeUp", 280, 2)]


def player(pocket):
    w, h, d = (0.064, 0.103, 0.022) if pocket else (0.058, 0.094, 0.011)
    shell = (0.36, 0.29, 0.20) if pocket else (0.17, 0.32, 0.33)  # brown bakelite or dusty teal
    paint = Painter(1024, 11 if pocket else 12)
    front = Region(1024, 0, 0, 576, 928)
    back = Region(1024, 584, 0, 432, 696)
    side = Region(1024, 584, 704, 432, 96)
    rubber = Region(1024, 584, 808, 200, 96)
    metal = Region(1024, 792, 808, 224, 96)
    k = front.w / FACE[0]

    def ui(x, y, x1=None, y1=None):
        """Overlay units to a front-region pixel box."""
        return (front.x + x * k, front.y + y * k, front.x + (x1 if x1 is not None else x) * k, front.y + (y1 if y1 is not None else y) * k)

    paint.fill(front, shell, 0.07, 0.05)
    paint.fill(back, shell, 0.07, 0.05)
    paint.fill(side, shell, 0.05, 0.05)
    paint.fill(rubber, (0.13, 0.125, 0.115), 0.05, 0.08)
    paint.fill(metal, (0.55, 0.55, 0.52), 0.06, 0.08, streaks=True)
    paint.rect(side.box(0, 0.48, 1, 0.52), (0, 0, 0, 60))  # mould parting line
    for region in (front, back, side):
        paint.smooth(region.box(0, 0, 1, 1), 0.12)  # handled, worn plastic
    paint.smooth(rubber.box(0, 0, 1, 1), 0.03)
    paint.smooth(metal.box(0, 0, 1, 1), 0.3)

    # Screen: an unlit reflective LCD behind a dark bezel, with a faint dot grid and glare.
    paint.rect(ui(18, 42, 342, 358), (14, 14, 13, 255))
    lcd = Region(1024, *ui(26, 50)[:2], 308 * k, 300 * k)
    paint.fill(lcd, (0.33, 0.37, 0.29), 0.04, 0.02)
    for gx in np.arange(lcd.x, lcd.x + lcd.w, 3):
        paint.rect((gx + 2, lcd.y, gx + 2, lcd.y + lcd.h), (0, 0, 0, 18))
    for i in range(14):
        paint.draw.line([(lcd.x + lcd.w * (0.55 + i * 0.012), lcd.y), (lcd.x + lcd.w * (0.15 + i * 0.012), lcd.y + lcd.h)],
                        fill=(255, 255, 255, 7), width=3)
    paint.scuffs(lcd, 22, (255, 255, 255, 26), 0.12)
    paint.smooth(ui(18, 42, 342, 358), 0.2)  # bezel
    paint.smooth(ui(26, 50, 334, 350), 0.7)  # only the screen glass is glossy

    engraving = tuple(int(min(1, c * 2.1) * 255) for c in shell) + (150,)
    silkscreen = (219, 209, 181, 235)  # cream paint, matching the overlay
    paint.text((front.x + 26 * k, front.y + 23 * k), "POCKET MUSIC PLAYER" if pocket else "RECHARGEABLE MUSIC PLAYER",
               13 * k, silkscreen, "lm", spacing=1.2 * k)
    if not pocket:  # charge indicator beside the screen
        paint.rect(ui(320, 18, 326, 24), (40, 60, 30, 255))

    keys = [ui(x, y, x + kw, y + kh) for _, x, y, kw, kh in TRANSPORT_KEYS] + \
           [ui(x, 460, x + 56, 502) for _, x, _ in FUNCTION_KEYS]
    paint.grime(front, keys + [ui(18, 42, 342, 358)], 3 * k, 0.65)
    glyph = (205, 196, 175, 235)
    for (name, x, y, kw, kh), box in zip(TRANSPORT_KEYS, keys):
        paint.rect(box, (31, 29, 27, 255))
        paint.rect((box[0], box[1], box[2], box[1] + 2 * k), (60, 58, 54, 255))
        paint.glyph(name, ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2), 3 * k, glyph, worn=0.12)
    for box in keys:
        paint.smooth(box, 0.04)  # rubber key tops
    for (name, x, pixel), box in zip(FUNCTION_KEYS, keys[3:]):
        paint.rect(box, (31, 29, 27, 255))
        paint.rect((box[0], box[1], box[2], box[1] + 2 * k), (60, 58, 54, 255))
        paint.glyph(name, ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2), pixel * k, glyph, worn=0.18)
    for i in range(4):
        paint.rect(ui(120, 532 + i * 9, 240, 535 + i * 9), (10, 9, 8, 230))
    for x, y in [(10, 10), (342, 10), (10, 560), (342, 560)]:
        paint.rect(ui(x, y, x + 8, y + 8), (22, 20, 18, 255))
        paint.rect(ui(x + 2, y + 3, x + 6, y + 5), (120, 115, 105, 255))
    paint.wear(front, 12, 0.16 if pocket else 0.10)
    paint.scuffs(front, 40, (255, 255, 255, 22))

    # Back: a sticker and, on the pocket player, the AA battery door.
    sticker = Region(1024, back.x + back.w * 0.16, back.y + back.h * 0.08, back.w * 0.68, back.h * 0.24)
    paint.fill(sticker, (0.68, 0.64, 0.52), 0.12, 0.06)
    lines = (["POCKET MUSIC PLAYER", "MODEL PMP-12", "1 x AA  1.5V  150mA", "S/N 0471-2219"] if pocket else
             ["RECHARGEABLE MUSIC PLAYER", "MODEL RMP-24", "Li-ion 3.7V  800mAh", "DO NOT DISPOSE IN FIRE"])
    for i, line in enumerate(lines):
        paint.text((sticker.x + 10, sticker.y + 14 + i * sticker.h * 0.22), line, sticker.h * (0.15 if i == 0 else 0.12),
                   (35, 32, 26, 230), "lm", bold=i == 0)
    paint.wear(sticker, 18, 0.5, (150, 140, 110))
    paint.smooth(sticker.box(0, 0, 1, 1), 0.03)
    if pocket:
        door = (back.x + back.w * 0.12, back.y + back.h * 0.42, back.x + back.w * 0.88, back.y + back.h * 0.94)
        paint.draw.rectangle(door, outline=(18, 15, 11, 230), width=3)
        for i in range(6):
            y = door[1] + 14 + i * 9
            paint.rect((door[0] + back.w * 0.2, y, door[2] - back.w * 0.2, y + 3), (20, 17, 12, 150))
        paint.text(((door[0] + door[2]) / 2, door[3] - 30), "OPEN", 20, engraving, "mm", spacing=3)
    paint.wear(back, 14, 0.18 if pocket else 0.10)
    paint.scuffs(back, 60, (255, 255, 255, 26))
    paint.scuffs(side, 30, (255, 255, 255, 30), 0.03)

    mesh = Mesh()
    face_uv = lambda x, y: front.uv(x / w + 0.5, 0.5 - y / h)
    back_uv = lambda x, y: back.uv(0.5 - x / w, 0.5 - y / h)
    slab(mesh, rounded_rect(w, h, 0.0075 if pocket else 0.006, 3), -d / 2, d / 2, 0.0022 if pocket else 0.0015,
         face_uv, back_uv, side.uv)

    def key(x, y, kw, kh, height):
        cx, cy = ((x + kw / 2) / FACE[0] - 0.5) * w, (0.5 - (y + kh / 2) / FACE[1]) * h
        sw, sh = kw / FACE[0] * w, kh / FACE[1] * h
        slab(mesh, rounded_rect(sw, sh, min(sw, sh) * 0.22, 1, (cx, cy)), d / 2 - 0.0005, d / 2 + height, 0.0005,
             face_uv, None, rubber.uv, back_cap=False)

    for _, x, y, kw, kh in TRANSPORT_KEYS:
        key(x, y, kw, kh, 0.0013)
    for _, x, _ in FUNCTION_KEYS:
        key(x, 460, 56, 42, 0.0009)
    # Headphone socket in the top edge.
    lathe(mesh, (w * 0.25, h / 2, 0), (0, 1, 0),
          [(0.0034, -0.0004), (0.0034, 0.0005, "hard"), (0.0021, 0.0005, "hard"), (0.0021, -0.0025, "hard"), (0, -0.0025)],
          8, metal)
    if pocket:  # lanyard loop
        x = -w * 0.27
        path = [(x, h / 2 - 0.0008, -0.0045), (x, h / 2 + 0.0024, -0.0040), (x, h / 2 + 0.0040, -0.0016),
                (x, h / 2 + 0.0040, 0.0016), (x, h / 2 + 0.0024, 0.0040), (x, h / 2 - 0.0008, 0.0045)]
        sweep(mesh, path, rounded_rect(0.0016, 0.0022, 0.0004, 1), side)
    else:  # charging port in the bottom edge
        a = mesh.vertex((-0.004, -h / 2 - 0.0001, -0.0012), rubber.uv(0.05, 0.05), (0, -1, 0))
        b = mesh.vertex((0.004, -h / 2 - 0.0001, -0.0012), rubber.uv(0.15, 0.05), (0, -1, 0))
        c = mesh.vertex((0.004, -h / 2 - 0.0001, 0.0012), rubber.uv(0.15, 0.15), (0, -1, 0))
        e = mesh.vertex((-0.004, -h / 2 - 0.0001, 0.0012), rubber.uv(0.05, 0.15), (0, -1, 0))
        paint.rect(rubber.box(0, 0, 0.2, 0.2), (6, 6, 6, 255))
        mesh.quad(a, b, c, e)
    return mesh, paint


def headphones(work):
    paint = Painter(512, 21 if work else 22)
    cup_region = Region(512, 0, 0, 512, 128)
    pad_region = Region(512, 0, 136, 512, 120)
    band_region = Region(512, 0, 264, 512, 104)
    metal = Region(512, 0, 376, 248, 64)
    cable = Region(512, 256, 376, 256, 64)
    plug = Region(512, 0, 448, 512, 64)
    cup_colour = (0.56, 0.43, 0.13) if work else (0.12, 0.115, 0.11)
    paint.fill(cup_region, cup_colour, 0.10 if work else 0.06, 0.06)
    paint.fill(pad_region, (0.10, 0.10, 0.095) if work else (0.25, 0.165, 0.11), 0.12, 0.10, streaks=not work)
    paint.fill(band_region, (0.09, 0.09, 0.085) if work else (0.20, 0.135, 0.09), 0.08, 0.08, streaks=not work)
    paint.fill(metal, (0.52, 0.52, 0.50), 0.05, 0.08, streaks=True)
    paint.fill(cable, (0.08, 0.08, 0.075), 0.05, 0.05)
    paint.fill(plug, (0.08, 0.08, 0.075), 0.05, 0.05)
    paint.fill(Region(512, plug.x + plug.w * 0.55, plug.y, plug.w * 0.45, plug.h), (0.60, 0.56, 0.42), 0.05, 0.08)
    paint.smooth(cup_region.box(0, 0, 1, 1), 0.12 if work else 0.18)
    paint.smooth(pad_region.box(0, 0, 1, 1), 0.02 if work else 0.12)  # foam, or worn leatherette
    paint.smooth(band_region.box(0, 0, 1, 1), 0.06 if work else 0.1)
    paint.smooth(metal.box(0, 0, 1, 1), 0.28)
    paint.smooth(cable.box(0, 0, 1, 1), 0.1)
    paint.smooth(plug.box(0, 0, 0.55, 1), 0.12)
    paint.smooth(plug.box(0.55, 0, 1, 1), 0.3)
    # Speaker cloth in the middle of each pad, then a rim and a trim ring on the cups.
    paint.fill(Region(512, 0, pad_region.y + pad_region.h * 0.86, 512, pad_region.h * 0.14), (0.05, 0.05, 0.05), 0.2, 0.2)
    paint.fill(Region(512, 0, cup_region.y + cup_region.h * 0.8, 512, cup_region.h * 0.2), cup_colour, 0.02, 0.02)
    paint.rect(cup_region.box(0, 0, 1, 0.12), (12, 11, 10, 255))
    if work:
        paint.rect(cup_region.box(0, 0.62, 1, 0.66), (20, 18, 12, 120))
        paint.grime(cup_region, [cup_region.box(0, 0.2, 1, 0.9)], 12, 0.7)
        paint.scuffs(cup_region, 90, (255, 240, 200, 34), 0.06)
        paint.scuffs(cup_region, 40, (20, 18, 12, 70), 0.04)
    else:
        paint.rect(cup_region.box(0, 0.56, 1, 0.62), (130, 128, 120, 255))
        paint.scuffs(cup_region, 50, (255, 255, 255, 22), 0.05)
        for x in range(0, 512, 6):  # stitching along the band's edges
            for s in (0.08, 0.92):
                paint.rect(band_region.box(s - 0.003, x / 512, s + 0.003, (x + 3) / 512), (150, 120, 90, 120))
    paint.scuffs(band_region, 30, (255, 255, 255, 20), 0.05)

    mesh = Mesh()
    sides = 10
    if work:
        x0, y0, scale = 0.068, 0.052, (1.0, 1.2)
        cup = [(0.036, 0), (0.040, 0.006), (0.041, 0.030, "hard"), (0.036, 0.042), (0.022, 0.049), (0, 0.051)]
        pad = [(0.036, -0.002), (0.041, 0.004), (0.042, 0.013), (0.036, 0.021), (0.025, 0.019), (0.017, 0.012, "hard"), (0, 0.012)]
        band_center, band_radius, band_size = (0.0, 0.108), 0.098, (0.007, 0.032)
    else:
        x0, y0, scale = 0.078, 0.042, (1.0, 1.0)
        cup = [(0.036, 0), (0.039, 0.004), (0.039, 0.020, "hard"), (0.031, 0.027), (0.016, 0.030), (0, 0.0305)]
        pad = [(0.037, -0.002), (0.041, 0.004), (0.041, 0.012), (0.035, 0.019), (0.024, 0.017), (0.016, 0.010, "hard"), (0, 0.010)]
        band_center, band_radius, band_size = (0.0, 0.112), 0.093, (0.009, 0.028)
    for side_sign in (-1, 1):
        lathe(mesh, (side_sign * x0, y0, 0), (side_sign, 0, 0), cup, sides, cup_region, scale)
        lathe(mesh, (side_sign * x0, y0, 0), (-side_sign, 0, 0), pad, sides, pad_region, scale)
        # A yoke from the cup top up into the band.
        top = y0 + cup[1][0] * scale[1]
        sweep(mesh, [(side_sign * band_radius, top - 0.012, 0), (side_sign * band_radius, band_center[1] + 0.002, 0)],
              rounded_rect(0.005, 0.012, 0.001, 1), metal)
    sweep(mesh, arc(band_center, band_radius, 0, 180, 12), rounded_rect(band_size[0], band_size[1], 0.0025, 1), band_region)
    if not work:  # a short cable and plug from the left cup
        path = [(-0.092, 0.010, 0), (-0.098, -0.008, 0.004), (-0.090, -0.025, 0.008), (-0.072, -0.033, 0.009),
                (-0.052, -0.030, 0.007), (-0.040, -0.026, 0.006)]
        sweep(mesh, curve(path, 2), circle(0.0018, 5), cable, caps=False)
        lathe(mesh, path[-1], unit(np.subtract(path[-1], path[-2])),
              [(0.0018, 0), (0.0032, 0.002), (0.0032, 0.014, "hard"), (0.0016, 0.0145, "hard"), (0.0016, 0.024),
               (0.0012, 0.0255), (0, 0.0258)], 8, plug)
    return mesh, paint


def budget_earbuds():
    paint = Painter(256, 31)
    shell = Region(256, 0, 0, 256, 96)
    cable = Region(256, 0, 104, 256, 40)
    plug = Region(256, 0, 152, 256, 56)
    paint.fill(shell, (0.70, 0.66, 0.55), 0.10, 0.06)  # off-white plastic, yellowed
    paint.fill(Region(256, 0, shell.y + shell.h * 0.58, 256, shell.h * 0.30), (0.24, 0.24, 0.22), 0.25, 0.25)  # foam
    paint.fill(Region(256, 0, shell.y + shell.h * 0.88, 256, shell.h * 0.12), (0.22, 0.22, 0.20), 0.03, 0.03)
    paint.grime(shell, [shell.box(0, 0.0, 1, 0.5)], 6, 0.45)
    paint.fill(cable, (0.50, 0.48, 0.42), 0.08, 0.05)
    paint.fill(plug, (0.62, 0.59, 0.50), 0.08, 0.05)
    paint.fill(Region(256, plug.x, plug.y + plug.h * 0.55, plug.w, plug.h * 0.45), (0.58, 0.55, 0.40), 0.06, 0.08)
    paint.rect(plug.box(0, 0.74, 1, 0.78), (20, 18, 14, 255))
    paint.smooth(shell.box(0, 0, 1, 0.58), 0.16)
    paint.smooth(shell.box(0, 0.58, 1, 1), 0.02)  # foam covers and grille
    paint.smooth(cable.box(0, 0, 1, 1), 0.1)
    paint.smooth(plug.box(0, 0, 1, 0.55), 0.12)
    paint.smooth(plug.box(0, 0.55, 1, 1), 0.3)

    mesh = Mesh()
    splitter = (0.003, 0.032, 0)
    for side_sign in (-1, 1):
        bx, by = side_sign * 0.018, 0.069 + (0.004 if side_sign > 0 else 0)
        lathe(mesh, (bx, by, 0), (0, 0, 1),
              [(0, -0.0055), (0.0055, -0.0045), (0.0085, -0.0012), (0.0086, 0.0010, "hard"), (0.0080, 0.0028),
               (0.0055, 0.0042), (0, 0.0047)], 10, shell)
        lathe(mesh, (bx, by - 0.0072, -0.0012), (0, -1, 0),
              [(0.0026, 0), (0.0026, 0.006), (0.0019, 0.010, "hard"), (0, 0.010)], 6, plug)
        end = (bx, by - 0.0172, -0.0012)
        sweep(mesh, curve([end, (bx * 0.92, by - 0.030, -0.0010), (bx * 0.35, 0.043, -0.0006),
                           (splitter[0], splitter[1] + 0.006, 0)], 3), circle(0.0010, 5), cable, caps=False)
    lathe(mesh, (splitter[0], splitter[1] + 0.006, 0), (0, -1, 0),
          [(0, 0), (0.0022, 0.0005), (0.0024, 0.0050), (0.0012, 0.0068, "hard"), (0, 0.0068)], 6, plug)
    sweep(mesh, curve([(splitter[0], splitter[1] - 0.0008, 0), (0.008, 0.024, 0.0005), (0.000, 0.016, 0.0008),
                       (-0.004, 0.011, 0.0005), (0, 0.0072, 0)], 3), circle(0.0011, 5), cable, caps=False)
    lathe(mesh, (0, 0.0072, 0), (0, -1, 0),
          [(0.0012, 0), (0.0030, 0.0012), (0.0030, 0.0070, "hard"), (0.0016, 0.0072, "hard"), (0.0016, 0.0110),
           (0.0013, 0.0118), (0.0016, 0.0124), (0.0012, 0.0138), (0, 0.0142)], 8, plug)
    return mesh, paint


ITEMS = {
    "pocket-player": lambda: player(True),
    "rechargeable-player": lambda: player(False),
    "budget-earbuds": budget_earbuds,
    "work-headphones": lambda: headphones(True),
    "hifi-headphones": lambda: headphones(False),
}


# ------------------------------------------------------------------------------------------- renderer

def render(mesh, texture, size, yaw, pitch, supersample=3):
    """Textured, smooth-shaded z-buffer render on a transparent background. The Kd tint is applied, so
    colours read as they will in game."""
    s = size * supersample
    v = np.array(mesh.positions, dtype=float)
    normals = np.array(mesh.normals, dtype=float)
    uvs = np.array(mesh.uvs, dtype=float)
    tex = np.asarray(texture, dtype=np.float32) / 255.0 * KD ** 0.45
    th, tw = tex.shape[:2]
    yaw_r, pitch_r = math.radians(yaw), math.radians(pitch)
    ry = np.array([[math.cos(yaw_r), 0, math.sin(yaw_r)], [0, 1, 0], [-math.sin(yaw_r), 0, math.cos(yaw_r)]])
    rx = np.array([[1, 0, 0], [0, math.cos(pitch_r), -math.sin(pitch_r)], [0, math.sin(pitch_r), math.cos(pitch_r)]])
    rotation = rx @ ry
    view = v @ rotation.T
    nview = normals @ rotation.T
    lo, hi = view[:, :2].min(axis=0), view[:, :2].max(axis=0)
    scale = (s * 0.84) / max(hi - lo)
    offset = (np.array([s, s]) - (hi - lo) * scale) / 2
    xy = (view[:, :2] - lo) * scale + offset
    xy[:, 1] = s - xy[:, 1]
    depth = view[:, 2]
    color = np.zeros((s, s, 3))
    alpha = np.zeros((s, s))
    zbuf = np.full((s, s), -np.inf)
    light = unit([-0.4, 0.7, 0.6])
    half = unit(light + np.array([0, 0, 1.0]))
    for i, j, k in mesh.faces:
        p = np.array([xy[i], xy[j], xy[k]])
        x0, y0 = np.floor(p.min(axis=0)).astype(int)
        x1, y1 = np.ceil(p.max(axis=0)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, s - 1), min(y1, s - 1)
        if x0 > x1 or y0 > y1:
            continue
        d = (p[1][1] - p[2][1]) * (p[0][0] - p[2][0]) + (p[2][0] - p[1][0]) * (p[0][1] - p[2][1])
        if abs(d) < 1e-9:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        w0 = ((p[1][1] - p[2][1]) * (gx - p[2][0]) + (p[2][0] - p[1][0]) * (gy - p[2][1])) / d
        w1 = ((p[2][1] - p[0][1]) * (gx - p[2][0]) + (p[0][0] - p[2][0]) * (gy - p[2][1])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        z = w0 * depth[i] + w1 * depth[j] + w2 * depth[k]
        region = zbuf[y0:y1 + 1, x0:x1 + 1]
        closer = inside & (z > region)
        if not closer.any():
            continue
        region[closer] = z[closer]
        a, b, c = w0[closer][:, None], w1[closer][:, None], w2[closer][:, None]
        n = a * nview[i] + b * nview[j] + c * nview[k]
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        uv = a * uvs[i] + b * uvs[j] + c * uvs[k]
        tx = np.clip((uv[:, 0] * tw).astype(int), 0, tw - 1)
        ty = np.clip(((1 - uv[:, 1]) * th).astype(int), 0, th - 1)
        diffuse = np.clip(n @ light, 0, 1)[:, None]
        specular = np.clip(n @ half, 0, 1)[:, None] ** 24 * 0.12
        shaded = tex[ty, tx] * (0.36 + 0.7 * diffuse) + specular
        color[y0:y1 + 1, x0:x1 + 1][closer] = shaded
        alpha[y0:y1 + 1, x0:x1 + 1][closer] = 1.0
    image = Image.fromarray(np.dstack([np.clip(color, 0, 1) * 255, alpha * 255]).astype(np.uint8))
    return image.resize((size, size), Image.LANCZOS)


def icon(mesh, texture, yaw, pitch):
    art = render(mesh, texture, 256, yaw, pitch)
    # A dark outline keeps the silhouette readable on the inventory's light and dark slots.
    outline = art.getchannel("A").filter(ImageFilter.MaxFilter(7))
    base = Image.new("RGBA", art.size, (24, 24, 24, 0))
    base.putalpha(outline.point(lambda a: 200 if a > 0 else 0))
    base.alpha_composite(art)
    return base


VIEWS = {
    "pocket-player": (-28, 14),
    "rechargeable-player": (-28, 14),
    "budget-earbuds": (-20, 15),
    "work-headphones": (-35, 18),
    "hifi-headphones": (-35, 18),
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview")
    args = parser.parse_args()
    os.makedirs(ASSETS, exist_ok=True)
    for name, build in ITEMS.items():
        mesh, paint = build()
        mesh.write(name)
        paint.save(os.path.join(ASSETS, name + ".png"))
        icon(mesh, paint.image, *VIEWS[name]).save(os.path.join(ASSETS, name + "-icon.png"))
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            sheet = Image.new("RGBA", (2048, 512), (60, 64, 70, 255))
            for column, (yaw, pitch) in enumerate([(0, 0), (90, 0), (0, 89), VIEWS[name]]):
                sheet.alpha_composite(render(mesh, paint.image, 512, yaw, pitch), (column * 512, 0))
            sheet.save(os.path.join(args.preview, name + "-views.png"))
        print(name, len(mesh.positions), "vertices", len(mesh.faces), "triangles", mesh.inverted(), "inverted")


if __name__ == "__main__":
    main()
