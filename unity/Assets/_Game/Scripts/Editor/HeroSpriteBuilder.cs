using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinobiDominion.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Builds a sprite hero from its layers and layout.json: importer settings, atlas, bone hierarchy,
    /// animation clips, Animator and prefab. Bones are transforms with one rigid sprite each.
    /// Run from the menu or with -executeMethod ShinobiDominion.EditorTools.HeroSpriteBuilder.BuildAll.
    /// </summary>
    public static class HeroSpriteBuilder
    {
        private const string ArtRoot = "Assets/_Game/Art/HeroSprites";
        private const string PrefabRoot = "Assets/_Game/Resources/HeroSprites";
        private const float HeroHeight = 1.5f;

        [Serializable] private class Layer { public string name; public string parent; public int order; public float pivotX; public float pivotY; public int width; public int height; public float worldX; public float worldY; }
        [Serializable] private class Layout { public string hero; public int form; public float canvasHeight; public float feetX; public float feetY; public List<Layer> layers; }

        [MenuItem("Shinobi Dominion/Build Sprite Heroes")]
        public static void BuildAll()
        {
            Build("solar_beast_sage", 0);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HERO_SPRITES_BUILT");
        }

        private static void Build(string hero, int form)
        {
            var folder = $"{ArtRoot}/{hero}/form{form}";
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(folder + "/layout.json"));
            var top = layout.layers.Max(l => l.worldY + l.height - l.pivotY);
            var pixelsPerUnit = (top - layout.feetY) / HeroHeight;

            foreach (var layer in layout.layers) ConfigureSprite($"{folder}/{layer.name}.png", layer, pixelsPerUnit);
            var shadowPath = folder + "/shadow.png";
            WriteShadow(shadowPath);
            ConfigureShadow(shadowPath);
            BuildAtlas(folder, layout);

            Directory.CreateDirectory($"{PrefabRoot}/{hero}");
            var material = MaterialAt(folder + "/hero_sprite.mat");

            var root = new GameObject(hero);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.gameObject.AddComponent<SortingGroup>();

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = Vector3.up * 0.015f;
            shadow.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(shadowPath);
            shadowRenderer.sharedMaterial = material;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var bones = new Dictionary<string, Transform>();
            var hips = new GameObject("hips").transform;
            hips.SetParent(visual, false);
            var hipsPivot = layout.layers.First(l => l.name == "pelvis");
            Vector2 Canvas(Layer l) => new Vector2(l.worldX, l.worldY);
            var feet = new Vector2(layout.feetX, layout.feetY);
            hips.localPosition = (Canvas(hipsPivot) - feet) / pixelsPerUnit;
            bones["hips"] = hips;
            var pending = layout.layers.ToList();
            var byName = layout.layers.ToDictionary(l => l.name);
            while (pending.Count > 0)
            {
                var ready = pending.First(l => bones.ContainsKey(l.parent));
                pending.Remove(ready);
                var bone = new GameObject(ready.name).transform;
                bone.SetParent(bones[ready.parent], false);
                var parentAt = ready.parent == "hips" ? Canvas(hipsPivot) : Canvas(byName[ready.parent]);
                bone.localPosition = (Canvas(ready) - parentAt) / pixelsPerUnit;
                bones[ready.name] = bone;
                var sprite = bone.gameObject.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{ready.name}.png");
                sprite.sharedMaterial = material;
                sprite.sortingOrder = ready.order;
                sprite.shadowCastingMode = ShadowCastingMode.Off;
            }

            var paths = bones.ToDictionary(b => b.Key, b => AnimationUtility.CalculateTransformPath(b.Value, root.transform));
            var rest = bones.ToDictionary(b => b.Key, b => b.Value.localPosition);
            var clips = new HeroClips(folder, paths, rest);
            var controller = BuildController(folder, clips.Idle(), clips.Run(), clips.Attack());

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            root.AddComponent<HeroSpriteActor>().Configure(visual, animator);

            var prefabPath = $"{PrefabRoot}/{hero}/form{form}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"Built {prefabPath}: {layout.layers.Count} layers, {pixelsPerUnit:F0} pixels per unit");
        }

        private static void ConfigureSprite(string path, Layer layer, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(layer.pivotX / layer.width, layer.pivotY / layer.height);
            settings.spritePixelsPerUnit = pixelsPerUnit;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.mipmapEnabled = true;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static void WriteShadow(string path)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.25f, 1f, distance))));
                }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static void ConfigureShadow(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void BuildAtlas(string folder, Layout layout)
        {
            EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
            var path = folder + "/hero.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                atlas.SetIncludeInBuild(true);
                atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });
                atlas.SetTextureSettings(new SpriteAtlasTextureSettings { generateMipMaps = true, sRGB = true, filterMode = FilterMode.Bilinear, readable = false });
                atlas.SetPlatformSettings(new TextureImporterPlatformSettings { maxTextureSize = 1024, format = TextureImporterFormat.Automatic, textureCompression = TextureImporterCompression.Compressed });
                AssetDatabase.CreateAsset(atlas, path);
                atlas.Add(layout.layers.Select(l => (Object)AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{l.name}.png")).ToArray());
            }
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        }

        private static Material MaterialAt(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, path);
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
            toRun.duration = 0.08f;
            toRun.hasExitTime = false;
            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            toIdle.duration = 0.12f;
            toIdle.hasExitTime = false;
            var strike = machine.AddAnyStateTransition(attackState);
            strike.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            strike.duration = 0.04f;
            strike.hasExitTime = false;
            strike.canTransitionToSelf = true;
            var backToRun = attackState.AddTransition(runState);
            backToRun.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            backToRun.hasExitTime = true;
            backToRun.exitTime = 0.92f;
            backToRun.duration = 0.08f;
            var backToIdle = attackState.AddTransition(idleState);
            backToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            backToIdle.hasExitTime = true;
            backToIdle.exitTime = 0.92f;
            backToIdle.duration = 0.08f;
            return controller;
        }

        /// <summary>Hand-keyed motion. Angles are degrees from the drawn pose; positive turns counter-clockwise.</summary>
        private sealed class HeroClips
        {
            private readonly string _folder;
            private readonly Dictionary<string, string> _paths;
            private readonly Dictionary<string, Vector3> _rest;

            public HeroClips(string folder, Dictionary<string, string> paths, Dictionary<string, Vector3> rest)
            {
                _folder = folder;
                _paths = paths;
                _rest = rest;
            }

            public AnimationClip Idle()
            {
                const float t = 1.8f;
                var clip = New("Idle", true);
                float[] times = { 0f, t / 2f, t };
                Spin(clip, "torso", times, 0f, 1.2f, 0f);
                Spin(clip, "head", times, 0f, -1.6f, 0f);
                Spin(clip, "upper_l", times, 0f, 2.5f, 0f);
                Spin(clip, "upper_r", times, 0f, -2.5f, 0f);
                Spin(clip, "fore_l", times, 0f, -2f, 0f);
                Spin(clip, "fore_r", times, 0f, 2f, 0f);
                Spin(clip, "sash", times, 0f, 2.5f, 0f);
                Move(clip, "hips", times, new[] { 0f, 0f, 0f }, new[] { 0f, -0.012f, 0f });
                Spin(clip, "thigh_l", times, 0f, -0.8f, 0f);
                Spin(clip, "thigh_r", times, 0f, 0.8f, 0f);
                Spin(clip, "shin_l", times, 0f, 1.2f, 0f);
                Spin(clip, "shin_r", times, 0f, -1.2f, 0f);
                return Save(clip);
            }

            public AnimationClip Run()
            {
                const float t = 0.56f;
                var clip = New("Run", true);
                float[] times = { 0f, t * 0.25f, t * 0.5f, t * 0.75f, t };
                // Leaning into the run, with the wide drawn stance pulled under the body.
                Spin(clip, "torso", times, -9f, -7f, -9f, -7f, -9f);
                Spin(clip, "head", times, 6f, 4f, 6f, 4f, 6f);
                Spin(clip, "thigh_r", times, 14f, -8f, -22f, -10f, 14f);
                Spin(clip, "shin_r", times, -6f, -12f, -10f, -26f, -6f);
                Spin(clip, "foot_r", times, 6f, 0f, 14f, 18f, 6f);
                Spin(clip, "thigh_l", times, -6f, -14f, 26f, 10f, -6f);
                Spin(clip, "shin_l", times, -10f, -26f, -6f, -12f, -10f);
                Spin(clip, "foot_l", times, 14f, 18f, 6f, 0f, 14f);
                // Elbows bent in towards the body, pumping a little, so the arms stay close.
                Spin(clip, "upper_r", times, -12f, -4f, 14f, 4f, -12f);
                Spin(clip, "fore_r", times, -72f, -62f, -48f, -62f, -72f);
                Spin(clip, "upper_l", times, 14f, 4f, -10f, -2f, 14f);
                Spin(clip, "fore_l", times, 48f, 62f, 72f, 62f, 48f);
                Spin(clip, "sash", times, 18f, 10f, 18f, 10f, 18f);
                Move(clip, "hips", times, new[] { 0f, 0f, 0f, 0f, 0f }, new[] { -0.02f, 0.035f, -0.02f, 0.035f, -0.02f });
                return Save(clip);
            }

            public AnimationClip Attack()
            {
                var clip = New("Attack", false);
                float[] times = { 0f, 0.14f, 0.24f, 0.36f, 0.6f };
                // Wind up, drive forward off the back foot, hold the punch, settle.
                Spin(clip, "torso", times, 0f, 8f, -13f, -11f, 0f);
                Spin(clip, "head", times, 0f, -4f, 8f, 6f, 0f);
                Spin(clip, "upper_r", times, 0f, -14f, 78f, 72f, 0f);
                Spin(clip, "fore_r", times, 0f, -82f, 8f, 2f, 0f);
                Spin(clip, "upper_l", times, 0f, 10f, 14f, 12f, 0f);
                Spin(clip, "fore_l", times, 0f, 58f, 78f, 70f, 0f);
                Spin(clip, "thigh_r", times, 0f, 6f, 20f, 18f, 0f);
                Spin(clip, "shin_r", times, 0f, -8f, -22f, -20f, 0f);
                Spin(clip, "thigh_l", times, 0f, 4f, 6f, 6f, 0f);
                Spin(clip, "shin_l", times, 0f, -6f, 2f, 2f, 0f);
                Spin(clip, "sash", times, 0f, -6f, 22f, 14f, 0f);
                Move(clip, "hips", times, new[] { 0f, -0.03f, 0.11f, 0.1f, 0f }, new[] { 0f, -0.025f, -0.035f, -0.03f, 0f });
                return Save(clip);
            }

            private AnimationClip New(string name, bool loop)
            {
                var clip = new AnimationClip { name = name, frameRate = 30f };
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            }

            private void Spin(AnimationClip clip, string bone, float[] times, params float[] degrees)
            {
                var flat = times.Select(_ => 0f).ToArray();
                Curve(clip, bone, "localEulerAnglesRaw.x", times, flat);
                Curve(clip, bone, "localEulerAnglesRaw.y", times, flat);
                Curve(clip, bone, "localEulerAnglesRaw.z", times, degrees);
            }

            private void Move(AnimationClip clip, string bone, float[] times, float[] dx, float[] dy)
            {
                var rest = _rest[bone];
                Curve(clip, bone, "m_LocalPosition.x", times, dx.Select(v => rest.x + v).ToArray());
                Curve(clip, bone, "m_LocalPosition.y", times, dy.Select(v => rest.y + v).ToArray());
                Curve(clip, bone, "m_LocalPosition.z", times, times.Select(_ => rest.z).ToArray());
            }

            private void Curve(AnimationClip clip, string bone, string property, float[] times, float[] values)
            {
                var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, values[i])).ToArray());
                for (var i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(_paths[bone], typeof(Transform), property), curve);
            }

            private AnimationClip Save(AnimationClip clip)
            {
                var path = $"{_folder}/{clip.name}.anim";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
        }
    }
}
