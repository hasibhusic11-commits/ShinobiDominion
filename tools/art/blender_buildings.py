"""Builds Shinobi Dominion buildings as 3D models in Blender and renders them at the game's camera.

  blender -b --factory-startup --python tools/art/blender_buildings.py -- <out_folder> <building> <level>

The game camera is orthographic, 30 degrees down and turned 45 degrees (IsoCamera.Pitch), so a square footprint
is a 2:1 diamond. Renders are 1024 x 1024, transparent, with the footprint's front corner near the bottom
centre, ready for tools/art/import_buildings_v2.py. Outlines are an inverted hull (dark back faces pushed out),
the classic toon trick. See docs/art/ART_BRIEF.md for the palette and rules.

World units: one grid tile = 1. Blender X/Y match Unity X/Z; the camera sees the -X and -Y faces.
"""
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

args = sys.argv[sys.argv.index('--') + 1:]
OUT, BUILDING, LEVEL = args[0], args[1], int(args[2])
os.makedirs(OUT, exist_ok=True)
rng = random.Random(LEVEL * 101 + 7)


def hexc(h, a=1.0):
    h = h.lstrip('#')
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, a)


PALETTE = {
    'slate': '#3A5590', 'slate_dark': '#22305A', 'crimson': '#C8302E', 'timber': '#9A5E30', 'timber_dark': '#64391E',
    'plank': '#C08048', 'stone': '#A8A090', 'stone_dark': '#7A7266', 'gold': '#F2C14E', 'banner': '#D8342C',
    'glow': '#FFD07A', 'rope': '#D9B677', 'steel': '#C4CAD2', 'slit': '#22160E', 'rock': '#857E74',
    'rock_dark': '#5E5850', 'moss': '#5E8F2E', 'earth': '#8A6440', 'flame': '#FF8A20', 'nugget': '#FFC838',
    'cave': '#1A0F08', 'iron': '#4A4A52',
}

# ---------------------------------------------------------------- scene

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0.0
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
try:
    scene.eevee.use_shadows = True
    scene.eevee.shadow_ray_count = 2
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.6
except AttributeError:
    pass

world = bpy.data.worlds.new('World')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = hexc('#C8D4F0')
world.node_tree.nodes['Background'].inputs[1].default_value = 0.75

materials = {}


def mat(name, color=None, rough=0.75, emit=0.0, metal=0.0, paint=0.22):
    """
    A material with a painted feel: the base colour drifts lighter and darker in soft blotches (a noise
    texture), the way a hand-painted surface varies, instead of one flat CG colour.
    """
    key = (name, color, emit, metal)
    if key in materials:
        return materials[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes['Principled BSDF']
    c = hexc(color or PALETTE[name])
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    if emit:
        bsdf.inputs['Base Color'].default_value = c
        bsdf.inputs['Emission Color'].default_value = c
        bsdf.inputs['Emission Strength'].default_value = emit
    else:
        coord = nodes.new('ShaderNodeTexCoord')
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 3.5
        noise.inputs['Detail'].default_value = 3.0
        links.new(coord.outputs['Object'], noise.inputs['Vector'])
        ramp = nodes.new('ShaderNodeValToRGB')
        dark = tuple(v * (1 - paint) for v in c[:3]) + (1,)
        light = tuple(min(1.0, v * (1 + paint * 1.4) + 0.01) for v in c[:3]) + (1,)
        ramp.color_ramp.elements[0].color = dark
        ramp.color_ramp.elements[1].color = light
        ramp.color_ramp.elements[0].position = 0.3
        ramp.color_ramp.elements[1].position = 0.7
        links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    materials[key] = m
    return m


OUTLINE = bpy.data.materials.new('Outline')
OUTLINE.use_nodes = True
OUTLINE.use_backface_culling = True
_n = OUTLINE.node_tree.nodes
_n.remove(_n['Principled BSDF'])
_e = _n.new('ShaderNodeEmission')
_e.inputs[0].default_value = hexc('#1A1008')
OUTLINE.node_tree.links.new(_e.outputs[0], _n['Material Output'].inputs[0])

parts = []


def finish(obj, material, bevel=0.025, outline=0.022):
    obj.data.materials.append(material)
    obj.data.materials.append(OUTLINE)
    if bevel:
        b = obj.modifiers.new('Bevel', 'BEVEL')
        b.width = bevel
        b.segments = 2
        b.limit_method = 'ANGLE'
    if outline:
        s = obj.modifiers.new('Outline', 'SOLIDIFY')
        s.thickness = outline
        s.offset = 1.0
        s.use_flip_normals = True
        s.use_rim = False
        s.material_offset = 1
    parts.append(obj)
    return obj


def box(center, size, material, rot_z=0.0, bevel=0.025, outline=0.022, name='Box'):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=(0, 0, rot_z))
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, material, bevel, outline)


