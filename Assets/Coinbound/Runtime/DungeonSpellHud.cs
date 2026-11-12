using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Presentation of spell state; casting and resource costs remain in the spellcaster.</summary>
    public sealed class DungeonSpellHud : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform rect;
            public RawImage art;
            public Texture2D smallReady, smallInactive, largeReady, largeInactive;
            public DungeonSpellCooldown mask;
            public Vector2 start, destination;
            public RectTransform costRoot;
            public readonly RawImage[] glyphs = new RawImage[4];
            public float mana = -1;
            public float manaFlash, cooldownFlash;
        }
        private readonly Slot[] slots = new Slot[5];
        private readonly int[] order = { 1, 2, 0, 3, 4 };
        private DungeonSpellcaster caster;
        private DungeonHudParticles particles;
        private float transition = 1;
        private int centered;
        public DungeonSpell CenteredSpell => (DungeonSpell)centered;
        private static Texture2D Art(string path) => Resources.Load<Texture2D>("InterfaceArt/SpellHud/" + path);

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size;
            return rect;
        }
        private static RawImage Image(string name, Transform parent, Texture2D texture)
        {
            var image = Rect(name, parent, new Vector2(texture.width, texture.height)).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false; return image;
        }
        private void Awake()
        {
            EnsureBuilt();
        }
        private void EnsureBuilt()
        {
            if (particles != null) return;
            string[] names = { "pulse", "trace", "ward", "decoy", "recall" };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = new Slot();
                slot.rect = Rect(names[i], transform, Vector2.one * 55);
                slot.smallReady = Art(names[i] + "/small_active");
                slot.smallInactive = Art(names[i] + "/small_inactive");
                slot.largeReady = Art(names[i] + "/large_active");
                slot.largeInactive = Art(names[i] + "/large_inactive");
                slot.art = Image("Icon", slot.rect, slot.smallReady);
                slot.mask = Rect("Cooldown", slot.rect, Vector2.one * 55).gameObject.AddComponent<DungeonSpellCooldown>();
                slot.mask.Initialize(Art("cooldown/small"), Art("cooldown/large"));
                slot.costRoot = Rect("Mana cost", slot.rect, new Vector2(24, 8));
                slot.costRoot.anchorMin = slot.costRoot.anchorMax = slot.costRoot.pivot = new Vector2(0, 1);
                for (int g = 0; g < slot.glyphs.Length; g++)
                {
                    RawImage glyph = slot.glyphs[g] = Image("Glyph " + g, slot.costRoot, Art("glyphs/0"));
                    glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = glyph.rectTransform.pivot = new Vector2(0, 1);
                }
                UpdateCost(slot, SpellDefinition.All[i].Mana, i == centered);
            }
            particles = Rect("Spell sparks", transform, new Vector2(265, 65)).gameObject.AddComponent<DungeonHudParticles>();
            particles.raycastTarget = false;
            SetDestinations(true);
        }
        private static void UpdateCost(Slot slot, float mana, bool large)
        {
            slot.costRoot.anchoredPosition = new Vector2(29, large ? -33 : -31);
            if (slot.mana == mana) return;
            slot.mana = mana;
            string text = Mathf.RoundToInt(mana) + "MP";
            float x = 0;
            for (int i = 0; i < slot.glyphs.Length; i++)
            {
                RawImage glyph = slot.glyphs[i]; glyph.gameObject.SetActive(i < text.Length);
                if (i >= text.Length) continue;
                Texture2D texture = Art("glyphs/" + text[i]); glyph.texture = texture;
                glyph.rectTransform.sizeDelta = new Vector2(texture.width, texture.height);
                glyph.rectTransform.anchoredPosition = new Vector2(x, 0);
                x += texture.width + 1;
            }
        }
        public void Bind(DungeonSpellcaster value)
        {
            EnsureBuilt();
            if (caster != null) { caster.CastSucceeded -= OnCast; caster.CastRejected -= OnRejected; }
            caster = value;
            if (caster != null) { caster.CastSucceeded += OnCast; caster.CastRejected += OnRejected; }
            centered = caster != null ? (int)caster.LastCast : 0; transition = 1;
            SortSlots();
            foreach (Slot slot in slots) slot.manaFlash = slot.cooldownFlash = 0;
            SetDestinations(true); particles.Clear();
        }
        private void OnDestroy() { if (caster != null) { caster.CastSucceeded -= OnCast; caster.CastRejected -= OnRejected; } }
        private void OnRejected(DungeonSpell spell, SpellCastResult result)
        {
            if (result == SpellCastResult.Cooldown && caster.Cooldown(spell) > 0) slots[(int)spell].cooldownFlash = .24f;
            if (result == SpellCastResult.NotEnoughMana) slots[(int)spell].manaFlash = .3f;
        }
        private void SortSlots()
        {
            int side = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == centered) continue;
                order[side < 2 ? side : side + 1] = i; side++;
            }
            order[2] = centered;
        }
        private void SetDestinations(bool immediate)
        {
            for (int i = 0; i < order.Length; i++)
            {
                Slot slot = slots[order[i]];
                slot.start = slot.rect.anchoredPosition;
                slot.destination = new Vector2((i - 2) * 48, i == 2 ? 3 : 0);
                if (immediate) slot.rect.anchoredPosition = slot.destination;
            }
            slots[centered].rect.SetAsLastSibling();
            if (particles != null) particles.transform.SetAsLastSibling();
        }
        private void OnCast(DungeonSpell spell)
        {
            centered = (int)spell; SortSlots();
            transition = 0; SetDestinations(DungeonSettings.ReducedMotion);
            if (DungeonSettings.ReducedMotion) transition = 1;
        }
        private void LateUpdate()
        {
            if (caster == null) return;
            float previous = transition;
            transition = Mathf.Min(1, transition + Time.deltaTime / .32f);
            float ease = transition * transition * (3 - 2 * transition);
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i]; bool large = i == centered;
                Vector2 position = Vector2.Lerp(slot.start, slot.destination, ease);
                if (large && !DungeonSettings.ReducedMotion) position.y += Mathf.Sin(transition * Mathf.PI) * 7;
                slot.rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
                bool active = caster.IsActive((DungeonSpell)i);
                slot.art.texture = large ? (active ? slot.largeReady : slot.largeInactive) : (active ? slot.smallReady : slot.smallInactive);
                float duration = caster.CooldownDuration((DungeonSpell)i);
                slot.cooldownFlash = Mathf.Max(0, slot.cooldownFlash - Time.deltaTime);
                slot.manaFlash = Mathf.Max(0, slot.manaFlash - Time.deltaTime);
                slot.mask.SetState(large, duration > 0 ? caster.Cooldown((DungeonSpell)i) / duration : 0, slot.cooldownFlash);
                UpdateCost(slot, caster.ManaCost((DungeonSpell)i), large);
                foreach (RawImage glyph in slot.glyphs) glyph.color = slot.manaFlash > 0 ? new Color(1,.35f,.3f) : Color.white;
            }
            if (previous < 1 && transition == 1)
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3;
                    particles.Detach(new Vector2(Mathf.Cos(angle) * 21, 3 + Mathf.Sin(angle) * 21), new Color32(180, 207, 255, 180), true);
                }
        }
    }

    /// <summary>Authored cooldown mask with the supplied four-row feather, kept on the pixel grid.</summary>
    public sealed class DungeonSpellCooldown : MaskableGraphic
    {
        private Texture2D small, large, current;
        private float remaining;
        private float flash;
        public override Texture mainTexture => current;
        public void Initialize(Texture2D smallMask, Texture2D largeMask)
        { small = DenseMask(smallMask); large = DenseMask(largeMask); raycastTarget = false; }
        private static Texture2D DenseMask(Texture2D source)
        {
            // Strengthen the supplied silhouette once; the animated four-pixel edge stays unchanged.
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                pixel.a = (byte)Mathf.RoundToInt(255 * (1 - Mathf.Pow(1 - pixel.a / 255f, 2.5f)));
                pixel.r = (byte)(pixel.r * .45f); pixel.g = (byte)(pixel.g * .45f); pixel.b = (byte)(pixel.b * .45f);
                pixels[i] = pixel;
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            { name = source.name + " dense cooldown", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        protected override void OnDestroy()
        {
            if (small != null) Destroy(small);
            if (large != null) Destroy(large);
            base.OnDestroy();
        }
        public void SetState(bool expanded, float fraction, float rejectedFlash = 0)
        {
            Texture2D next = expanded ? large : small;
            if (next == current && fraction == remaining && flash == rejectedFlash) return;
            current = next; remaining = Mathf.Clamp01(fraction); flash = rejectedFlash; SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (current == null || remaining <= 0) return;
            float edge = -2 + 59 * (1 - remaining);
            for (int y = 0; y < 55; y++)
            {
                float alpha = Mathf.Clamp01((y + .5f - edge + 2) / 4);
                if (flash > 0) alpha = Mathf.Max(alpha, .7f) * (DungeonSettings.ReducedMotion ? 1 : Mathf.FloorToInt(flash * 16) % 2 == 0 ? 1 : .25f);
                if (alpha <= 0) continue;
                Color tint = new Color(1, 1, 1, alpha);
                int n = mesh.currentVertCount;
                float top = 27.5f - y, v = 1 - y / 55f;
                mesh.AddVert(new Vector3(-27.5f, top - 1), tint, new Vector2(0, v - 1 / 55f));
                mesh.AddVert(new Vector3(-27.5f, top), tint, new Vector2(0, v));
                mesh.AddVert(new Vector3(27.5f, top), tint, new Vector2(1, v));
                mesh.AddVert(new Vector3(27.5f, top - 1), tint, new Vector2(1, v - 1 / 55f));
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 3, n);
            }
        }
    }
}
