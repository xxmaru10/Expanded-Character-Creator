"""
Gerador de miniaturas OFFLINE (fora do jogo) para o Custom Body Parts.

Renderiza cada peça (.obj + textura) para um PNG 512x512 de fundo transparente, com o MESMO nome de
arquivo que o jogo procura (thumbs/<key sanitizada>.png), lendo os caminhos do scales.json. Assim o
jogo carrega as miniaturas prontas (ThumbnailStore.TryLoad) sem precisar gerar nada em tempo real —
evita o travamento do botão "Gerar miniaturas" com biblioteca gigante.

Requer Blender (testado no 4.1). NÃO abre o jogo.

USO (com o jogo FECHADO nao e necessario, mas o Blender sim):
  blender --background --python gen_thumbnails.py -- \
      --game "D:/SteamLibrary/steamapps/common/The RPG Engine" \
      --only-missing            # (padrao) so gera as que faltam
  Opcoes:
      --limit N                 # so as N primeiras que faltam (teste)
      --all                     # regenerar TODAS (ignora as existentes)
      --fill 0.9                # fracao do quadro que a peca ocupa (zoom)
      --yaw 0  --pitch 20       # angulo da camera (graus): yaw=giro, pitch=inclinacao
      --out PASTA               # pasta de saida (padrao: <game>/CustomParts/thumbs)
"""
import bpy, sys, os, json, glob, math, argparse
from mathutils import Vector

# ------------------------------------------------------------------ args
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--game", required=True, help="raiz do jogo (a pasta 'The RPG Engine')")
ap.add_argument("--out", default=None)
ap.add_argument("--only-missing", action="store_true", default=True)
ap.add_argument("--all", action="store_true", help="regerar todas, ignorando as existentes")
ap.add_argument("--limit", type=int, default=0)
ap.add_argument("--grep", default=None, help="so gerar peças cuja key contenha esse texto (ex.: panturrilha)")
ap.add_argument("--fill", type=float, default=0.9)
ap.add_argument("--yaw", type=float, default=0.0)
ap.add_argument("--pitch", type=float, default=18.0)
args = ap.parse_args(argv)

CUSTOM = os.path.join(args.game, "CustomParts")
SCALES = os.path.join(CUSTOM, "scales.json")
THUMBS = args.out or os.path.join(CUSTOM, "thumbs")
os.makedirs(THUMBS, exist_ok=True)

INVALID = set('<>:"/\\|?*')
def sanitize(key):
    # espelha o ThumbnailStore.Sanitize do C# (troca chars invalidos de nome de arquivo por '_')
    return ''.join('_' if (c in INVALID or ord(c) < 32) else c for c in key)

def resolve(path):
    """Caminho real no disco, tolerando a corrupcao de encoding do scales.json (acento vira U+FFFD)."""
    if not path:
        return None
    if os.path.isfile(path):
        return path
    if '�' in path:
        d, name = os.path.dirname(path), os.path.basename(path)
        pat = name.replace('�', '*')
        while '**' in pat:
            pat = pat.replace('**', '*')
        m = glob.glob(os.path.join(d, pat))
        if m:
            return m[0]
    return None

# ------------------------------------------------------------------ ler scales.json
records = []
with open(SCALES, 'r', encoding='utf-8', errors='replace') as f:  # errors='replace' = mesmo U+FFFD do jogo
    for line in f:
        line = line.strip()
        if not line:
            continue
        try:
            e = json.loads(line)
        except Exception:
            continue
        key, model = e.get('key'), e.get('model')
        if not key or not model or key.startswith('__'):
            continue
        records.append(e)

# fila: so as que faltam (a menos de --all)
todo = []
for e in records:
    if args.grep and args.grep.lower() not in e['key'].lower():
        continue
    png = os.path.join(THUMBS, sanitize(e['key']) + ".png")
    if not args.all and os.path.isfile(png):
        continue
    todo.append(e)
if args.limit > 0:
    todo = todo[:args.limit]

print(f"[thumbs] {len(records)} pecas no scales.json; {len(todo)} a gerar.")

