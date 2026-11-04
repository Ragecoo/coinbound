using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Coinbound.Tests
{
    /// <summary>Isolated real-physics encounters, independent of map generation and character artwork.</summary>
    public sealed class DungeonCombatTests
    {
        private static readonly Vector2 Origin = new Vector2(2048, 2048);
        private GameObject fixture;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode editorInputBehavior;
        private readonly List<Object> ownedAssets = new List<Object>();
        private bool previousBlood;
        private bool previousHitStop;
        private bool previousArrowImpacts;
        private float previousNorthInset;
        private float previousOtherInset;
        private float previousArrowLifetime;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1;
            DungeonFxSettings settings = DungeonFxSettings.Current;
            previousBlood = settings.blood;
            previousHitStop = settings.hitStop;
            previousArrowImpacts = settings.arrowImpacts;
            previousNorthInset = settings.arrowNorthWallInset;
            previousOtherInset = settings.arrowOtherWallInset;
            previousArrowLifetime = settings.arrowStuckLifetime;
            settings.blood = false;
            settings.hitStop = false;
            settings.arrowImpacts = true;
            settings.arrowNorthWallInset = 0.85f;
            settings.arrowOtherWallInset = 0.25f;
            settings.arrowStuckLifetime = 1.1f;
            fixture = new GameObject("Isolated combat fixture");
            // Synthetic neutral devices prevent held editor/game controls from starting extra actions.
            backgroundBehavior = InputSystem.settings.backgroundBehavior;
            editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null) Object.Destroy(fixture);
            foreach (Object asset in ownedAssets) if (asset != null) Object.Destroy(asset);
            ownedAssets.Clear();
            DungeonFxSettings settings = DungeonFxSettings.Current;
            settings.blood = previousBlood;
            settings.hitStop = previousHitStop;
            settings.arrowImpacts = previousArrowImpacts;
            settings.arrowNorthWallInset = previousNorthInset;
            settings.arrowOtherWallInset = previousOtherInset;
            settings.arrowStuckLifetime = previousArrowLifetime;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
            Time.timeScale = 1;
            yield return null; // Destroy also unregisters enemies and removes every child projectile/effect.
        }

        private DungeonPlayer Player(DungeonHero hero)
        {
            var obj = new GameObject("Combat player", typeof(Rigidbody2D), typeof(BoxCollider2D));
            obj.transform.SetParent(fixture.transform, false);
            obj.transform.position = Origin;
            var body = obj.GetComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var feet = obj.GetComponent<BoxCollider2D>();
            feet.size = new Vector2(0.36f, 0.36f);
            var player = obj.AddComponent<DungeonPlayer>();
            player.Initialize(hero);
            player.SetInputEnabled(true);
            Physics2D.SyncTransforms();
            Assert.That(player.CanAct, Is.True, "Combat fixture needs Game View focus, or Unity batchmode.");
            return player;
        }

        private DungeonEnemy Enemy(DungeonPlayer player, Vector2 offset, bool stationary = true)
        {
            var obj = new GameObject("Combat enemy");
            obj.transform.SetParent(fixture.transform, false);
            obj.transform.position = Origin + offset;
            var enemy = obj.AddComponent<DungeonEnemy>();
            enemy.Initialize(player, null);
            if (stationary) obj.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            Physics2D.SyncTransforms();
            return enemy;
        }

        private BoxCollider2D Wall(Vector2 offset, Vector2 size, float angle = 0)
        {
            var obj = new GameObject("Solid wall", typeof(BoxCollider2D));
            obj.transform.SetParent(fixture.transform, false);
            obj.transform.position = Origin + offset;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            var collider = obj.GetComponent<BoxCollider2D>();
            collider.size = size;
            Physics2D.SyncTransforms();
            return collider;
        }

        private DungeonChest Chest(Vector2 offset)
        {
            var obj = new GameObject("Interaction chest", typeof(SpriteRenderer));
            obj.transform.SetParent(fixture.transform, false);
            obj.transform.position = Origin + offset;
            var chest = obj.AddComponent<DungeonChest>();
            chest.Initialize("s");
            Physics2D.SyncTransforms();
            return chest;
        }

        [Test]
        public void ChestRequiresFacingDistanceAndUnobstructedLine()
        {
            DungeonPlayer player = Player(DungeonHero.First);
            DungeonChest chest = Chest(Vector2.right);
            player.SetAimDirection(Vector2.left);
            Assert.That(chest.TryOpen(player), Is.False, "A chest behind the player cannot open.");
            player.SetAimDirection(Vector2.up);
            Assert.That(chest.TryOpen(player), Is.False, "A chest to the side cannot open.");
            player.SetAimDirection(Vector2.right);
            Assert.That(chest.CanInteract(player), Is.True);
            chest.transform.position = Origin + Vector2.right * 3;
            Physics2D.SyncTransforms();
            Assert.That(chest.TryOpen(player), Is.False, "A remote chest cannot open.");
            chest.transform.position = Origin + Vector2.right;
            Physics2D.SyncTransforms();
            Wall(Vector2.right * .5f, new Vector2(.1f, 2));
            Assert.That(chest.TryOpen(player), Is.False, "Interaction cannot pass through a wall.");
            Assert.That(chest.State, Is.EqualTo(DungeonChest.ChestState.Closed));
        }

        [UnityTest]
        public IEnumerator ChestOpensOncePausesAndHoldsFinalFrame()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            DungeonChest chest = Chest(Vector2.right);
            player.SetAimDirection(Vector2.right);
            int opened = 0;
            chest.Opened += _ => opened++;
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(chest.TryOpen(player), Is.False);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(chest.FrameIndex, Is.Zero);
            Time.timeScale = 1;
            yield return new WaitForSeconds(.65f);
            Assert.That(chest.State, Is.EqualTo(DungeonChest.ChestState.Open));
            Assert.That(chest.FrameIndex, Is.EqualTo(8));
            Assert.That(opened, Is.EqualTo(1));
            yield return new WaitForSeconds(.15f);
            Assert.That(chest.FrameIndex, Is.EqualTo(8));
            Assert.That(opened, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SwordHitsAimedSectorButNotRearOrEnemyBehindWall()
        {
            DungeonPlayer player = Player(DungeonHero.First);
            Vector2 aim = new Vector2(Mathf.Cos(30 * Mathf.Deg2Rad), Mathf.Sin(30 * Mathf.Deg2Rad));
            DungeonEnemy front = Enemy(player, aim * 1.2f);
            DungeonEnemy rear = Enemy(player, -aim * 1.2f);
            Vector2 blockedDirection = new Vector2(Mathf.Cos(-20 * Mathf.Deg2Rad), Mathf.Sin(-20 * Mathf.Deg2Rad));
            DungeonEnemy behindWall = Enemy(player, blockedDirection * 1.4f);
            BoxCollider2D obstacle = Wall(blockedDirection * 0.75f, new Vector2(0.12f, 0.42f));
            RaycastHit2D[] sight = Physics2D.LinecastAll(Origin, behindWall.transform.position);
            Assert.That(System.Array.Exists(sight, hit => hit.collider == obstacle), Is.True,
                "The encounter fixture must actually place a solid collider across the victim's sight line.");

            Assert.That(player.TryAttack(aim), Is.True);
            yield return new WaitForSeconds(0.25f);

            Assert.That(front.Health, Is.LessThan(front.MaxHealth), "A non-cardinal aimed slash must hit the exposed enemy.");
            Assert.That(rear.Health, Is.EqualTo(rear.MaxHealth), "The sword must not damage behind the attacker.");
            Assert.That(behindWall.Health, Is.EqualTo(behindWall.MaxHealth), "A melee arc cannot pass through a solid wall.");
        }

        [UnityTest]
        public IEnumerator ArrowTravelsAtArbitraryAngleAndDamagesEnemy()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            Vector2 aim = new Vector2(Mathf.Cos(37 * Mathf.Deg2Rad), Mathf.Sin(37 * Mathf.Deg2Rad));
            DungeonEnemy enemy = Enemy(player, aim * 6);
            Assert.That(player.TryAttack(aim), Is.True);
            yield return new WaitForSeconds(0.18f);
            Assert.That(player.BowShotSequence, Is.EqualTo(1), "One arrow release should drive one recoil impulse.");
            DungeonArrow arrow = fixture.GetComponentInChildren<DungeonArrow>();
            Assert.That(arrow, Is.Not.Null, "The bow must create an actual travelling projectile.");
            Assert.That(Vector2.Dot(arrow.Direction, aim), Is.GreaterThan(0.9999f), "Arrow aim must not snap to four or eight directions.");
            yield return new WaitForSeconds(0.5f);
            Assert.That(enemy.Health, Is.InRange(enemy.MaxHealth - 1.5f, enemy.MaxHealth - 1f));
            Assert.That(fixture.GetComponentsInChildren<DungeonArrow>(), Is.Empty, "The projectile is consumed by the hit.");
        }

        [UnityTest]
        public IEnumerator ArrowStopsOnWallBeforeEnemy()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            float angle = 23;
            Vector2 aim = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            DungeonEnemy enemy = Enemy(player, aim * 4);
            Wall(aim * 1.5f, new Vector2(0.15f, 2), angle);
            Assert.That(player.TryAttack(aim), Is.True);
            yield return new WaitForSeconds(0.32f);
            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
            DungeonArrow arrow = fixture.GetComponentInChildren<DungeonArrow>();
            Assert.That(arrow, Is.Not.Null, "The wall should retain the arrow briefly as impact feedback.");
            Assert.That(arrow.IsStuck, Is.True, "The swept projectile must become stationary at the first wall.");
            Vector3 lodgedPosition = arrow.transform.position;
            yield return new WaitForSeconds(0.35f);
            Assert.That(arrow.transform.position, Is.EqualTo(lodgedPosition), "Only the arrow pixels may vibrate; its logical position stays at the wall.");
            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth), "A lodged arrow cannot continue damaging actors behind the wall.");
            player.SetInputEnabled(false);
            yield return new WaitForSeconds(0.3f);
            Assert.That(arrow != null && arrow.IsStuck, Is.True, "Opening the menu must pause the lodged-arrow lifetime too.");
            player.SetInputEnabled(true);
            yield return new WaitForSeconds(1);
            Assert.That(fixture.GetComponentsInChildren<DungeonArrow>(), Is.Empty, "The short-lived wall arrow should clean itself up after fading.");
        }

        [UnityTest]
        public IEnumerator ArrowTipsEmbedIntoAllFourStoneFacesWhileLightAnchorsStayOutside()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            player.transform.position = Origin + Vector2.one * 0.5f;
            var grid = new GameObject("Wall projection grid", typeof(Grid));
            grid.transform.SetParent(fixture.transform, false);
            grid.transform.position = Origin;
            var layer = new GameObject("Stone tiles", typeof(Tilemap), typeof(TilemapCollider2D));
            layer.transform.SetParent(grid.transform, false);
            Tilemap map = layer.GetComponent<Tilemap>();
            Tile stone = ScriptableObject.CreateInstance<Tile>();
            stone.colliderType = Tile.ColliderType.Grid;
            ownedAssets.Add(stone);
            map.SetTile(new Vector3Int(0, 2, 0), stone);
            map.SetTile(new Vector3Int(0, -2, 0), stone);
            map.SetTile(new Vector3Int(2, 0, 0), stone);
            map.SetTile(new Vector3Int(-2, 0, 0), stone);
            TilemapCollider2D walls = layer.GetComponent<TilemapCollider2D>();
            walls.ProcessTilemapChanges();
            Physics2D.SyncTransforms();

            DungeonArrow north = DungeonArrow.Launch(player, Vector2.up);
            DungeonArrow south = DungeonArrow.Launch(player, Vector2.down);
            DungeonArrow east = DungeonArrow.Launch(player, Vector2.right);
            DungeonArrow west = DungeonArrow.Launch(player, Vector2.left);
            yield return new WaitForSeconds(0.2f);

            Assert.That(north.VisibleTip.y, Is.EqualTo(Origin.y + 2.85f).Within(1f / 32),
                "The north wall should receive the tip farther into its raised front-face art.");
            Assert.That(south.VisibleTip.y, Is.EqualTo(Origin.y - 1.25f).Within(1f / 32),
                "The south-wall tip must be inside the wall, not floating above the room floor.");
            Assert.That(east.VisibleTip.x, Is.EqualTo(Origin.x + 2.25f).Within(1f / 32));
            Assert.That(west.VisibleTip.x, Is.EqualTo(Origin.x - 1.25f).Within(1f / 32));
            foreach (DungeonArrow arrow in new[] { north, south, east, west })
            {
                Assert.That(arrow.IsStuck, Is.True);
                Assert.That(walls.OverlapPoint(arrow.VisibleTip), Is.True,
                    "The actual displayed tip must overlap its stone tile, including beside a side-wall corner.");
                Assert.That(walls.OverlapPoint(arrow.transform.position), Is.False,
                    "The logical projectile and its illumination anchor remain on the room side of collision.");
                SpriteRenderer tipPixel = arrow.transform.GetChild(0).GetComponent<SpriteRenderer>();
                Assert.That((Vector2)tipPixel.transform.position, Is.EqualTo(arrow.VisibleTip),
                    "VisibleTip must report the rendered head pixel, not a separate theoretical contact point.");
                var properties = new MaterialPropertyBlock();
                tipPixel.GetPropertyBlock(properties);
                Vector4 light = properties.GetVector("_GroundLightPosition");
                Assert.That(new Vector2(light.x, light.y), Is.EqualTo((Vector2)arrow.transform.position));
            }
        }

        [UnityTest]
        public IEnumerator LandmarkArrowUsesItsOwnSilhouetteInsteadOfNorthWallInset()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            DungeonFxSettings.Current.arrowNorthWallInset = 1;
            var landmark = new GameObject("Solid landmark", typeof(SpriteRenderer), typeof(PolygonCollider2D));
            landmark.transform.SetParent(fixture.transform, false);
            landmark.transform.position = Origin + Vector2.up * 2.5f;
            landmark.transform.localScale = Vector3.one * 3;
            Texture2D white = Texture2D.whiteTexture;
            Sprite art = Sprite.Create(white, new Rect(0, 0, white.width, white.height), Vector2.one * 0.5f, white.width);
            ownedAssets.Add(art);
            SpriteRenderer visual = landmark.GetComponent<SpriteRenderer>();
            visual.sprite = art;
            PolygonCollider2D collider = landmark.GetComponent<PolygonCollider2D>();
            collider.SetPath(0, new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) });
            Physics2D.SyncTransforms();
            DungeonArrow arrow = DungeonArrow.Launch(player, Vector2.up);
            yield return new WaitForSeconds(0.2f);
            Assert.That(arrow.IsStuck, Is.True);
            Assert.That(arrow.VisibleTip.y, Is.InRange(Origin.y + 1.70f, Origin.y + 1.82f),
                "An object's lower rim is not a north terrain wall and must ignore the large north-wall inset.");
            Assert.That(visual.bounds.Contains(arrow.VisibleTip), Is.True);
            Assert.That(collider.OverlapPoint(arrow.transform.position), Is.False);
        }

        [Test]
        public void DirectionalBlockConsumesOneHitAndStunsAttacker()
        {
            DungeonPlayer player = Player(DungeonHero.First);
            DungeonEnemy attacker = Enemy(player, Vector2.right);
            player.SetAimDirection(Vector2.right);
            player.SetBlocking(true);

            Assert.That(player.TakeDamage(1, attacker.transform.position, attacker), Is.False);
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
            Assert.That(player.BlockCooldownRemaining, Is.EqualTo(2).Within(0.001));
            Assert.That(attacker.StunRemaining, Is.EqualTo(0.6f).Within(0.001));
            Assert.That(player.IsBlocking, Is.False, "The first blocked hit consumes the guard even while RMB remains held.");
            DungeonCombatFx[] blockedEffects = fixture.GetComponentsInChildren<DungeonCombatFx>();
            Assert.That(System.Array.Exists(blockedEffects, fx => fx.EffectKind == "Parry" && fx.ActivePixelCount > 0), Is.True,
                "A successful block needs its own visible contact effect.");
            Assert.That(System.Array.Exists(blockedEffects, fx => fx.ProducesBlood), Is.False,
                "Blocking an attack must not produce blood.");

            Assert.That(player.TakeDamage(1, attacker.transform.position, attacker), Is.True, "A second hit during block cooldown must deal damage.");
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth - 1));
            DungeonCombatFx[] hurtEffects = fixture.GetComponentsInChildren<DungeonCombatFx>();
            Assert.That(System.Array.Exists(hurtEffects, fx => fx.ProducesBlood), Is.False,
                "The restored default presentation keeps metal sparks and disables blood.");
            Assert.That(System.Array.Exists(hurtEffects, fx => fx.EffectKind == "Hit" && fx.ActivePixelCount > 0), Is.True,
                "Damage retains a visible spark impact when blood is disabled.");
            Assert.That(player.TakeDamage(1, attacker.transform.position, attacker), Is.False);
            Assert.That(fixture.GetComponentsInChildren<DungeonCombatFx>().Length, Is.EqualTo(hurtEffects.Length),
                "Invulnerability must not emit another hit effect.");
        }

        [UnityTest]
        public IEnumerator ParryHitStopIsLocalBriefAndPausesWithMenu()
        {
            DungeonFxSettings.Current.hitStop = true;
            DungeonPlayer player = Player(DungeonHero.First);
            DungeonEnemy attacker = Enemy(player, Vector2.right);
            player.SetAimDirection(Vector2.right);
            player.SetBlocking(true);
            Assert.That(player.TakeDamage(1, attacker.transform.position, attacker), Is.False);
            Assert.That(player.IsHitStopped, Is.True);
            Assert.That(attacker.IsHitStopped, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1), "Combat emphasis must never stop the global game clock.");
            player.SetInputEnabled(false);
            yield return new WaitForSeconds(0.12f);
            Assert.That(player.IsHitStopped, Is.True, "Menu pause preserves the remaining local stop.");
            player.SetInputEnabled(true);
            yield return new WaitForSeconds(0.12f);
            Assert.That(player.IsHitStopped, Is.False, "The impact freeze is only about 40 ms, not a stun of the player.");
            Assert.That(attacker.IsHitStopped, Is.False);
            Assert.That(attacker.StunRemaining, Is.GreaterThan(0.35f), "The enemy's longer parry stun remains after the brief impact stop.");
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [Test]
        public void BlockDoesNotProtectTheBackAndArcherCannotBlock()
        {
            DungeonPlayer swordsman = Player(DungeonHero.First);
            swordsman.SetAimDirection(Vector2.right);
            swordsman.SetBlocking(true);
            Assert.That(swordsman.TakeDamage(1, Origin + Vector2.left), Is.True);
            Assert.That(swordsman.BlockCooldownRemaining, Is.Zero, "An unblocked rear hit must not spend the guard cooldown.");
            swordsman.SetInputEnabled(false);

            DungeonPlayer archer = Player(DungeonHero.Second);
            archer.transform.position += Vector3.up * 4;
            Physics2D.SyncTransforms();
            archer.SetAimDirection(Vector2.right);
            archer.SetBlocking(true);
            Assert.That(archer.IsBlocking, Is.False);
            Assert.That(archer.TakeDamage(1, (Vector2)archer.transform.position + Vector2.right), Is.True);
            Assert.That(archer.BlockCooldownRemaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BlockBecomesAvailableAfterTwoSeconds()
        {
            DungeonPlayer player = Player(DungeonHero.First);
            player.SetAimDirection(Vector2.right);
            player.SetBlocking(true);
            Assert.That(player.TakeDamage(1, Origin + Vector2.right), Is.False);
            yield return new WaitForSeconds(1.2f);
            player.SetBlocking(true);
            Assert.That(player.IsBlocking, Is.False);
            yield return new WaitForSeconds(0.9f);
            player.SetAimDirection(Vector2.right);
            player.SetBlocking(true);
            Assert.That(player.IsBlocking, Is.True);
            Assert.That(player.TakeDamage(1, Origin + Vector2.right), Is.False);
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
        }

        [UnityTest]
        public IEnumerator RollHasBriefInvulnerabilityAndOnePointFiveSecondCooldownAndSolidCollision()
        {
            DungeonPlayer player = Player(DungeonHero.First);
            Wall(Vector2.right, new Vector2(0.2f, 5));
            Assert.That(player.TryRoll(Vector2.right), Is.True);
            Assert.That(player.RollCooldownRemaining, Is.EqualTo(player.Hero.RollRecovery).Within(0.001));
            Assert.That(player.TakeDamage(1, Origin + Vector2.right), Is.False);
            Assert.That(player.TryRoll(Vector2.left), Is.False);
            yield return new WaitForSeconds(0.18f);
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
            Assert.That(player.transform.position.x, Is.LessThan(Origin.x + 0.73f), "Rolling must stop at the wall with the feet collider outside it.");
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.IsRolling, Is.False);
            Assert.That(player.TakeDamage(1, (Vector2)player.transform.position + Vector2.right), Is.True, "Roll immunity must end before the next roll is available.");
            Assert.That(player.TryRoll(Vector2.left), Is.False);
            yield return new WaitForSeconds(1.25f);
            Assert.That(player.TryRoll(Vector2.left), Is.True, "A second roll is available 1.5 seconds after the first started.");
        }

        [UnityTest]
        public IEnumerator MenuPauseFreezesAttackProjectileAndEnemyPursuit()
        {
            DungeonPlayer player = Player(DungeonHero.Second);
            DungeonEnemy enemy = Enemy(player, Vector2.right * 8, false);
            Assert.That(player.TryAttack(Vector2.right), Is.True);
            player.SetInputEnabled(false);
            Vector3 enemyStart = enemy.transform.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.AttackProgress, Is.Zero);
            Assert.That(enemy.transform.position, Is.EqualTo(enemyStart));
            Assert.That(fixture.GetComponentsInChildren<DungeonArrow>(), Is.Empty);

            player.SetInputEnabled(true);
            yield return new WaitForSeconds(0.2f);
            DungeonArrow arrow = fixture.GetComponentInChildren<DungeonArrow>();
            Assert.That(arrow, Is.Not.Null);
            Assert.That(enemy.transform.position.x, Is.LessThan(enemyStart.x));
            player.SetInputEnabled(false);
            // Finish the current physics step before measuring paused roots (Rigidbody interpolation).
            yield return new WaitForFixedUpdate();
            Vector2 enemyPaused = enemy.GetComponent<Rigidbody2D>().position;
            Vector3 arrowPaused = arrow.transform.position;
            float attackPaused = player.AttackProgress;
            yield return new WaitForSeconds(0.3f);
            Assert.That(enemy.GetComponent<Rigidbody2D>().position, Is.EqualTo(enemyPaused));
            Assert.That(arrow.transform.position, Is.EqualTo(arrowPaused));
            Assert.That(player.AttackProgress, Is.EqualTo(attackPaused));
            player.SetInputEnabled(true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth), "Combat resumes from the same projectile state after closing the menu.");
        }
    }
}
