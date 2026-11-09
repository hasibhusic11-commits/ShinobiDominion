using System.IO;
using System.Linq;
using ShinobiDominion.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Builds the 3D heroes (ADR-037). Each Art/Heroes3D/&lt;id&gt; folder holds &lt;id&gt;.fbx, packed by
    /// tools/art/pack_hero_3d.py from the Meshy exports with clips idle, walk, run, attack, special, hit and death,
    /// and &lt;id&gt;_color.png. This sets the importers, makes an unlit material in the painted style, an Animator with
    /// the same states as the sprite heroes, and the prefab Resources/Heroes3D/&lt;id&gt; that HeroSprites spawns.
    /// Run from the menu or with -executeMethod ShinobiDominion.EditorTools.Hero3DBuilder.BuildAll.
    /// </summary>
    public static class Hero3DBuilder
    {
        private const string ArtRoot = "Assets/_Game/Art/Heroes3D";
        private const string PrefabRoot = "Assets/_Game/Resources/Heroes3D";
        private const string ShadowSprite = "Assets/_Game/Art/HeroSprites/solar_beast_sage/form0/shadow.png";
        private const string ShadowMaterial = "Assets/_Game/Art/HeroSprites/solar_beast_sage/form0/hero_sprite.mat";
        /// <summary>Same height as the sprite heroes, so callers' scales still fit.</summary>
        public const float HeroHeight = 1.5f;
        private static readonly string[] Looping = { "idle", "walk", "run" };

        [MenuItem("Shinobi Dominion/Build 3D Heroes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabRoot);
            foreach (var folder in Directory.GetDirectories(ArtRoot).OrderBy(f => f))
            {
                var hero = Path.GetFileName(folder);
                if (File.Exists($"{ArtRoot}/{hero}/{hero}.fbx")) Build(hero);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HEROES_3D_BUILT");
        }

        private static void Build(string hero)
        {
            var folder = $"{ArtRoot}/{hero}";
            var modelPath = $"{folder}/{hero}.fbx";
            ConfigureModel(modelPath);
            var material = BuildMaterial(folder, hero);
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var controller = BuildController(folder, clips["idle"], clips["run"], clips["attack"]);

            var root = new GameObject(hero);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // Like the sprite heroes: the body is drawn as one unit in the sprites' draw order, so the painted base
            // under it never paints over it.
            visual.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(visual, false);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Measure in the pose the hero is seen in: the first frame of idle. The clips place the hips themselves,
            // so the bind pose alone can sit away from where the hero really stands.
            var bindCentre = Bounds(model).center;
            clips["idle"].SampleAnimation(model, 0f);
            var idleCentre = Bounds(model).center;

            // Turn the model so its face looks along +Z (the "headfront" bone sits in front of the head), then
            // scale it to the hero height with its feet on the ground.
            var head = model.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
            var front = model.GetComponentsInChildren<Transform>().First(t => t.name == "headfront");
            var facing = front.position - head.position;
            facing.y = 0f;
            model.transform.rotation = Quaternion.FromToRotation(facing.normalized, Vector3.forward) * model.transform.rotation;
            var bounds = Bounds(model);
            var scale = HeroHeight / bounds.size.y;
            model.transform.localScale *= scale;
            bounds = Bounds(model);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = Vector3.up * 0.015f;
            shadow.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSprite);
            shadowRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterial);
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            root.AddComponent<HeroSpriteActor>().Configure(visual, animator, true);

            var prefabPath = $"{PrefabRoot}/{hero}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"Built {prefabPath}: scale {scale:F3}, idle moves the body {(idleCentre - bindCentre).magnitude:F2} from the bind pose, clips {string.Join(",", clips.Keys)}");
        }

        /// <summary>Loads each built prefab, runs its Animator a little and reports where the body ends up against the
        /// hero's own position. A 3D hero must stand on its spot.</summary>
        [MenuItem("Shinobi Dominion/Check 3D Heroes")]
        public static void CheckAll()
        {
            foreach (var path in Directory.GetFiles(PrefabRoot, "*.prefab").OrderBy(p => p))
            {
                var root = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/')));
                var animator = root.GetComponentInChildren<Animator>();
                var before = Bounds(root).center;
                animator.Rebind();
                for (var i = 0; i < 10; i++) animator.Update(0.05f);
                var after = Bounds(root).center;
                var hips = root.GetComponentsInChildren<Transform>().First(t => t.name == "Hips").position;
                Debug.Log($"CHECK {Path.GetFileNameWithoutExtension(path)}: body at rest {Flat(before)}, playing {Flat(after)}, hips {Flat(hips)}, height {Bounds(root).size.y:F2}");
                Object.DestroyImmediate(root);
            }
            Debug.Log("HEROES_3D_CHECKED");
        }

        private static string Flat(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";

        private static Bounds Bounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bounds = new Bounds();
            var first = true;
            foreach (var r in renderers)
            {
                var baked = new Mesh();
                r.BakeMesh(baked, false);
                foreach (var v in baked.vertices)
                {
                    var world = r.transform.TransformPoint(v);
                    if (first) bounds = new Bounds(world, Vector3.zero);
                    else bounds.Encapsulate(world);
                    first = false;
                }
                Object.DestroyImmediate(baked);
            }
            return bounds;
        }

        private static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            var takes = importer.defaultClipAnimations;
            foreach (var take in takes)
            {
                take.name = take.name.Split('|').Last();
                take.loopTime = Looping.Contains(take.name);
                take.lockRootRotation = false;
                take.keepOriginalPositionY = true;
            }
            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(string folder, string hero)
        {
            var texturePath = $"{folder}/{hero}_color.png";
            var texture = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            texture.maxTextureSize = 1024;
            texture.mipmapEnabled = true;
            texture.textureCompression = TextureImporterCompression.Compressed;
            texture.SaveAndReimport();

            // Unlit, like the painted buildings: the texture already carries the painting's light and shade.
            var path = $"{folder}/{hero}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            // Drawn with the painted sprites (the transparent pass), so the village's draw order applies and the hero
            // draws after its base instead of under it. A plain queue override is not enough: URP resets the queue of
            // an "opaque" material on reimport. So the surface is transparent (the texture is fully opaque, so it
            // looks solid) with depth writing forced on, so the body still hides its own far side.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWriteControl", 1f);
            material.SetFloat("_ZWrite", 1f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController(string folder, AnimationClip idle, AnimationClip run, AnimationClip attack)
        {
            var path = folder + "/hero.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState(HeroSpriteActor.IdleState);
            idleState.motion = idle;
            var runState = machine.AddState(HeroSpriteActor.RunState);
            runState.motion = run;
            var attackState = machine.AddState(HeroSpriteActor.AttackState);
            attackState.motion = attack;
            machine.defaultState = idleState;

            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            toRun.duration = 0.1f;
            toRun.hasExitTime = false;
            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            toIdle.duration = 0.15f;
            toIdle.hasExitTime = false;
            var strike = machine.AddAnyStateTransition(attackState);
            strike.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            strike.duration = 0.06f;
            strike.hasExitTime = false;
            strike.canTransitionToSelf = true;
            var backToRun = attackState.AddTransition(runState);
            backToRun.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            backToRun.hasExitTime = true;
            backToRun.exitTime = 0.9f;
            backToRun.duration = 0.1f;
            var backToIdle = attackState.AddTransition(idleState);
            backToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            backToIdle.hasExitTime = true;
            backToIdle.exitTime = 0.9f;
            backToIdle.duration = 0.1f;
            return controller;
        }
    }
}