def cylinder(center, radius, depth, material, verts=12, rot=(0, 0, 0), bevel=0.015, outline=0.02, radius2=None):
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius2, depth=depth, location=center, rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    return finish(o, material, bevel, outline)


def sphere(center, radius, material, scale=(1, 1, 1), outline=0.015):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius, location=center)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    bpy.ops.object.shade_smooth()
    return finish(o, material, 0, outline)


def mesh_object(name, verts, faces, material, bevel=0.0, outline=0.02, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return finish(o, material, bevel, outline)


def tapered_post(a, b, r0, r1, material):
    """A square post from point a to point b, thinning from r0 to r1."""
    a, b = Vector(a), Vector(b)
    up = (b - a)
    length = up.length
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=r0 * 1.41, radius2=r1 * 1.41, depth=length, location=(a + b) / 2)
    o = bpy.context.active_object
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(up.normalized()) @ \
        Matrix.Rotation(math.radians(45), 4, 'Z').to_quaternion()
    return finish(o, material, 0.015, 0.02)


def pagoda_roof(center_z, half, rise, upturn, overhang_drop, material, ridge_material, rows=5, cols=8):
    """A four-sided hip roof with a concave sweep and upturned corners, the signature shape of the village."""
    verts, faces = [], []
    apex = Vector((0, 0, center_z + rise))
    for side in range(4):
        ang = side * math.pi / 2
        base = len(verts)
        for r in range(rows + 1):
            t = r / rows
            hw = half * (1 - t)
            for c in range(cols + 1):
                u = c / cols * 2 - 1
                corner = abs(u) ** 3
                z = center_z - overhang_drop * (1 - t) + rise * (t ** 1.6) + upturn * corner * (1 - t) ** 2
                p = Vector((u * hw, -hw, z))
                p.rotate(Matrix.Rotation(ang, 3, 'Z'))
                verts.append(tuple(p))
        for r in range(rows):
            for c in range(cols):
                i = base + r * (cols + 1) + c
                faces.append((i, i + 1, i + cols + 2, i + cols + 1))
    o = mesh_object('Roof', verts, faces, material, 0, 0.03, smooth=True)
    sol = o.modifiers.new('Thick', 'SOLIDIFY')
    sol.thickness = 0.06
    sol.offset = -1
    o.modifiers.move(len(o.modifiers) - 1, 0)
    # Rows of rounded tiles running down each face, so the roof reads as tiled, not as cloth.
    for side in range(4):
        ang = side * math.pi / 2
        for c in range(1, cols * 2):
            u = c / (cols * 2) * 2 - 1
            pts = []
            for r in range(rows + 1):
                t = r / rows
                hw = half * (1 - t)
                if abs(u) * hw > hw - 0.02 or (1 - t) * abs(u) > 0.92:
                    continue
                corner = abs(u) ** 3
                z = center_z - overhang_drop * (1 - t) + rise * (t ** 1.6) + upturn * corner * (1 - t) ** 2 + 0.025
                p = Vector((u * hw, -hw - 0.01, z))
                p.rotate(Matrix.Rotation(ang, 3, 'Z'))
                pts.append(p)
            for a, b in zip(pts, pts[1:]):
                if (a - b).length > 0.02:
                    tapered_post(a, b, 0.022, 0.018, material).modifiers['Outline'].thickness = 0.012
    # Ridges down each corner, ending in an upturned tip.
    for side in range(4):
        ang = side * math.pi / 2 + math.pi / 4
        pts = []
        for r in range(rows + 1):
            t = r / rows
            hw = half * (1 - t)
            z = center_z - overhang_drop * (1 - t) + rise * (t ** 1.6) + upturn * (1 - t) ** 2
            p = Vector((hw * 1.414, 0, z + 0.04))
            p.rotate(Matrix.Rotation(ang - math.pi / 4 * 0 + 0, 3, 'Z'))
            pts.append(p)
        for a, b in zip(pts, pts[1:]):
            tapered_post(a, b, 0.045, 0.04, ridge_material)
        sphere(pts[0] + Vector((0, 0, 0.05)), 0.07, ridge_material)
    sphere(apex + Vector((0, 0, 0.08)), 0.1, ridge_material)
    cylinder(apex + Vector((0, 0, 0.25)), 0.03, 0.3, ridge_material, verts=8)
    return o


