# Blender script: packs one Meshy hero (rig + walk/run + library clips) into a single game-ready FBX.
#
#   blender -b --factory-startup --python tools/art/pack_hero_3d.py -- <hero folder> <out folder> <hero id> [staff]
#
# The hero folder is Downloads/SD_Heroes_3D/<id> as made by the Meshy CLI (see its EXPORT_REPORT.md).
# Output: <out>/<id>.fbx with the skinned mesh, the 24-bone armature and clips named idle, walk, run, attack,
# special, hit and death, plus <out>/<id>_color.png (1024 px). Clips are cut and kept in place:
# - attack is one strike: the frames around the moment a hand reaches furthest from the hips.
# - attack and special keep the hips' height but not their sideways travel, so the hero never drifts.
# With "staff", the separate staff (model/split/prop.glb) is fitted back to the body and parented to RightHand.
import bpy, sys, os, math
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
src, out, hero = os.path.abspath(args[0]), os.path.abspath(args[1]), args[2]
with_staff = len(args) > 3 and args[3] == 'staff'
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30


def import_fbx(path):
    before = set(bpy.data.objects)
    actions_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before], [a for a in bpy.data.actions if a not in actions_before]


def delete(objects):
    for o in objects:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0 and isinstance(data, (bpy.types.Mesh, bpy.types.Armature)):
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            else:
                bpy.data.armatures.remove(data)


objects, rest_actions = import_fbx(os.path.join(src, 'rig', 'rigged_character.fbx'))
arm = next(o for o in objects if o.type == 'ARMATURE')
body = next(o for o in objects if o.type == 'MESH')
for a in rest_actions:
    bpy.data.actions.remove(a)

# Library clips by their Meshy names, mapped to the game's names.
names = {
    'idle': ['Idle'],
    'attack': ['Kung_Fu_Punch', 'Right_Hand_Sword_Slash', 'mage_soell_cast', 'Archery_Shot', 'Roundhouse_Kick'],
    'special': ['Punch_Forward_with_Both_Fists', 'Charged_Slash', 'Charged_Spell_Cast', 'Draw_and_Shoot_from_Back', 'Flying_Fist_Kick'],
    'hit': ['Face_Punch_Reaction'],
    'death': ['dying_backwards'],
}
clips = {}
for path, fixed in ((os.path.join(src, 'rig', 'walking_fbx.fbx'), 'walk'), (os.path.join(src, 'rig', 'running_fbx.fbx'), 'run'), (os.path.join(src, 'animations', 'animation_fbx.fbx'), None)):
    objs, acts = import_fbx(path)
    delete(objs)
    for a in acts:
        short = a.name.split('|')[-2] if a.name.endswith('baselayer') else a.name.split('|')[-1]
        game = fixed or next((k for k, v in names.items() if short in v), None)
        if game is None or game in clips:
            bpy.data.actions.remove(a)
            continue
        a.name = game
        a.use_fake_user = True
        clips[game] = a
missing = [k for k in ('idle', 'walk', 'run', 'attack', 'special', 'hit', 'death') if k not in clips]
if missing:
    raise SystemExit('MISSING CLIPS ' + str(missing))


def assign(action):
    arm.animation_data_create()
    arm.animation_data.action = action
    if getattr(action, 'slots', None) and len(action.slots):
        arm.animation_data.action_slot = action.slots[0]


hips = arm.pose.bones['Hips']


def strike_frame(action):
    """The frame where either hand is furthest from the hips, ignoring height."""
    assign(action)
    a, b = int(action.frame_range[0]), int(action.frame_range[1])
    best, best_frame = -1.0, a
    for f in range(a, b + 1):
        scene.frame_set(f)
        root = arm.matrix_world @ hips.head
        for side in ('LeftHand', 'RightHand'):
            hand = arm.matrix_world @ arm.pose.bones[side].head
            d = math.hypot(hand.x - root.x, hand.y - root.y)
            if d > best:
                best, best_frame = d, f
    return best_frame


def keep_in_place(action):
    """Hips keep their height (bone Y) but not their sideways travel (bone X and Z)."""
    for fc in action.fcurves:
        if fc.data_path == 'pose.bones["Hips"].location' and fc.array_index in (0, 2) and len(fc.keyframe_points):
            first = fc.keyframe_points[0].co[1]
            for k in fc.keyframe_points:
                k.co[1] = first
                k.handle_left[1] = first
                k.handle_right[1] = first


def cut(action, longest, before, length):
    """A clip longer than `longest` frames keeps `length` frames, starting `before` frames ahead of the strike."""
    if action.frame_range[1] - action.frame_range[0] <= longest:
        return
    peak = strike_frame(action)
    start = max(int(action.frame_range[0]), min(peak - before, int(action.frame_range[1]) - length + 1))
    action.use_frame_range = True
    action.frame_start = start
    action.frame_end = start + length - 1


