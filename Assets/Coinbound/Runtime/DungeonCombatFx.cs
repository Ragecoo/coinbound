using UnityEngine;

namespace Coinbound
{
    /// <summary>Code-native combat shapes on the 32-pixel world grid, with no rotated or filtered pixel quads.</summary>
    public sealed class DungeonCombatFx : MonoBehaviour
    {
        private enum Kind { Slash, Impact, Guard, Windup, Stun, Parry, Hit, RollDust, WallHit, FootDust, WetSplash }
        private const int SightSamples = 96;
        private const float TwoPi = Mathf.PI * 2;
        private static readonly RaycastHit2D[] SightHits = new RaycastHit2D[48];
        private static readonly ContactFilter2D SolidFilter = new ContactFilter2D
        {
            useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers
        };
        private static readonly Color32 Violet = new Color32(126, 91, 168, 255);
        private static readonly Color32 Steel = new Color32(184, 199, 227, 255);
        private static readonly Color32 Gold = new Color32(235, 216, 159, 255);
        private Kind kind;
        private DungeonPlayer owner;
        private Transform anchor;
        private DungeonEnemy enemy;
        private Vector2 origin;
        private Vector2 visualOffset;
        private Vector2 direction;
        private Color32 tint;
        private float duration;
        private float age;
        private float handHeight;
        private bool visible = true;
        private uint random;
        private DungeonCombatMesh air;
        private DungeonCombatMesh ground;
        private DungeonCombatMesh sparkMesh;
        private Particle[] sparks;
        private Particle[] droplets;
        private Particle[] chips;
        private readonly float[] sightDistances = new float[SightSamples];
        private Vector2 lastSightOrigin;
        private bool hasSight;

        private struct Particle
        {
            internal Vector2 velocity;
            internal float verticalSpeed;
            internal float life;
            internal float landingTime;
            internal bool clearLanding;
            internal int size;
            internal float shade;
        }

        public string EffectKind => kind.ToString();
        public bool ProducesBlood => kind == Kind.Hit && droplets != null;
        public int ActivePixelCount => (air != null ? air.VisiblePixelCount : 0) + (ground != null ? ground.VisiblePixelCount : 0)
            + (sparkMesh != null ? sparkMesh.VisiblePixelCount : 0);

        private static DungeonCombatFx Create(DungeonPlayer owner, Kind kind, float duration, Color32 tint)
        {
            var obj = new GameObject("Combat " + kind);
            obj.transform.SetParent(owner.transform.parent, false);
            var fx = obj.AddComponent<DungeonCombatFx>();
            fx.owner = owner;
            fx.kind = kind;
            fx.duration = duration;
            fx.tint = tint;
            fx.handHeight = DungeonHeroAnimator.HandHeight;
            fx.random = unchecked((uint)obj.GetEntityId().GetHashCode() ^ 0x85EBCA6Bu);
            fx.air = DungeonCombatMesh.Create(obj.transform, "Air pixels", owner.EffectMaterial);
            return fx;
        }

        internal static void Slash(DungeonPlayer owner, Vector2 direction)
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            if (!settings.swordTrail) return;
            var fx = Create(owner, Kind.Slash, 0.24f, settings.slashColor);
            fx.anchor = owner.transform;
            fx.direction = Normal(direction);
            Vector2 feet = owner.transform.position;
            Vector2 blade = feet + fx.direction * 0.65f;
            bool clearBlade = DungeonCombatPhysics.FreeFeet(blade, 0.03f) && DungeonCombatPhysics.ClearLine(feet, blade);
            DungeonEffectLights.Flash(clearBlade ? blade : feet,
                settings.slashLightRadius, settings.slashLightIntensity, settings.slashLightColor, settings.slashLightDuration);
            fx.Draw();
        }

        // Stone/metal impacts are intentionally bloodless.
        internal static void Impact(DungeonPlayer owner, Vector2 position, Color color, float duration = 0.24f)
        {
            if (!DungeonFxSettings.Current.sparks) return;
            // Keep the caller API, while all metal/stone sparks share the inspectable palette.
            var fx = Create(owner, Kind.Impact, duration, DungeonFxSettings.Current.sparkColor);
            fx.origin = position;
            fx.handHeight = 0.48f;
            fx.direction = Vector2.up;
            fx.MakeSparks(11, true);
            SparkLight(position);
            fx.Draw();
        }