# ---------------------------------------------------------------- the Defense Tower

def stone_base(half, height, rows=3):
    """A plinth of chunky, slightly irregular stone blocks around a solid core."""
    box((0, 0, height / 2 - 0.01), (half * 2 - 0.12, half * 2 - 0.12, height - 0.02), mat('stone_dark'), bevel=0, outline=0)
    row_h = height / rows
    for r in range(rows):
        inset = r * 0.04
        h = half - inset
        z = r * row_h + row_h / 2
        for side in range(4):
            ang = side * math.pi / 2
            n = 4
            for i in range(n):
                w = (2 * h) / n
                x = -h + w * (i + 0.5) + (w * 0.5 if r % 2 else 0) * (1 if i < n - 1 else 0)
                if r % 2 and i == n - 1:
                    continue
                p = Vector((x, -h + 0.13, z))
                p.rotate(Matrix.Rotation(ang, 3, 'Z'))
                shade = rng.uniform(-0.06, 0.06)
                base = Vector(hexc(PALETTE['stone'])[:3])
                col = '#%02X%02X%02X' % tuple(int(max(0, min(1, (c ** (1 / 2.2)) + shade)) * 255) for c in base)
                box(tuple(p), (w * 0.94, 0.28, row_h * 0.92), mat('stone_v%02d' % int((shade + 0.06) * 100), col), rot_z=ang,
                    bevel=0.04, outline=0.018)


