using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private RectTransform authoredHud;
        private DungeonSpellHud spellHud;
        private DungeonHudFill hpArt, mpArt;
        private DungeonHudParticles resourceParticles;
        private RawImage attackArt, blockArt, dashArt;
        private RawImage attackShade, blockShade, dashShade;
        private Texture2D swordNormal, swordPressed, bowNormal, bowPressed;
        private Texture2D blockNormal, blockPressed, emptySlot, dashNormal, dashPressed;

        private RawImage HudIcon(string name, Transform parent, string asset, Vector2 position, float size)
        {
            RawImage icon = Raw(name, parent, position, Vector2.one * size);
            icon.texture = Resources.Load<Texture2D>("InterfaceArt/Stats/" + asset);
            icon.gameObject.AddComponent<DungeonUiArtScale>().iconSize = size;
            return icon;
        }

        private static Texture2D HudTexture(string asset) => Resources.Load<Texture2D>("InterfaceArt/Hud/" + asset);

        // All authored coordinates are top-left pixel positions from the supplied README.
        private RectTransform HudRect(string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, authoredHud, Vector2.zero, size);
            Anchor(rect, new Vector2(0, 1), new Vector2(position.x, -position.y));
            return rect;
        }

        private RawImage HudLayer(string name, string asset, Vector2 position)
        {
            Texture2D texture = HudTexture(asset);
            var image = HudRect(name, position, new Vector2(texture.width, texture.height)).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            return image;
        }

        private DungeonHudFill HudFill(string name, string asset, Vector2 position)
        {
            Texture2D texture = HudTexture(asset);
            var fill = HudRect(name, position, new Vector2(texture.width, texture.height)).gameObject.AddComponent<DungeonHudFill>();
            fill.raycastTarget = false; fill.Initialize(texture); return fill;
        }

        private void BuildAuthoredHud()
        {
            var spellRoot = Rect("Spell HUD", hud.transform, Vector2.zero, new Vector2(265, 65));
            spellRoot.anchorMin = spellRoot.anchorMax = new Vector2(.5f, 0);
            spellRoot.anchoredPosition = new Vector2(0, 76);
            spellHud = spellRoot.gameObject.AddComponent<DungeonSpellHud>();
            authoredHud = Rect("Authored HUD", hud.transform, Vector2.zero, new Vector2(165, 57));
            Anchor(authoredHud, new Vector2(0, 1), new Vector2(20, -20));
            authoredHud.localScale = Vector3.one * 2;
            HudLayer("HUD base", "hud_base", Vector2.zero);
            hpArt = HudFill("Health fill", "health_fill", new Vector2(49, 20));
            HudLayer("Mana track", "mana_track", new Vector2(49, 29));
            mpArt = HudFill("Mana fill", "mana_fill", new Vector2(49, 29));
            // Slot overlays cover the junction between the diamond and the bars.
            attackArt = HudLayer("Attack", "sword_normal", new Vector2(18, 7));
            HudLayer("Sound placeholder", "sound_normal", new Vector2(7, 18));
            blockArt = HudLayer("Block", "block_normal", new Vector2(29, 18));
            dashArt = HudLayer("Dash", "dash_normal", new Vector2(18, 29));
            attackShade = HudLayer("Attack cooldown", "cooldown_overlay", new Vector2(18, 7));
            blockShade = HudLayer("Block cooldown", "cooldown_overlay", new Vector2(29, 18));
            dashShade = HudLayer("Dash cooldown", "cooldown_overlay", new Vector2(18, 29));
            resourceParticles = HudRect("Resource particles", Vector2.zero, new Vector2(165, 57)).gameObject.AddComponent<DungeonHudParticles>();
            resourceParticles.raycastTarget = false;
            hpArt.PixelDetached += (point, tint) => resourceParticles.Detach(
                resourceParticles.rectTransform.InverseTransformPoint(hpArt.rectTransform.TransformPoint(point)), tint, false);
            mpArt.PixelDetached += (point, tint) => resourceParticles.Detach(
                resourceParticles.rectTransform.InverseTransformPoint(mpArt.rectTransform.TransformPoint(point)), tint, true);
            hpArt.PixelRestored += (point, tint) => resourceParticles.GatherHealth(
                resourceParticles.rectTransform.InverseTransformPoint(hpArt.rectTransform.TransformPoint(point)));
            mpArt.PixelRestored += (point, tint) => resourceParticles.Gather(
                resourceParticles.rectTransform.InverseTransformPoint(mpArt.rectTransform.TransformPoint(point)), tint);
            swordNormal = HudTexture("sword_normal"); swordPressed = HudTexture("sword_pressed");
            bowNormal = HudTexture("bow_normal"); bowPressed = HudTexture("bow_pressed");
            blockNormal = HudTexture("block_normal"); blockPressed = HudTexture("block_pressed");
            emptySlot = HudTexture("empty_normal");
            dashNormal = HudTexture("dash_normal"); dashPressed = HudTexture("dash_pressed");
        }

        private static void Cooldown(RawImage image, Vector2 position, float remaining)
        {
            int height = Mathf.RoundToInt(21 * Mathf.Clamp01(remaining));
            image.enabled = height > 0;
            image.rectTransform.anchoredPosition = new Vector2(position.x, -position.y - (21 - height));
            image.rectTransform.sizeDelta = new Vector2(21, height);
            image.uvRect = new Rect(0, 0, 1, height / 21f);
        }

        private void RefreshAuthoredHud()
        {
            float canvasScale = Mathf.Max(.01f, uiCanvas.scaleFactor);
            float scale = Mathf.Max(1, Mathf.Round(2 * canvasScale)) / canvasScale;
            authoredHud.localScale = new Vector3(scale, scale, 1);
            spellHud.transform.localScale = new Vector3(scale, scale, 1);
            ((RectTransform)spellHud.transform).anchoredPosition = new Vector2(0, 16 + 30 * scale);
            if (statsPanel != null) statsPanel.anchoredPosition = new Vector2(24, -20 - 57 * scale - 12);
            if (voiceStatus != null) voiceStatus.gameObject.SetActive(false);
            DungeonPlayer player = gameplayPlayer;
            hpArt.AnimateTo(player.Health / (float)player.MaxHealth);
            mpArt.AnimateTo(player.MaxMana > 0 ? player.Mana / (float)player.MaxMana : 0);
            attackArt.texture = player.Hero.IsArcher
                ? (player.IsAttacking ? bowPressed : bowNormal)
                : (player.IsAttacking ? swordPressed : swordNormal);
            blockArt.texture = player.Hero.IsArcher ? emptySlot : player.IsBlocking ? blockPressed : blockNormal;
            dashArt.texture = player.IsRolling ? dashPressed : dashNormal;
            Cooldown(attackShade, new Vector2(18, 7), player.IsAttacking ? 1 - player.AttackProgress : 0);
            Cooldown(blockShade, new Vector2(29, 18), player.Hero.IsArcher ? 0 : player.BlockCooldownRemaining / player.Hero.BlockRecovery);
            Cooldown(dashShade, new Vector2(18, 29), player.RollCooldownRemaining / player.Hero.RollRecovery);
        }
    }
}