# ------------------------------------------------------------------ render setup
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'  # Blender 4.1
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'  # cores fieis a textura (sem tonemap Filmic)

cam_data = bpy.data.cameras.new("ThumbCam")
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new("ThumbCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

def purge():
    # apaga tudo menos a camera + limpa data blocks (evita inchar a memoria em 15k iteracoes)
    for o in list(bpy.data.objects):
        if o is cam:
            continue
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.textures):
        for b in list(coll):
            if b.users == 0:
                coll.remove(b)

def import_obj(path):
    before = set(bpy.data.objects)
    try:
        bpy.ops.wm.obj_import(filepath=path)
    except Exception as ex:
        print(f"[thumbs] falha import '{path}': {ex}")
        return []
    return [o for o in bpy.data.objects if o not in before and o.type == 'MESH']

def flat_texture_material(tex_path):
    mat = bpy.data.materials.new("ThumbMat")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emis = nt.nodes.new("ShaderNodeEmission")   # unlit: sempre visivel, independe de luz
    nt.links.new(emis.outputs["Emission"], out.inputs["Surface"])
    if tex_path and os.path.isfile(tex_path):
        try:
            img = bpy.data.images.load(tex_path, check_existing=True)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            nt.links.new(tex.outputs["Color"], emis.inputs["Color"])
        except Exception as ex:
            print(f"[thumbs] textura falhou '{tex_path}': {ex}")
            emis.inputs["Color"].default_value = (0.7, 0.7, 0.72, 1)
    else:
        emis.inputs["Color"].default_value = (0.7, 0.7, 0.72, 1)
    return mat

def world_bbox(objs):
    mn = Vector((1e18, 1e18, 1e18))
    mx = Vector((-1e18, -1e18, -1e18))
    for o in objs:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    return mn, mx

# Pernas (coxa=knee, panturrilha=legLower) vem com o "front" apontando pro LADO, nao pra frente como
# o torso. Giro por-slot pra virar de frente; esquerda e direita sao ESPELHADAS, entao giram ao
# contrario (direita +90 mostra a frente; a esquerda no +90 mostra as costas -> usa -90).
LEG_YAW = {"kneer": 90, "leglowerr": 90, "kneel": -90, "leglowerl": -90}

def leg_yaw_offset(slot):
    return LEG_YAW.get((slot or "").lower(), 0)

def frame_and_render(objs, out_png, extra_yaw=0.0):
    mn, mx = world_bbox(objs)
    center = (mn + mx) * 0.5
    size = mx - mn
    max_dim = max(size.x, size.y, size.z, 1e-6)

    # direcao da camera (yaw ao redor de Z, pitch pra cima)
    yaw = math.radians(args.yaw + extra_yaw)
    pitch = math.radians(args.pitch)
    dir = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    dist = max_dim * 3.0
    cam.location = center + dir * dist
    # apontar a camera pro centro
    look = (center - cam.location).normalized()
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = max_dim / max(0.1, min(1.0, args.fill))

    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)

# ------------------------------------------------------------------ loop
done = fail = 0
for i, e in enumerate(todo):
    key = e['key']
    obj_path = resolve(e.get('model'))
    if not obj_path:
        fail += 1
        continue
    tex_path = resolve(e.get('tex')) or (resolve(e.get('texVariants', [None])[0]) if e.get('texVariants') else None)

    purge()
    objs = import_obj(obj_path)
    if not objs:
        fail += 1
        continue
    mat = flat_texture_material(tex_path)
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)

    out_png = os.path.join(THUMBS, sanitize(key) + ".png")
    try:
        frame_and_render(objs, out_png, extra_yaw=leg_yaw_offset(e.get('slot')))
        done += 1
    except Exception as ex:
        print(f"[thumbs] render falhou '{key}': {ex}")
        fail += 1

    if (i + 1) % 200 == 0:
        print(f"[thumbs] {i+1}/{len(todo)} (ok={done} falha={fail})")

print(f"[thumbs] CONCLUIDO. geradas={done} falhas={fail} de {len(todo)}.")