def defense_tower(level):
    half = 0.98
    base_h = 0.48
    stone_base(half, base_h)
    top = 2.7
    body_bottom, body_top = 0.78, 0.6
    timber, dark = mat('timber'), mat('timber_dark')
    # Four corner posts leaning inwards.
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        tapered_post((sx * body_bottom, sy * body_bottom, base_h), (sx * body_top, sy * body_top, top), 0.1, 0.085, dark)
    # Plank walls: one tapered panel per side, with dark grooves between the planks.
    plank = mat('plank')
    groove = mat('timber_dark')
    for side in range(4):
        ang = side * math.pi / 2
        rot = Matrix.Rotation(ang, 3, 'Z')
        b0, b1 = body_bottom - 0.04, body_top - 0.04
        z0, z1 = base_h, top
        verts = [(-b0, -b0, z0), (b0, -b0, z0), (b1, -b1, z1), (-b1, -b1, z1),
                 (-b0 + 0.08, -b0 + 0.08, z0), (b0 - 0.08, -b0 + 0.08, z0), (b1 - 0.08, -b1 + 0.08, z1), (-b1 + 0.08, -b1 + 0.08, z1)]
        verts = [tuple(rot @ Vector(v)) for v in verts]
        faces = [(0, 1, 2, 3), (5, 4, 7, 6), (0, 3, 7, 4), (1, 5, 6, 2), (3, 2, 6, 7), (0, 4, 5, 1)]
        mesh_object('Panel', verts, faces, plank, 0.01, 0.012)
        for i in range(1, 5):
            t = i / 5 * 2 - 1
            a = rot @ Vector((t * b0, -b0 - 0.012, z0 + 0.02))
            b = rot @ Vector((t * b1, -b1 - 0.012, z1 - 0.02))
            tapered_post(a, b, 0.012, 0.012, groove)
        # Cross braces.
        for a, b in (((-0.62, base_h + 0.15), (0.62, top - 0.9)), ((0.62, base_h + 0.15), (-0.62, top - 0.9))):
            pa = Vector((a[0], -0.76 + (a[1] - base_h) * 0.08, a[1]))
            pb = Vector((b[0], -0.76 + (b[1] - base_h) * 0.08, b[1]))
            pa.rotate(Matrix.Rotation(ang, 3, 'Z'))
            pb.rotate(Matrix.Rotation(ang, 3, 'Z'))
            tapered_post(pa, pb, 0.04, 0.04, dark)
        if side in (0, 3):
            for zz in (1.45, 2.05):
                p = Vector((0, -0.72, zz))
                p.rotate(Matrix.Rotation(ang, 3, 'Z'))
                box(tuple(p), (0.1, 0.06, 0.32), mat('slit'), rot_z=ang, bevel=0.01, outline=0.015)
    # Bands of rope / iron around the body.
    for zz in (base_h + 0.08, 1.6, top - 0.06):
        hw = body_bottom - (body_bottom - body_top) * (zz - base_h) / (top - base_h) + 0.03
        for side in range(4):
            ang = side * math.pi / 2
            p = Vector((0, -hw, zz))
            p.rotate(Matrix.Rotation(ang, 3, 'Z'))
            box(tuple(p), (2 * hw + 0.06, 0.05, 0.07), mat('rope'), rot_z=ang, bevel=0.01, outline=0.012)
    # Fighting platform.
    deck = 0.86
    box((0, 0, top + 0.06), (deck * 2, deck * 2, 0.14), dark, bevel=0.03)
    for side in range(4):
        ang = side * math.pi / 2
        for i in range(5):
            x = -deck + 0.06 + (2 * deck - 0.12) * i / 4
            p = Vector((x, -deck + 0.05, top + 0.32))
            p.rotate(Matrix.Rotation(ang, 3, 'Z'))
            box(tuple(p), (0.07, 0.07, 0.42), timber, rot_z=ang, bevel=0.01, outline=0.012)
        p = Vector((0, -deck + 0.05, top + 0.5))
        p.rotate(Matrix.Rotation(ang, 3, 'Z'))
        box(tuple(p), (deck * 2, 0.08, 0.07), dark, rot_z=ang, bevel=0.012, outline=0.014)
    # Roof posts and the pagoda roof.
    roof_z = top + 1.05
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        tapered_post((sx * 0.72, sy * 0.72, top + 0.1), (sx * 0.72, sy * 0.72, roof_z), 0.06, 0.06, dark)
    pagoda_roof(roof_z, 1.18, 0.62, 0.32, 0.18, mat('slate'), mat('slate_dark'))
    # The crossbow on the deck's front-right corner, aimed out to the lower right of the screen, clear of the roof.
    aim = Vector((1, -1, -0.25)).normalized()
    side_vec = Vector((1, 1, 0)).normalized()
    base_p = Vector((0.5, -0.5, top + 0.55))
    cylinder(base_p - Vector((0, 0, 0.12)), 0.08, 0.25, dark, verts=8)
    stock_c = base_p + aim * 0.25 + Vector((0, 0, 0.08))
    stock = box(tuple(stock_c), (0.85, 0.12, 0.1), timber, rot_z=math.atan2(aim.y, aim.x), bevel=0.02)
    bow_c = base_p + aim * 0.62 + Vector((0, 0, 0.1))
    pts = []
    for i in range(9):
        t = i / 8 * 2 - 1
        pts.append(bow_c + side_vec * (t * 0.55) - aim * (0.18 * t * t))
    for a, b in zip(pts, pts[1:]):
        tapered_post(a, b, 0.035, 0.035, mat('timber_dark'))
    tapered_post(pts[0], base_p + aim * 0.15 + Vector((0, 0, 0.1)), 0.008, 0.008, mat('rope'))
    tapered_post(pts[-1], base_p + aim * 0.15 + Vector((0, 0, 0.1)), 0.008, 0.008, mat('rope'))
    tapered_post(base_p + aim * 0.05 + Vector((0, 0, 0.16)), base_p + aim * 0.95 + Vector((0, 0, 0.16)), 0.018, 0.018, mat('steel'))
    # A red crest banner hanging on the lit (left) face, below the deck.
    bz = top - 0.08
    bverts = []
    bfaces = []
    cols, rows = 4, 6
    width, length = 0.56, 1.05
    for r in range(rows + 1):
        for c in range(cols + 1):
            u = c / cols - 0.5
            v = r / rows
            wave = 0.03 * math.sin(v * 5 + u * 2)
            bverts.append((-0.83 - wave, u * width, bz - v * length))
    for r in range(rows):
        for c in range(cols):
            i = r * (cols + 1) + c
            bfaces.append((i, i + cols + 1, i + cols + 2, i + 1))
    # Swallow-tail cut at the bottom.
    mesh_object('Banner', bverts, bfaces, mat('banner'), 0, 0.015, smooth=True).modifiers['Outline'].thickness = 0.015
    cylinder((-0.86, 0, bz + 0.02), 0.025, width + 0.16, mat('gold'), verts=8, rot=(math.pi / 2, 0, 0))
    crest_c = Vector((-0.87, 0, bz - 0.45))
    cylinder(crest_c, 0.17, 0.02, mat('gold', rough=0.35, metal=0.3), verts=24, rot=(0, math.pi / 2, 0), outline=0.012)
    cylinder(crest_c + Vector((-0.01, 0, 0)), 0.12, 0.02, mat('banner'), verts=24, rot=(0, math.pi / 2, 0), outline=0)
    for k in range(3):
        a = k * 2 * math.pi / 3
        p = crest_c + Vector((-0.02, math.cos(a) * 0.05, math.sin(a) * 0.05))
        sphere(p, 0.045, mat('gold', rough=0.35, metal=0.3), outline=0)
    # A paper lantern hanging from the deck corner on the right.
    lantern = Vector((0.82, -0.82, top - 0.25))
    tapered_post(lantern + Vector((0, 0, 0.12)), lantern + Vector((0, 0, 0.32)), 0.01, 0.01, mat('rope'))
    sphere(lantern, 0.13, mat('glow', emit=3.0), scale=(1, 1, 1.25), outline=0.015)
    box(tuple(lantern + Vector((0, 0, 0.17))), (0.12, 0.12, 0.04), dark, bevel=0.01, outline=0.01)
    box(tuple(lantern + Vector((0, 0, -0.17))), (0.12, 0.12, 0.04), dark, bevel=0.01, outline=0.01)
    return half


