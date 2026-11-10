using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The battle's effect pictures: painted bursts, shots that fly to their target, rings on the ground, flying
    /// chips and a building's last moment. All of it is for show; nothing here changes the battle.
    /// </summary>
    public sealed partial class BattleStage
    {
        /// <summary>Effect pictures draw over the whole world and under health bars, numbers and the HUD.</summary>
        private const int FxOrder = 20000;
        private const int MaxFlashes = 180;
        private const int MaxShards = 120;

        private sealed class Flash
        {
            public Transform Item;
            /// <summary>Null for the old round orb, which stands in when a picture is missing.</summary>
            public SpriteRenderer Sprite;
            public Vector3 From;
            public Vector3 To;
            public float Age;
            public float Duration;
            public float Delay;
            public float StartSize;
            public float EndSize;
            public float Arc;
            public float Angle;
            public float Spin;
            public bool Aim;
            public bool Flat;
            public bool Ease;
            public float Unit = 1f;
            public Color Tint = Color.white;
            /// <summary>The share of its life after which it fades out; 1 or more never fades.</summary>
            public float FadeFrom = 1f;
            public Transform Target;
            public Vector3 TargetOffset;
            /// <summary>Stays on its target the whole time instead of flying to it.</summary>
            public bool Stick;
            public Action<Vector3> Landed;
        }

        private sealed class Shard
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Color Tint;
        }

        private sealed class Falling
        {
            public SpriteRenderer Renderer;
            public Vector3 Scale;
            public Color Color;
            public float Age;
        }

        private readonly Dictionary<string, Sprite> _fxSprites = new Dictionary<string, Sprite>();
        private readonly Stack<SpriteRenderer> _fxPool = new Stack<SpriteRenderer>();
        private readonly List<Shard> _shards = new List<Shard>();
        private readonly List<Falling> _falling = new List<Falling>();
        private readonly List<KeyValuePair<float, Action>> _later = new List<KeyValuePair<float, Action>>();
        private readonly List<UnityEngine.Object> _fxOwned = new List<UnityEngine.Object>();
        private readonly System.Random _fxRandom = new System.Random(7);
        private Material _fxMaterial;
        private bool _fxShaderMissing;
        private Material _spriteMaterial;
        private Sprite _ringSprite;
        private Sprite _dotSprite;
        private int _fxSerial;
        private float _fxClock;
        private IsoCamera _rig;
        private bool _rigLooked;

        // ---------- Pictures, material and pool ----------

        private Sprite Fx(string name)
        {
            if (_fxSprites.TryGetValue(name, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Effects/" + name);
            _fxSprites[name] = sprite;
            return sprite;
        }

        /// <summary>Draws over everything in the world; null when the shader is missing, and the plain sprite look is used.</summary>
        private Material FxMaterial()
        {
            if (_fxMaterial != null) return _fxMaterial;
            if (_fxShaderMissing) return null;
            var shader = Resources.Load<Shader>("Shaders/EffectSprite");
            if (shader == null || !shader.isSupported)
            {
                _fxShaderMissing = true;
                return null;
            }
            _fxMaterial = new Material(shader) { name = "EffectSprite" };
            return _fxMaterial;
        }

        private SpriteRenderer TakeSprite(Sprite sprite, bool ground)
        {
            SpriteRenderer renderer = null;
            while (_fxPool.Count > 0 && renderer == null) renderer = _fxPool.Pop();
            if (renderer == null)
            {
                var go = new GameObject("Fx");
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (_spriteMaterial == null) _spriteMaterial = renderer.sharedMaterial;
            }
            renderer.sprite = sprite;
            renderer.sharedMaterial = ground ? _spriteMaterial : (FxMaterial() ?? _spriteMaterial);
            renderer.sortingOrder = ground ? BuildingSprites.ShadowOrder + 2 : FxOrder + (_fxSerial++ % 2000);
            renderer.color = Color.white;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private void ReleaseSprite(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.gameObject.SetActive(false);
            _fxPool.Push(renderer);
        }

        private void Release(Flash flash)
        {
            if (flash.Item == null) return;
            if (flash.Sprite == null) Destroy(flash.Item.gameObject);
            else ReleaseSprite(flash.Sprite);
        }

        /// <summary>A soft white round picture made here, so rings and chips need no art file.</summary>
        private Sprite SoftSprite(string name, int size, Func<float, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var alpha = Mathf.Clamp01(alphaAt(Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            _fxOwned.Add(texture);
            _fxOwned.Add(sprite);
            return sprite;
        }

        // ---------- Starting an effect ----------

        private Flash Begin(Flash flash)
        {
            _flashes.Add(flash);
            if (flash.Delay > 0f) flash.Item.gameObject.SetActive(false);
            else Place(flash);
            return flash;
        }

        /// <summary>The old round orb: still the stand-in whenever an effect picture is missing.</summary>
        private Flash AddFlash(Vector3 from, Vector3 to, float duration, float startSize, float endSize, Color color, float delay = 0f)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * startSize;
            go.AddComponent<MeshFilter>().sharedMesh = _orb;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _orbMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            TintOrb(go, color);
            return Begin(new Flash { Item = go.transform, From = from, To = to, Duration = duration, Delay = delay, StartSize = startSize, EndSize = endSize });
        }

        /// <summary>
        /// A painted burst that grows in place and fades. Sizes are the picture's full width in world units.
        /// Returns null when too many effects are already on screen.
        /// </summary>
        private Flash Burst(string picture, Vector3 at, float duration, float startSize, float endSize, Color tint,
            float spin = 0f, float delay = 0f, float rise = 0f, float angle = 0f, float fadeFrom = 0.55f)
        {
            if (_flashes.Count >= MaxFlashes) return null;
            var sprite = Fx(picture);
            if (sprite == null) return AddFlash(at, at + Vector3.up * rise, duration, startSize * 0.4f, endSize * 0.4f, tint, delay);
            var renderer = TakeSprite(sprite, false);
            return Begin(new Flash
            {
                Item = renderer.transform,
                Sprite = renderer,
                From = at,
                To = at + Vector3.up * rise,
                Duration = duration,
                Delay = delay,
                StartSize = startSize,
                EndSize = endSize,
                Spin = spin,
                Angle = angle,
                Ease = true,
                Tint = tint,
                FadeFrom = fadeFrom,
                Unit = 1f / Mathf.Max(sprite.bounds.size.x, 0.001f)
            });
        }

        /// <summary>
        /// A shot that flies to a point, or follows a moving target, and calls back where it lands. Returns the time
        /// until it lands. With too many effects on screen it lands at once, so nothing that depends on it is lost.
        /// </summary>
        private float Shot(string picture, Vector3 from, Vector3 to, float speed, float size, float arc, Color tint,
            Transform target, Vector3 targetOffset, Action<Vector3> landed, float delay = 0f)
        {
            if (_flashes.Count >= MaxFlashes)
            {
                landed?.Invoke(to);
                return 0f;
            }
            var flight = Mathf.Clamp(Vector3.Distance(from, to) / speed, 0.08f, 0.6f) / (IsLive ? 1f : Mathf.Max(1f, PlaybackSpeed));
            var sprite = Fx(picture);
            Flash flash;
            if (sprite == null) flash = AddFlash(from, to, flight, size * 0.25f, size * 0.25f, tint, delay);
            else
            {
                var renderer = TakeSprite(sprite, false);
                flash = Begin(new Flash
                {
                    Item = renderer.transform,
                    Sprite = renderer,
                    From = from,
                    To = to,
                    Duration = flight,
                    Delay = delay,
                    StartSize = size,
                    EndSize = size,
                    Aim = true,
                    Tint = tint,
                    Unit = 1f / Mathf.Max(sprite.bounds.size.x, 0.001f)
                });
            }
            flash.Arc = arc;
            flash.Target = target;
            flash.TargetOffset = targetOffset;
            flash.Landed = landed;
            return flight + delay;
        }

        /// <summary>A soft ring that spreads over the ground. On top, it also shows through buildings.</summary>
        private Flash GroundRing(Vector3 at, float duration, float startDiameter, float endDiameter, Color tint,
            float delay = 0f, bool onTop = false, float fadeFrom = 0.4f)
        {
            if (_flashes.Count >= MaxFlashes) return null;
            if (_ringSprite == null)
                _ringSprite = SoftSprite("FxRing", 128, r => r > 1f ? 0f : Mathf.Max(
                    Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.07f, 2f)),
                    r < 0.86f ? 0.18f * (r / 0.86f) * (r / 0.86f) : 0f));
            var renderer = TakeSprite(_ringSprite, !onTop);
            var ground = new Vector3(at.x, 0.04f, at.z);
            return Begin(new Flash
            {
                Item = renderer.transform,
                Sprite = renderer,
                From = ground,
                To = ground,
                Duration = duration,
                Delay = delay,
                StartSize = startDiameter,
                EndSize = endDiameter,
                Flat = true,
                Ease = true,
                Tint = tint,
                FadeFrom = fadeFrom,
                Unit = 1f / 0.86f
            });
        }

        /// <summary>The painted target ring. It is drawn as seen from this camera, so it faces the view.</summary>
        private Flash TargetRing(Vector3 at, float diameter, float duration)
        {
            // The ring fills 413 of the picture's 512 pixels.
            var size = diameter * 512f / 413f;
            return Burst("fx_target_ring", new Vector3(at.x, 0.05f, at.z), duration, size * 0.85f, size, Color.white, fadeFrom: 0.75f);
        }

        /// <summary>Embers or stone chips thrown out from a point; they fall, land and fade.</summary>
        private void Scatter(Vector3 at, int count, Color tint, float size, float speed, float life)
        {
            if (_dotSprite == null) _dotSprite = SoftSprite("FxDot", 32, r => (1f - r) * 2.2f);
            for (var i = 0; i < count && _shards.Count < MaxShards; i++)
            {
                var angle = (float)_fxRandom.NextDouble() * Mathf.PI * 2f;
                var flat = speed * (0.35f + (float)_fxRandom.NextDouble() * 0.65f);
                _shards.Add(new Shard
                {
                    Renderer = TakeSprite(_dotSprite, false),
                    Position = at,
                    Velocity = new Vector3(Mathf.Cos(angle) * flat, speed * (0.9f + (float)_fxRandom.NextDouble() * 0.9f), Mathf.Sin(angle) * flat),
                    Life = life * (0.7f + (float)_fxRandom.NextDouble() * 0.6f),
                    Size = size * (0.6f + (float)_fxRandom.NextDouble() * 0.8f),
                    Tint = tint
                });
            }
        }

        /// <summary>A copy of a building's picture that sinks and fades, so it is seen to fall instead of vanishing.</summary>
        private void Topple(IsoStand stand)
        {
            if (stand == null || stand.Renderer == null || stand.Renderer.sprite == null) return;
            var source = stand.Renderer;
            var go = new GameObject("Falling");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = source.sprite;
            copy.sharedMaterial = source.sharedMaterial;
            copy.color = source.color;
            copy.flipX = source.flipX;
            copy.sortingOrder = source.sortingOrder;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            _falling.Add(new Falling { Renderer = copy, Scale = go.transform.localScale, Color = source.color });
        }

        private void Later(float delay, Action action)
        {
            if (delay <= 0f) action();
            else _later.Add(new KeyValuePair<float, Action>(_fxClock + delay, action));
        }

        private void Shake(float amount)
        {
            if (!_rigLooked)
            {
                _rigLooked = true;
                if (_camera != null) _rig = _camera.GetComponent<IsoCamera>();
            }
            if (_rig != null) _rig.AddTrauma(amount);
        }

        /// <summary>The view leans in for a moment: kept for the biggest moments of a battle.</summary>
        private void ZoomPunch(float share)
        {
            Shake(0f);
            if (_rig != null) _rig.ZoomPunch(share);
        }

        private float Jitter(float range) => ((float)_fxRandom.NextDouble() * 2f - 1f) * range;

        /// <summary>How long a number of battle ticks lasts on screen at the speed the battle is shown.</summary>
        private float Seconds(float ticks) => ticks / (BattleSim.TicksPerSecond * (IsLive ? 1f : Mathf.Max(0.1f, PlaybackSpeed)));

        // ---------- Every frame ----------

        private void UpdateFlashes()
        {
            var dt = Time.deltaTime;
            _fxClock += dt;
            for (var i = _later.Count - 1; i >= 0; i--)
            {
                if (_later[i].Key > _fxClock) continue;
                var action = _later[i].Value;
                _later.RemoveAt(i);
                action();
            }

            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                if (flash.Item == null)
                {
                    _flashes.RemoveAt(i);
                    continue;
                }
                if (flash.Delay > 0f)
                {
                    flash.Delay -= dt;
                    if (flash.Delay > 0f) continue;
                    flash.Item.gameObject.SetActive(true);
                }
                else flash.Age += dt;
                if (flash.Target != null && flash.Target.gameObject.activeInHierarchy)
                {
                    flash.To = flash.Target.position + flash.TargetOffset;
                    if (flash.Stick) flash.From = flash.To;
                }
                if (flash.Age >= flash.Duration)
                {
                    _flashes.RemoveAt(i);
                    Release(flash);
                    flash.Landed?.Invoke(flash.To);
                    continue;
                }
                Place(flash);
            }

            UpdateShards(dt);
            UpdateFalling(dt);

            for (var i = _expiring.Count - 1; i >= 0; i--)
            {
                if (Time.time < _expiring[i].Key) continue;
                var item = _expiring[i].Value;
                _expiring.RemoveAt(i);
                if (item == null) continue;
                var system = item.GetComponent<ParticleSystem>();
                if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(item, 2f);
            }
        }

        private void Place(Flash flash)
        {
            var t = Mathf.Clamp01(flash.Age / Mathf.Max(flash.Duration, 0.0001f));
            var k = flash.Ease ? 1f - (1f - t) * (1f - t) : t;
            var at = Vector3.Lerp(flash.From, flash.To, k);
            at.y += flash.Arc * 4f * t * (1f - t);
            var size = Mathf.Lerp(flash.StartSize, flash.EndSize, k);
            flash.Item.position = at;
            if (flash.Sprite == null)
            {
                flash.Item.localScale = Vector3.one * size;
                return;
            }
            flash.Item.localScale = new Vector3(size * flash.Unit, size * flash.Unit, 1f);
            var cam = _camera != null ? _camera.transform : null;
            if (flash.Flat) flash.Item.rotation = Quaternion.Euler(90f, cam != null ? cam.eulerAngles.y : 0f, 0f);
            else
            {
                var angle = flash.Angle + flash.Spin * flash.Age;
                if (flash.Aim && cam != null)
                {
                    // Point along the flight as it looks on screen, following the arc up and back down.
                    var d = flash.To - flash.From + Vector3.up * (flash.Arc * 4f * (1f - 2f * t));
                    angle += Mathf.Atan2(Vector3.Dot(d, cam.up), Vector3.Dot(d, cam.right)) * Mathf.Rad2Deg;
                }
                flash.Item.rotation = (cam != null ? cam.rotation : Quaternion.identity) * Quaternion.Euler(0f, 0f, angle);
            }
            var color = flash.Tint;
            if (flash.FadeFrom < 1f) color.a *= 1f - Mathf.Clamp01((t - flash.FadeFrom) / (1f - flash.FadeFrom));
            flash.Sprite.color = color;
        }

        private void UpdateShards(float dt)
        {
            var facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            for (var i = _shards.Count - 1; i >= 0; i--)
            {
                var shard = _shards[i];
                shard.Age += dt;
                if (shard.Renderer == null || shard.Age >= shard.Life)
                {
                    ReleaseSprite(shard.Renderer);
                    _shards.RemoveAt(i);
                    continue;
                }
                shard.Velocity.y -= 14f * dt;
                shard.Position += shard.Velocity * dt;
                if (shard.Position.y < 0.06f)
                {
                    shard.Position.y = 0.06f;
                    shard.Velocity = Vector3.zero;
                }
                var color = shard.Tint;
                color.a *= 1f - Mathf.Clamp01((shard.Age / shard.Life - 0.6f) / 0.4f);
                shard.Renderer.color = color;
                var item = shard.Renderer.transform;
                item.SetPositionAndRotation(shard.Position, facing);
                item.localScale = Vector3.one * shard.Size;
            }
        }

        private void UpdateFalling(float dt)
        {
            for (var i = _falling.Count - 1; i >= 0; i--)
            {
                var falling = _falling[i];
                falling.Age += dt;
                var k = Mathf.Clamp01(falling.Age / 0.32f);
                if (falling.Renderer == null || k >= 1f)
                {
                    if (falling.Renderer != null) Destroy(falling.Renderer.gameObject);
                    _falling.RemoveAt(i);
                    continue;
                }
                falling.Renderer.transform.localScale = new Vector3(falling.Scale.x * (1f + 0.08f * k), falling.Scale.y * (1f - 0.6f * k), falling.Scale.z);
                var color = falling.Color;
                color.a *= 1f - k * k;
                falling.Renderer.color = color;
            }
        }

        private void DestroyFx()
        {
            foreach (var item in _fxOwned)
                if (item != null) Destroy(item);
            if (_fxMaterial != null) Destroy(_fxMaterial);
        }
    }
}
