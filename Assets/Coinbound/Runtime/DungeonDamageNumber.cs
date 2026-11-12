using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Pixel-snapped combat text using the same screen projection as spoken spells.</summary>
    public sealed class DungeonDamageNumber : MonoBehaviour
    {
        private DungeonPlayer owner;
        private DungeonPixelText label, weight;
        private Canvas surface;
        private Vector3 origin;
        private Color tint;
        private float age, drift;
        public static void Create(DungeonPlayer player, Vector3 position, float damage, bool incoming, bool critical)
        {
            if (player == null || damage <= 0 || (incoming ? !DungeonSettings.PlayerDamageNumbers : !DungeonSettings.EnemyDamageNumbers)) return;
            var obj = new GameObject("Damage number", typeof(Canvas));
            obj.transform.SetParent(player.transform.parent, false);
            var fx = obj.AddComponent<DungeonDamageNumber>();
            fx.owner = player; fx.origin = position + Vector3.up * 1.2f;
            fx.drift = Random.Range(-18f, 18f);
            fx.tint = incoming ? new Color(.95f, .22f, .25f, .4f) : new Color(.78f, .79f, .82f, .4f);
            fx.surface = obj.GetComponent<Canvas>(); fx.surface.renderMode = RenderMode.ScreenSpaceOverlay;
            fx.surface.sortingOrder = 109; fx.surface.pixelPerfect = true;
            string text = damage.ToString("0.0", CultureInfo.InvariantCulture) + (critical ? "!" : "");
            fx.label = fx.MakeLabel("Amount", text);
            // A one-screen-pixel overprint thickens the native pixel font without smoothing it.
            if (critical) { fx.weight = fx.MakeLabel("Critical weight", text); fx.label.GlyphScale = fx.weight.GlyphScale = 1.15f; }
            fx.Render();
        }
        private DungeonPixelText MakeLabel(string name, string text)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(transform, false);
            var result = obj.AddComponent<DungeonPixelText>();
            result.font = Resources.Load<Font>("InterfaceFonts/OpirusOpik");
            result.ExtraPixelSteps = 0; result.fontSize = 24; result.text = text;
            result.raycastTarget = false; result.alignment = TextAnchor.MiddleCenter;
            result.rectTransform.anchorMin = result.rectTransform.anchorMax = Vector2.zero;
            result.rectTransform.sizeDelta = new Vector2(180, 50); result.color = tint;
            return result;
        }
        private void LateUpdate()
        {
            if (owner == null) { Destroy(gameObject); return; }
            surface.enabled = owner.CanAct || owner.IsDead;
            if (!surface.enabled) return;
            age += Time.deltaTime;
            if (age >= .95f) { Destroy(gameObject); return; }
            Render();
        }
        private void Render()
        {
            var camera = Camera.main; if (camera == null) return;
            Vector3 projected = camera.WorldToScreenPoint(origin);
            if (projected.z < 0) { surface.enabled = false; return; }
            float bounce = DungeonSettings.ReducedMotion ? 0 : Mathf.Abs(Mathf.Sin(Mathf.Min(age / .48f, 1) * Mathf.PI)) * 18;
            Vector2 point = new Vector2(Mathf.Round(projected.x + (DungeonSettings.ReducedMotion ? 0 : drift * age)), Mathf.Round(projected.y + age * 22 + bounce));
            var color = tint; color.a *= Mathf.Clamp01((.95f - age) / .38f);
            label.color = color; label.rectTransform.anchoredPosition = point;
            if (weight != null) { weight.color = color; weight.rectTransform.anchoredPosition = point + Vector2.right; }
        }
    }
}