def rock(center, size, material, seed, flat=True, squash=0.75, outline=0.03):
    """A chunky faceted boulder: a low-poly sphere with its points pushed in and out."""
    r = random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=center)
    o = bpy.context.active_object
    for v in o.data.vertices:
        v.co *= 1 + r.uniform(-0.18, 0.12)
    o.scale = (size * r.uniform(0.9, 1.15), size * r.uniform(0.9, 1.15), size * squash)
    o.rotation_euler = (0, 0, r.uniform(0, 6.28))
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    if not flat:
        bpy.ops.object.shade_smooth()
    return finish(o, material, 0, outline)


def torch(at, level):
    """A torch on a post, flame glowing."""
    at = Vector(at)
    tapered_post(at, at + Vector((0, 0, 0.75)), 0.045, 0.04, mat('timber_dark'))
    cylinder(at + Vector((0, 0, 0.8)), 0.08, 0.12, mat('gold' if level >= 5 else 'iron', metal=0.4, rough=0.4), verts=8, radius2=0.11)
    cylinder(at + Vector((0, 0, 0.98)), 0.09, 0.26, mat('flame', emit=6.0), verts=8, radius2=0.0, outline=0.012)
    sphere(at + Vector((0, 0, 0.9)), 0.07, mat('glow', emit=8.0), outline=0)


