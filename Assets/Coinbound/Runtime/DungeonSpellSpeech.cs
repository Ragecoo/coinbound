using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed class DungeonSpellSpeech : MonoBehaviour
    {
        private DungeonPlayer owner;
        private DungeonPixelText label;
        private Vector3 origin;
        private Vector2 direction;
        private bool success;
        private float age;
        public static void Create(DungeonPlayer player, string word, bool success)
        {
            if (player == null) return;
            var obj = new GameObject("Spoken spell", typeof(Canvas));
            obj.transform.SetParent(player.transform.parent, false);
            Canvas canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; canvas.pixelPerfect = true;
            var fx = obj.AddComponent<DungeonSpellSpeech>(); fx.owner = player; fx.success = success;
            fx.origin = player.transform.position + Vector3.up * 1.35f;
            fx.direction = new Vector2(Random.Range(-.7f, .7f), 1).normalized;
            var textObject = new GameObject("Word", typeof(RectTransform)); textObject.transform.SetParent(obj.transform, false);
            fx.label = textObject.AddComponent<DungeonPixelText>();
            fx.label.font = Resources.Load<Font>("InterfaceFonts/OpirusOpik");
            fx.label.ExtraPixelSteps = 0; fx.label.fontSize = 32; fx.label.text = word;
            fx.label.raycastTarget = false; fx.label.alignment = TextAnchor.MiddleCenter;
            fx.label.rectTransform.anchorMin = fx.label.rectTransform.anchorMax = Vector2.zero;
            fx.label.rectTransform.sizeDelta = new Vector2(180, 50);
            textObject.AddComponent<Shadow>().effectColor = Color.black;
        }
        private void LateUpdate()
        {
            if (owner == null || owner.IsDead) { Destroy(gameObject); return; }
            Canvas canvas = GetComponent<Canvas>();
            canvas.enabled = owner.CanAct;
            if (!owner.CanAct) return;
            age += Time.deltaTime;
            if (age >= 1.1f) { Destroy(gameObject); return; }
            Camera camera = Camera.main;
            if (camera == null) return;
            float fade = Mathf.Clamp01((1.1f - age) / .5f);
            Color color = age < .18f ? new Color(1, .96f, .82f) : success ? new Color(.4f, 1, .45f) : new Color(1, .25f, .25f);
            color.a = fade; label.color = color;
            Vector2 point = (Vector2)camera.WorldToScreenPoint(origin) + direction * (age * 48);
            if (!success && age >= .18f && age < .4f) point.x += Mathf.Round(Mathf.Sin(age * 90)) * 2;
            label.rectTransform.anchoredPosition = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
        }
    }
}