        internal static void Parry(DungeonPlayer owner, Vector2 feet, Vector2 normal)
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            if (!settings.sparks && !settings.blockPulse) return;
            var fx = Create(owner, Kind.Parry, 0.4f, settings.sparkColor);
            fx.origin = feet;
            fx.direction = Normal(normal);
            if (settings.sparks) fx.MakeSparks(16, false);
            Vector2 contact = feet + fx.direction * 0.54f;
            SparkLight(DungeonCombatPhysics.FreeFeet(contact, 0.03f) ? contact : feet);
            fx.Draw();
        }

        internal static void Hit(DungeonPlayer owner, Vector2 feet, Vector2 direction, bool playerHit)
        {
            if (!DungeonFxSettings.Current.blood && !DungeonFxSettings.Current.sparks && !playerHit) return;
            var fx = Create(owner, Kind.Hit, 1.1f, Steel);
            fx.origin = feet;
            fx.direction = Normal(direction);
            fx.handHeight = playerHit ? DungeonHeroAnimator.HandHeight * 0.8f : 0.36f;
            if (playerHit)
            {
                fx.chips = new Particle[10];
                Vector2 side = new Vector2(-fx.direction.y, fx.direction.x);
                for (int i = 0; i < fx.chips.Length; i++)
                    fx.chips[i] = new Particle {
                        velocity = fx.direction * Mathf.Lerp(.4f, 1.8f, fx.Next()) + side * (fx.Next() - .5f) * 2,
                        verticalSpeed = .4f + fx.Next(), life = .25f + fx.Next() * .25f,
                        size = i % 3 == 0 ? 2 : 1, shade = fx.Next()
                    };
            }
            if (DungeonFxSettings.Current.sparks)
            {
                fx.sparkMesh = DungeonCombatMesh.Create(fx.transform, "Metal spark pixels", owner.EffectMaterial);
                fx.MakeSparks(7, true);
                SparkLight(feet);
            }
            if (DungeonFxSettings.Current.blood)
            {
                fx.ground = DungeonCombatMesh.Create(fx.transform, "Brief floor traces", owner.EffectMaterial);
                fx.MakeBlood();
            }
            fx.Draw();
        }

        internal static void RollDust(DungeonPlayer owner, Vector2 feet, Vector2 direction, bool trail = false)
        {
            if (!DungeonFxSettings.Current.rollDust) return;
            var fx = Create(owner, Kind.RollDust, trail ? .30f : .45f, new Color32(255, 255, 255, 255));
            fx.origin = feet;
            fx.direction = -Normal(direction);
            fx.handHeight = 0;
            fx.MakeSparks(trail ? 5 : 14, false);
            fx.Draw();
        }

        internal static void WallHit(DungeonPlayer owner, Vector2 wallPoint, Vector2 normal, Vector2? visualTip = null)
        {
            if (!DungeonFxSettings.Current.sparks && !DungeonFxSettings.Current.stoneChips) return;
            var fx = Create(owner, Kind.WallHit, 0.75f, Steel);
            fx.direction = Normal(normal);
            // Move the floor sample outside the collider; particles still start at the visible stone face.
            fx.origin = wallPoint + fx.direction * 0.12f;
            // The projected wall face differs from its floor collision boundary. Keep light and
            // obstacle queries outside the wall while the sparks start at the actual visible tip.
            if (visualTip.HasValue)
                fx.visualOffset = visualTip.Value - (fx.origin + Vector2.up * fx.handHeight);
            if (DungeonFxSettings.Current.sparks)
            {
                fx.sparkMesh = DungeonCombatMesh.Create(fx.transform, "Stone spark pixels", owner.EffectMaterial);
                fx.MakeSparks(6, false);
                SparkLight(fx.origin);
            }
            if (DungeonFxSettings.Current.stoneChips)
            {
                fx.chips = new Particle[5];
                Vector2 side = new Vector2(-fx.direction.y, fx.direction.x);
                for (int i = 0; i < fx.chips.Length; i++)
                {
                    float up = Mathf.Lerp(0.2f, 0.9f, fx.Next());
                    fx.chips[i] = new Particle
                    {
                        velocity = fx.direction * Mathf.Lerp(0.35f, 1.3f, fx.Next()) + side * (fx.Next() - 0.5f) * 1.3f,
                        verticalSpeed = up,
                        landingTime = (up + Mathf.Sqrt(up * up + 18 * fx.handHeight)) / 9,
                        size = i % 3 == 0 ? 2 : 1,
                        shade = fx.Next()
                    };
                }
            }
            fx.Draw();
        }

        internal static void Footstep(DungeonPlayer owner, Vector2 feet, Vector2 movement, bool wet)
        {
            if (wet ? !DungeonFxSettings.Current.wetSplashes : !DungeonFxSettings.Current.footDust) return;
            if (!DungeonCombatPhysics.FreeFeet(feet, 0.07f)) return;
            var fx = Create(owner, wet ? Kind.WetSplash : Kind.FootDust, wet ? 0.38f : 0.28f,
                wet ? new Color32(134, 153, 187, 180) : new Color32(255, 255, 255, 255));
            fx.origin = feet;
            fx.direction = -Normal(movement);
            fx.handHeight = 0;
            fx.MakeSparks(wet ? 5 : 3, true);
            fx.Draw();
        }

        private static void SparkLight(Vector2 feet)
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            DungeonEffectLights.Flash(feet, settings.sparkLightRadius, settings.sparkLightIntensity,
                settings.sparkLightColor, settings.sparkLightDuration);
        }

        internal static DungeonCombatFx Guard(DungeonPlayer owner)
        {
            var fx = Create(owner, Kind.Guard, -1, new Color32(161, 177, 218, 180));
            fx.anchor = owner.transform;
            fx.Draw();
            return fx;
        }

        internal static DungeonCombatFx Windup(DungeonPlayer owner, DungeonEnemy enemy, Vector2 direction, float duration)
        {
            var fx = Create(owner, Kind.Windup, duration, new Color32(226, 111, 127, 225));
            fx.anchor = enemy.transform;
            fx.enemy = enemy;
            fx.direction = Normal(direction);
            fx.handHeight = 0.04f;
            fx.Draw();
            return fx;
        }

        internal static DungeonCombatFx Stun(DungeonPlayer owner, DungeonEnemy enemy)
        {
            var fx = Create(owner, Kind.Stun, -1, Gold);
            fx.anchor = enemy.transform;
            fx.enemy = enemy;
            fx.handHeight = 0.72f;
            fx.Draw();
            return fx;
        }

        internal void SetVisible(bool value)
        {
            visible = value;
            if (air != null) air.SetVisible(value);
            if (ground != null) ground.SetVisible(value);
            if (sparkMesh != null) sparkMesh.SetVisible(value);
        }

        private void Update()
        {
            bool followsAnchor = kind == Kind.Slash || kind == Kind.Guard || kind == Kind.Windup || kind == Kind.Stun;
            if (owner == null || (followsAnchor && anchor == null)) { Destroy(gameObject); return; }
            if (!owner.CanAct) return;
            if (enemy != null ? enemy.IsHitStopped : owner.IsHitStopped) return;
            age += Time.deltaTime;
            if (duration > 0 && age >= duration) { Destroy(gameObject); return; }
            if (kind == Kind.Guard) visible = owner.IsBlocking;
            if (kind == Kind.Stun) visible = enemy != null && !enemy.IsDead && enemy.StunRemaining > 0.15f;
            if (!visible) { SetVisible(false); return; }
            Draw();
        }

        private void Draw()
        {
            Vector2 feet = anchor != null ? (Vector2)anchor.position : origin;
            Vector2 center = feet + Vector2.up * handHeight + visualOffset;
            if (kind == Kind.Guard) direction = owner.AimDirection;
            // The sword's graphic remains whole beside walls; physical hit tests are handled by the player.
            if (kind != Kind.Slash) UpdateSight(feet);
            bool onGround = kind == Kind.RollDust || kind == Kind.FootDust || kind == Kind.WetSplash;
            DungeonFxSettings settings = DungeonFxSettings.Current;
            float emission = kind == Kind.Slash ? settings.slashEmission : kind == Kind.Impact || kind == Kind.Parry ? settings.sparkEmission : 0;
            air.Begin(center, feet, onGround, emission, kind == Kind.Slash, kind == Kind.RollDust || kind == Kind.FootDust);
            if (ground != null) ground.Begin(feet, feet, true);
            if (sparkMesh != null) sparkMesh.Begin(center, feet, false, settings.sparkEmission);
            float progress = duration > 0 ? Mathf.Clamp01(age / duration) : age;
            switch (kind)
            {
                case Kind.Slash: DrawSlash(feet, center, progress); break;
                case Kind.Guard: DrawGuard(center); break;
                case Kind.Windup: DrawWindup(center, progress); break;
                case Kind.Stun: DrawStun(center); break;
                case Kind.Parry:
                    if (settings.blockPulse) DrawParry(center, progress);
                    DrawSparks(feet, center);
                    break;
                case Kind.Hit:
                    DrawSparks(feet, center);
                    DrawBlood(feet);
                    DrawHitFragments(feet, center);
                    break;
                case Kind.RollDust: DrawDust(feet, progress); break;
                case Kind.FootDust: DrawDust(feet, progress); break;
                case Kind.WetSplash: DrawSplash(feet, progress); break;
                case Kind.WallHit:
                    DrawSparks(feet, center);
                    DrawStone(feet, center, progress);
                    break;
                default: DrawSparks(feet, center); break;
            }
            air.End();
            if (ground != null) ground.End();
            if (sparkMesh != null) sparkMesh.End();
        }

        private void DrawSlash(Vector2 feet, Vector2 center, float t)
        {
            Color slashColor = DungeonFxSettings.Current.slashColor;
            slashColor.a *= .5f;
            Color32 core = slashColor;
            Color32 outerEdge = Shade(slashColor, 0.85f);
            Color32 innerEdge = Shade(slashColor, 0.58f);
            Color32 tail = Shade(slashColor, 0.72f);
            float sweep = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.72f));
            float head = Mathf.Lerp(-0.65f, 1.7f, sweep);
            float trail = Mathf.Lerp(0.75f, 2.85f, Mathf.Clamp01(t / 0.32f));
            float fade = t < 0.64f ? 1 : Mathf.Pow(1 - (t - 0.64f) / 0.36f, 1.1f);
            Vector2 side = new Vector2(-direction.y, direction.x);
            for (int y = 12; y < DungeonCombatMesh.Resolution - 12; y++)
            for (int x = 12; x < DungeonCombatMesh.Resolution - 12; x++)
            {
                Vector2 delta = air.WorldCenter(x, y) - center;
                float radius = delta.magnitude;
                if (radius < 0.65f || radius > 1.55f) continue;
                float angle = Mathf.Atan2(Vector2.Dot(delta, side), Vector2.Dot(delta, direction));
                float behindHead = Mathf.Repeat(head - angle, TwoPi);
                Color32 color;
                if (behindHead <= trail)
                {
                    float along = 1 - behindHead / trail;
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(along * Mathf.PI) * Mathf.Sqrt(along)), 0.65f);
                    float outer = 1.26f + 0.12f * Mathf.Sin(along * Mathf.PI) + t * 0.07f;
                    float thickness = (0.025f + 0.49f * taper) * Mathf.Lerp(0.7f, 1, Mathf.Clamp01(t * 5));
                    float inner = outer - thickness;
                    if (radius < inner || radius > outer) continue;
                    float edge = outer - radius;
                    color = edge < 0.055f ? outerEdge : radius - inner < 0.045f ? innerEdge : core;
                    color = Alpha(color, fade * Mathf.Lerp(0.5f, 1, Mathf.Clamp01(along * 4)));
                }
                else
                {
                    // A detached, broken outer filament makes the heavy crescent read as fast motion.
                    float gap = behindHead - trail;
                    if (t < 0.18f || gap < 0.10f || gap > 0.72f) continue;
                    float tailRadius = 1.32f + t * 0.07f;
                    if (Mathf.Abs(radius - tailRadius) > 0.018f || (x + y * 3) % 17 < 4) continue;
                    color = Alpha(tail, fade * 0.52f * (1 - gap / 0.8f));
                }
                air.Paint(x, y, color);
            }
            if (t < 0.28f)
            {
                Vector2 edge = direction * 1.23f + side * -0.10f;
                air.Glint(center + edge, Alpha(core, 1 - t / 0.28f), 2);
            }
        }

        private void DrawGuard(Vector2 center)
        {
            float pulse = 0.86f + 0.14f * Mathf.Sin(age * 4);
            DrawArc(center, direction, 0.63f, 0.0625f, 0.92f, Alpha(tint, pulse), true);
            DrawArc(center, direction, 0.54f, 0.03125f, 0.48f, Alpha(Violet, 0.48f * pulse), false);
            Vector2 side = new Vector2(-direction.y, direction.x);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 point = direction * 0.46f + side * (0.44f * sign);
                if (!CanSee(point)) continue;
                air.Glint(center + point, Alpha(Steel, 0.7f), 1);
            }
        }

        private void DrawParry(Vector2 center, float t)
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            float radius = Mathf.Lerp(0.37f, 1.12f, 1 - (1 - t) * (1 - t));
            float fade = Mathf.Clamp01(1 - t / 0.85f);
            DrawArc(center, direction, radius, Mathf.Lerp(0.17f, 0.035f, t), 1.08f, Alpha(settings.sparkHotColor, fade), true);
            DrawArc(center, direction, radius - 0.13f, 0.035f, 0.8f, Alpha(settings.sparkColor, fade * 0.75f), false);
            if (age < 0.13f)
            {
                Vector2 impact = direction * 0.54f;
                if (CanSee(impact))
                    air.Glint(center + impact, Alpha(settings.sparkHotColor, 1 - age / 0.13f),
                        Mathf.Max(1, 5 - Mathf.FloorToInt(age * 28)));
            }
        }

        private void DrawWindup(Vector2 center, float t)
        {
            float radius = Mathf.Lerp(1.05f, 0.76f, t);
            DrawArc(center, direction, radius, 0.03125f, 0.68f, Alpha(tint, 0.5f + t * 0.5f), false);
            if (t > 0.6f && CanSee(direction * radius)) air.Glint(center + direction * radius, tint, 1);
        }

        private void DrawStun(Vector2 center)
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = age * 6 + i * TwoPi / 3;
                Vector2 point = center + new Vector2(Mathf.Cos(angle) * 0.27f, Mathf.Sin(angle) * 0.10f);
                air.Glint(point, Gold, i == 0 ? 1 : 0);
            }
        }

        private void DrawArc(Vector2 center, Vector2 forward, float radius, float thickness, float halfAngle, Color32 color, bool tapered)
        {
            Vector2 side = new Vector2(-forward.y, forward.x);
            int extent = Mathf.CeilToInt((radius + 0.1f) * 32) + 2;
            int middleX = Mathf.FloorToInt((center.x - air.LowerLeft.x) * 32);
            int middleY = Mathf.FloorToInt((center.y - air.LowerLeft.y) * 32);
            for (int y = middleY - extent; y <= middleY + extent; y++)
            for (int x = middleX - extent; x <= middleX + extent; x++)
            {
                Vector2 delta = air.WorldCenter(x, y) - center;
                float length = delta.magnitude;
                if (length > radius || length < radius - thickness || !CanSee(delta)) continue;
                float angle = Mathf.Atan2(Vector2.Dot(delta, side), Vector2.Dot(delta, forward));
                if (Mathf.Abs(angle) > halfAngle) continue;
                float localThickness = tapered ? thickness * (0.2f + 0.8f * Mathf.Cos(angle / halfAngle * Mathf.PI * 0.5f)) : thickness;
                if (length < radius - localThickness) continue;
                air.Paint(x, y, color);
            }
        }

        private void MakeSparks(int count, bool radial)
        {
            sparks = new Particle[count];
            float aim = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < count; i++)
            {
                float angle = radial ? Next() * TwoPi : aim + (Next() - 0.5f) * 2.6f;
                sparks[i] = new Particle
                {
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Lerp(1.5f, 4.3f, Next()),
                    verticalSpeed = Mathf.Lerp(0.15f, 1.3f, Next()),
                    life = Mathf.Lerp(0.16f, 0.34f, Next()),
                    size = i % 5 == 0 ? 2 : 1,
                    shade = Next()
                };
            }
        }

        private void DrawSparks(Vector2 feet, Vector2 center)
        {
            if (sparks == null) return;
            DungeonFxSettings settings = DungeonFxSettings.Current;
            DungeonCombatMesh canvas = sparkMesh != null ? sparkMesh : air;
            Vector2 kick = kind == Kind.Parry ? direction * 0.54f : Vector2.zero;
            for (int i = 0; i < sparks.Length; i++)
            {
                Particle spark = sparks[i];
                if (age >= spark.life) continue;
                float flight = (1 - Mathf.Exp(-3 * age)) / 3;
                Vector2 offset = kick + spark.velocity * flight;
                if (!CanSee(offset)) continue;
                float lift = spark.verticalSpeed * age - 3.8f * age * age;
                Vector2 point = center + offset + Vector2.up * lift;
                Color32 shade = Color.Lerp(settings.sparkColor, settings.sparkHotColor, spark.shade * 0.7f);
                Color32 color = Alpha(shade, Mathf.Clamp01((spark.life - age) / 0.10f));
                Vector2 previous = point - spark.velocity.normalized * Mathf.Lerp(0.12f, 0.025f, age / spark.life);
                canvas.Line(previous, point, color, spark.size);
                canvas.Pixel(point, Alpha(settings.sparkHotColor, color.a / 255f), spark.size, spark.size);
            }
            if (age < 0.065f && kind != Kind.Parry)
                canvas.Glint(center, Alpha(settings.sparkHotColor, 1 - age / 0.065f), 3);
        }

        private void MakeBlood()
        {
            droplets = new Particle[7];
            float angle = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < droplets.Length; i++)
            {
                float heading = angle + (Next() - 0.5f) * 2.5f;
                float height = Mathf.Lerp(0.3f, 0.9f, Next());
                droplets[i] = new Particle
                {
                    velocity = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * Mathf.Lerp(0.6f, 1.9f, Next()),
                    verticalSpeed = height,
                    landingTime = (height + Mathf.Sqrt(height * height + 18 * handHeight)) / 9,
                    size = i % 3 == 0 ? 2 : 1,
                    shade = Next()
                };
                // Reserve the whole 3-by-2-pixel trace, including its quantized edge,
                // so blood cannot be painted over a wall or a solid object's base.
                Vector2 landing = origin + droplets[i].velocity * droplets[i].landingTime;
                droplets[i].clearLanding = DungeonCombatPhysics.FreeFeet(landing, 0.09f);
            }
        }

        private void DrawBlood(Vector2 feet)
        {
            if (droplets == null) return;
            for (int i = 0; i < droplets.Length; i++)
            {
                Particle drop = droplets[i];
                float flight = Mathf.Min(age, drop.landingTime);
                Vector2 groundOffset = drop.velocity * flight;
                if (!CanSee(groundOffset)) continue;
                Vector2 floor = feet + groundOffset;
                if (age < drop.landingTime)
                {
                    float height = handHeight + drop.verticalSpeed * age - 4.5f * age * age;
                    Color32 color = drop.shade > 0.6f ? new Color32(145, 45, 69, 255) : new Color32(105, 28, 51, 255);
                    air.Pixel(floor + Vector2.up * height, color, drop.size, drop.size);
                }
                else
                {
                    if (!drop.clearLanding) continue;
                    float stainAge = age - drop.landingTime;
                    float opacity = 0.62f * Mathf.Clamp01(1 - stainAge / 0.72f);
                    Color32 stain = Alpha(new Color32(83, 27, 48, 255), opacity);
                    ground.Pixel(floor, stain, drop.size + 1, 1);
                    if (drop.size > 1) ground.Pixel(floor + Vector2.up / 32, Alpha(stain, 0.65f), 1, 1);
                }
            }
        }

        private void DrawDust(Vector2 feet, float t)
        {
            if (sparks == null) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                Particle puff = sparks[i];
                float sizeScale = kind == Kind.FootDust ? 0.48f : 1;
                Vector2 offset = puff.velocity * (0.025f + (1 - (1 - t) * (1 - t)) * 0.15f) * sizeScale;
                if (!CanSee(offset)) continue;
                Vector2 point = feet + offset + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 0.10f);
                int size = t < .25f ? 2 : t < .6f && kind == Kind.RollDust ? 3 : 1;
                float fade = Mathf.Ceil((1 - t) * 4) / 4;
                Color32 shade = tint;
                air.Pixel(point, Alpha(shade, fade), size, size);
                if (size == 3)
                    air.Pixel(point + Vector2.up / 32, Alpha(tint, fade), 1, 1);
            }
        }

        private void DrawHitFragments(Vector2 feet, Vector2 center)
        {
            if (chips == null) return;
            foreach (Particle chip in chips)
            {
                if (age >= chip.life) continue;
                Vector2 offset = chip.velocity * age;
                if (!CanSee(offset)) continue;
                Vector2 point = center + offset + Vector2.up * (chip.verticalSpeed * age - 4 * age * age);
                float fade = Mathf.Ceil((1 - age / chip.life) * 4) / 4;
                Color32 color = chip.shade > .6f ? new Color32(236, 123, 126, 255) : new Color32(173, 52, 74, 255);
                air.Pixel(point, Alpha(color, fade), chip.size, age < .16f ? chip.size + 1 : chip.size);
            }
        }

        private void DrawSplash(Vector2 feet, float t)
        {
            float radius = 0.07f + t * 0.20f;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * TwoPi / 40;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.48f) * radius;
                if (CanSee(offset)) air.Pixel(feet + offset, Alpha(tint, (1 - t) * 0.55f));
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                Particle drop = sparks[i];
                Vector2 offset = drop.velocity * (age * 0.17f);
                if (!CanSee(offset)) continue;
                float lift = Mathf.Max(0, Mathf.Sin(t * Mathf.PI)) * (0.05f + drop.shade * 0.08f);
                air.Pixel(feet + offset + Vector2.up * lift, Alpha(tint, 1 - t));
            }
        }

        private void DrawStone(Vector2 feet, Vector2 center, float t)
        {
            if (chips == null) return;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 scratch = center - direction * 0.07f;
            float fade = Mathf.Clamp01((1 - t) * 1.8f);
            // Two short stepped facets, rather than a persistent decal or damage to the wall art.
            air.Line(scratch - side * 0.06f, scratch + side * 0.055f + direction * 0.02f,
                Alpha(new Color32(113, 117, 146, 255), fade * 0.65f));
            air.Pixel(scratch + Vector2.up / 32, Alpha(new Color32(171, 178, 203, 255), fade * 0.65f));
            for (int i = 0; i < chips.Length; i++)
            {
                Particle chip = chips[i];
                if (age >= chip.landingTime) continue;
                Vector2 offset = chip.velocity * age;
                if (!CanSee(offset)) continue;
                float height = handHeight + chip.verticalSpeed * age - 4.5f * age * age;
                Vector2 point = feet + offset + Vector2.up * Mathf.Max(0, height) + visualOffset;
                Color32 color = chip.shade < 0.5f ? new Color32(115, 120, 147, 255) : new Color32(167, 169, 191, 255);
                air.Pixel(point, Alpha(color, fade), chip.size, chip.size);
            }
        }

        private void UpdateSight(Vector2 feet)
        {
            if (hasSight && (feet - lastSightOrigin).sqrMagnitude < DungeonCombatMesh.PixelSize * DungeonCombatMesh.PixelSize) return;
            hasSight = true;
            lastSightOrigin = feet;
            for (int i = 0; i < SightSamples; i++)
            {
                float angle = i * TwoPi / SightSamples;
                Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = 2.1f;
                int count = Physics2D.Raycast(feet, ray, SolidFilter, SightHits, distance);
                for (int hit = 0; hit < count; hit++)
                    if (DungeonCombatPhysics.IsObstacle(SightHits[hit].collider)) distance = Mathf.Min(distance, SightHits[hit].distance);
                sightDistances[i] = distance;
            }
        }

        private bool CanSee(Vector2 offset)
        {
            float distance = offset.magnitude;
            if (distance < 0.02f) return true;
            float angle = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x), TwoPi);
            int first = Mathf.FloorToInt(angle * SightSamples / TwoPi) % SightSamples;
            int second = (first + 1) % SightSamples;
            // A nearby wall in either adjacent ray wins. Include the pixel's corners,
            // rather than letting a bright edge leak through a narrow wall corner.
            float visibleDistance = Mathf.Min(sightDistances[first], sightDistances[second]);
            return distance + DungeonCombatMesh.PixelSize * 0.75f <= visibleDistance;
        }

        private float Next()
        {
            random = unchecked(random * 1664525u + 1013904223u);
            return (random >> 8) * (1f / 16777216);
        }

        private static Vector2 Normal(Vector2 vector) => vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector2.right;
        private static Color32 Shade(Color color, float brightness) => new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        private static Color32 Alpha(Color32 color, float factor)
        {
            color.a = (byte)Mathf.RoundToInt(color.a * Mathf.Clamp01(factor));
            return color;
        }
    }
}