def gold_mine(level):
    """
    A gold mine on a 3x3 footprint: a mound of faceted boulders with moss, a timber-framed tunnel glowing gold
    on the front-right face (-Y), rails running out to a cart heaped with nuggets, and torches either side.
    Higher levels add more gold, gold trim, lanterns and glowing crystals.
    """
    half = 1.5
    # Earth slab filling the footprint, with a kerb of small stones.
    box((0, 0, 0.06), (half * 2 - 0.06, half * 2 - 0.06, 0.12), mat('earth'), bevel=0.04, outline=0.02)
    seed = 11
    for side in range(4):
        ang = side * math.pi / 2
        for i in range(7):
            p = Vector((-half + 0.2 + i * (2 * half - 0.4) / 6, -half + 0.16, 0.12))
            p.rotate(Matrix.Rotation(ang, 3, 'Z'))
            seed += 1
            rock(tuple(p), 0.16, mat('rock'), seed, outline=0.015)
    # The mound: big boulders at the back, smaller towards the front, moss on top.
    mound = [((0.55, 0.65, 0.55), 0.95), ((-0.45, 0.75, 0.5), 0.85), ((0.9, -0.05, 0.4), 0.7), ((-0.85, 0.05, 0.38), 0.72),
             ((0.1, 0.25, 0.95), 0.8), ((0.55, 0.4, 1.25), 0.5), ((-0.25, 0.55, 1.2), 0.55), ((0.95, 0.85, 0.3), 0.6),
             ((-0.9, 0.85, 0.3), 0.6), ((0.0, 0.95, 0.35), 0.65)]
    mound = [((c[0] * 0.9, c[1] * 0.8 + 0.3, c[2] * 0.9), s * 0.85) for c, s in mound]
    for i, (c, s) in enumerate(mound):
        rock(c, s, mat('rock' if i % 3 else 'rock_dark'), 100 + i)
        if c[2] > 0.7 or i % 2 == 0:
            moss = sphere((c[0], c[1], c[2] + s * 0.55), s * 0.55, mat('moss'), scale=(1, 1, 0.35), outline=0.02)
    # Gold veins in the rock, more with each level.
    for i in range(2 + level):
        r = random.Random(500 + i)
        c = mound[r.randrange(len(mound))]
        p = Vector(c[0]) + Vector((r.uniform(-0.3, 0.3), -abs(r.uniform(0.2, 0.5)), r.uniform(-0.1, 0.3)))
        sphere(tuple(p), 0.07 + 0.01 * level, mat('nugget', emit=0.6, metal=0.6), outline=0.012)
    # Tunnel mouth on the front-right face: dark cave, golden glow, timber frame with a little shingle roof.
    door = Vector((0.15, -0.55, 0))
    box(tuple(door + Vector((0, 0.2, 0.64))), (1.12, 0.6, 1.28), mat('cave'), bevel=0.02, outline=0)
    box(tuple(door + Vector((0, -0.115, 0.42))), (0.86, 0.02, 0.72), mat('glow', '#FFB040', emit=1.6 + 0.15 * level), bevel=0, outline=0)
    box(tuple(door + Vector((0, -0.12, 0.85))), (0.86, 0.025, 0.4), mat('cave'), bevel=0, outline=0)
    frame = mat('gold') if level >= 6 else mat('timber_dark')
    for sx in (-0.6, 0.6):
        tapered_post(door + Vector((sx, -0.12, 0)), door + Vector((sx * 0.92, -0.12, 1.35)), 0.1, 0.085, mat('timber'))
    tapered_post(door + Vector((-0.78, -0.14, 1.38)), door + Vector((0.78, -0.14, 1.38)), 0.1, 0.1, frame)
    tapered_post(door + Vector((-0.55, -0.14, 1.12)), door + Vector((0.55, -0.14, 1.12)), 0.04, 0.04, mat('rope'))
    if level >= 2:
        # A red crest cloth hung from the lintel.
        box(tuple(door + Vector((0, -0.12, 0.9))), (0.32, 0.03, 0.34), mat('banner'), bevel=0.01, outline=0.012)
        cylinder(door + Vector((0, -0.14, 0.9)), 0.08, 0.02, mat('gold'), verts=16, rot=(math.pi / 2, 0, 0), outline=0.008)
    # Rails and sleepers running out of the tunnel to the front edge.
    for sx in (-0.17, 0.17):
        tapered_post(door + Vector((sx, 0.1, 0.16)), Vector((door.x + sx, -half + 0.15, 0.16)), 0.022, 0.022, mat('steel', metal=0.6, rough=0.4))
    for k in range(5):
        y = door.y + 0.05 - k * 0.2
        box((door.x, y, 0.13), (0.5, 0.1, 0.04), mat('timber_dark'), bevel=0.01, outline=0.01)
    # The mine cart, heaped with nuggets.
    cart = Vector((door.x + 0.05, -1.0, 0.0))
    box(tuple(cart + Vector((0, 0, 0.42))), (0.62, 0.5, 0.36), mat('timber'), bevel=0.03, outline=0.022)
    for zz in (0.3, 0.55):
        box(tuple(cart + Vector((0, 0, zz))), (0.65, 0.53, 0.05), mat('gold' if level >= 5 else 'iron', metal=0.5, rough=0.4), bevel=0.01, outline=0.012)
    for sx in (-0.22, 0.22):
        for sy in (-0.27, 0.27):
            cylinder(cart + Vector((sx, sy, 0.2)), 0.11, 0.06, mat('iron', metal=0.5, rough=0.5), verts=12, rot=(math.pi / 2, 0, 0), outline=0.015)
    r = random.Random(9)
    for i in range(10 + level * 3):
        p = cart + Vector((r.uniform(-0.25, 0.25), r.uniform(-0.2, 0.2), 0.62 + r.uniform(0, 0.12 + 0.02 * level)))
        rock(tuple(p), r.uniform(0.07, 0.11), mat('nugget', metal=0.7, rough=0.3), 900 + i, squash=0.8, outline=0.012)
    # Torches either side of the tunnel; lanterns from level 4; glowing crystals from level 7.
    torch((door.x - 0.75, door.y - 0.2, 0.1), level)
    torch((door.x + 0.75, door.y - 0.2, 0.1), level)
    if level >= 7:
        for i, c in enumerate(((-0.5, 0.5, 1.5), (0.6, 0.6, 1.55))):
            cylinder(c, 0.1, 0.4, mat('glow', '#5FE0FF', emit=3.0), verts=6, radius2=0.0, outline=0.015)
    return half