attack = clips['attack']
cut(attack, 60, 18, 35)
# Very long specials (Draw_and_Shoot_from_Back is 236 frames) become one move of two seconds.
cut(clips['special'], 150, 40, 60)
keep_in_place(attack)
keep_in_place(clips['special'])

if with_staff:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, 'model', 'split', 'prop.glb'))
    staff = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
    for o in [o for o in bpy.data.objects if o not in before and o is not staff]:
        bpy.data.objects.remove(o, do_unlink=True)
    # The staff was cut from the unrigged model, which Meshy sized to about 0.95 and centred. Fit it with the
    # same scale and offset that take the unrigged body (body.glb) onto the rigged body.
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, 'model', 'split', 'body.glb'))
    ref = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')

    def bounds(o):
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))

    assign(clips['idle'])
    arm.animation_data.action = None
    scene.frame_set(0)
    rlo, rhi = bounds(ref)
    blo, bhi = bounds(body)
    s = (bhi.z - blo.z) / (rhi.z - rlo.z)
    centre_ref = (rlo + rhi) / 2
    centre_body = (blo + bhi) / 2
    staff.matrix_world = staff.matrix_world.copy()
    for v in staff.data.vertices:
        world = staff.matrix_world @ v.co
        v.co = staff.matrix_world.inverted() @ (centre_body + (world - centre_ref) * s)
    for o in [o for o in bpy.data.objects if o not in before]:
        bpy.data.objects.remove(o, do_unlink=True)
    # Back to the rest pose: a pose left over from measuring clips would skew the parenting.
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    # Slide the shaft into the nearer hand (it was cut standing just beside it), then parent it there.
    pts = [staff.matrix_world @ v.co for v in staff.data.vertices]
    low = sorted(pts, key=lambda p: p.z)[: max(1, len(pts) // 3)]
    shaft = Vector((sum(p.x for p in low) / len(low), sum(p.y for p in low) / len(low), 0))
    hands = {name: arm.matrix_world @ arm.pose.bones[name].tail for name in ('LeftHand', 'RightHand')}
    grip = min(hands, key=lambda n: (hands[n].x - shaft.x) ** 2 + (hands[n].y - shaft.y) ** 2)
    shift = Vector((hands[grip].x - shaft.x, hands[grip].y - shaft.y, 0))
    for v in staff.data.vertices:
        v.co = staff.matrix_world.inverted() @ (staff.matrix_world @ v.co + shift)
    keep = staff.matrix_world.copy()
    staff.parent = arm
    staff.parent_type = 'BONE'
    staff.parent_bone = grip
    staff.matrix_world = keep
    print('STAFF in', grip, 'moved', tuple(round(c, 3) for c in shift))
    staff.name = 'Staff'
    # The staff shares the body's material, so one texture serves both.
    staff.data.materials.clear()
    staff.data.materials.append(body.material_slots[0].material)
    print('STAFF fitted, scale', round(s, 3))

# The colour texture, at 1024 px, saved next to the FBX.
material = body.material_slots[0].material
node = next(n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
image = node.image
if not image.has_data or image.size[0] == 0:
    # Some rig FBXs point to a texture file Meshy never downloaded; the rig's GLB carries the same one inside.
    before_images = set(bpy.data.images)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, 'rig', 'rigged_character.glb'))
    delete([o for o in bpy.data.objects if o not in before])
    image = max((i for i in bpy.data.images if i not in before_images and i.size[0] > 0), key=lambda i: i.size[0] * i.size[1])
    node.image = image
    print('TEXTURE from rigged_character.glb', tuple(image.size))
image.scale(1024, 1024)
color_path = os.path.join(out, hero + '_color.png')
image.filepath_raw = color_path
image.file_format = 'PNG'
if image.packed_file:
    image.unpack(method="REMOVE")  # keep the pixels, drop the pack, so save writes the file
image.save(filepath=color_path)
if not os.path.exists(color_path):
    raise SystemExit("TEXTURE NOT SAVED " + color_path)
image.filepath = color_path
material.name = hero

# Each clip on its own NLA strip, bound to its action slot (Blender 4.4+ needs the slot, or the clip bakes
# static), and exported as one FBX take per strip.
arm.animation_data_create()
arm.animation_data.action = None
order = ('idle', 'walk', 'run', 'attack', 'special', 'hit', 'death')
for k in order:
    a = clips[k]
    track = arm.animation_data.nla_tracks.new()
    track.name = k
    strip = track.strips.new(k, int(a.frame_range[0]), a)
    if getattr(a, 'slots', None) and len(a.slots) and hasattr(strip, 'action_slot'):
        strip.action_slot = a.slots[0]
    strip.name = k
for o in list(bpy.data.objects):
    o.select_set(o.type in ('ARMATURE', 'MESH'))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=os.path.join(out, hero + '.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
for k in ('idle', 'walk', 'run', 'attack', 'special', 'hit', 'death'):
    a = clips[k]
    print('CLIP', k, int(a.frame_range[0]), int(a.frame_range[1]))
print('PACKED', hero)