BUILDERS = {'defense_tower': defense_tower, 'gold_mine': gold_mine}
half = BUILDERS[BUILDING](LEVEL)

# ---------------------------------------------------------------- light and camera

sun = bpy.data.lights.new('Sun', 'SUN')
sun.energy = 4.6
sun.color = hexc('#FFE8C8')[:3]
sun.angle = math.radians(8)
sun_obj = bpy.data.objects.new('Sun', sun)
scene.collection.objects.link(sun_obj)
# From the upper left of the screen: the camera's left is world -X+Y, so light travels towards +X-Y and down.
sun_obj.rotation_euler = (math.radians(50), 0, math.radians(-110))

fill = bpy.data.lights.new('Fill', 'SUN')
fill.energy = 0.7
fill.color = hexc('#B8C8FF')[:3]
fill_obj = bpy.data.objects.new('Fill', fill)
scene.collection.objects.link(fill_obj)
fill_obj.rotation_euler = (math.radians(60), 0, math.radians(60))

cam_data = bpy.data.cameras.new('Cam')
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new('Cam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam.rotation_euler = (math.radians(60), 0, math.radians(-45))
direction = cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))
cam.location = -direction * 40 + Vector((0, 0, 1.5))
bpy.context.view_layer.update()

# Fit: the whole model inside the frame with a margin, and the footprint's front corner near the bottom centre.
deps = bpy.context.evaluated_depsgraph_get()
points = []
for o in parts:
    ev = o.evaluated_get(deps)
    points += [o.matrix_world @ Vector(c) for c in ev.bound_box]
front = Vector((-half, -half, 0))
cam_data.ortho_scale = 6.0
for _ in range(3):
    bpy.context.view_layer.update()
    proj = [world_to_camera_view(scene, cam, p) for p in points]
    fx = world_to_camera_view(scene, cam, front)
    ys = [p.y for p in proj]
    xs = [p.x for p in proj]
    span = max(max(ys) - fx.y, (max(xs) - min(xs)))
    cam_data.ortho_scale *= span / 0.9
    bpy.context.view_layer.update()
    fx = world_to_camera_view(scene, cam, front)
    cam_data.shift_x += (fx.x - 0.5)
    cam_data.shift_y += (fx.y - 0.03)

name = '%s_level_%02d.png' % (BUILDING, LEVEL)
scene.render.filepath = os.path.join(OUT, name)
bpy.ops.render.render(write_still=True)
print('RENDERED', scene.render.filepath)
