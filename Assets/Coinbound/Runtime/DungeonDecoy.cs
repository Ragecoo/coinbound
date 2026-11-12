using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonDecoy : MonoBehaviour
    {
        private static readonly List<DungeonDecoy> Active = new List<DungeonDecoy>();
        private DungeonPlayer owner;
        private float remaining;
        private SpriteRenderer visual;
        private DungeonCombatMesh motes;
        internal DungeonCombatNavigation Navigation { get; private set; }
        public bool IsAlive => remaining > 0 && owner != null && !owner.IsDead && isActiveAndEnabled;
        public static DungeonDecoy Create(DungeonPlayer player, DungeonMap map, float seconds)
        {
            var obj = new GameObject("Spectral decoy"); obj.transform.SetParent(player.transform.parent, false);
            obj.transform.position = player.transform.position;
            var copy = obj.AddComponent<DungeonDecoy>(); copy.owner = player; copy.remaining = seconds;
            player.Spells.Track(obj);
            copy.motes = DungeonCombatMesh.Create(obj.transform, "Decoy motes", player.EffectMaterial);
            DungeonSpellFlash.Create(player.Spells, player.transform.position, Color.white, .18f);
            copy.Navigation = DungeonCombatNavigation.Independent(map);
            foreach (SpriteRenderer source in player.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!source.enabled || source.sprite == null || source.GetComponentInParent<DungeonContactShadow>() != null) continue;
                var art = new GameObject("Frozen hero pose", typeof(SpriteRenderer)); art.transform.SetParent(obj.transform, false);
                art.transform.position = source.transform.position; art.transform.rotation = source.transform.rotation;
                art.transform.localScale = source.transform.lossyScale;
                copy.visual = art.GetComponent<SpriteRenderer>();
                copy.visual.sprite = source.sprite; copy.visual.sharedMaterial = source.sharedMaterial;
                copy.visual.flipX = source.flipX; copy.visual.flipY = source.flipY;
                copy.visual.sortingOrder = DungeonDepthSort.OrderForY(obj.transform.position.y) + 1;
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_GhostWhite", 1); properties.SetFloat("_EffectUnlit", 1);
                copy.visual.SetPropertyBlock(properties); copy.visual.color = new Color(1,1,1,.45f);
                break;
            }
            return copy;
        }
        internal static DungeonDecoy Nearest(Vector2 position)
        {
            DungeonDecoy result = null; float best = 64;
            foreach (DungeonDecoy decoy in Active)
            {
                if (!decoy.IsAlive) continue;
                float distance = ((Vector2)decoy.transform.position - position).sqrMagnitude;
                if (distance <= best) { best = distance; result = decoy; }
            }
            return result;
        }
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
        private void LateUpdate()
        {
            if (owner == null || owner.IsDead) { Destroy(gameObject); return; }
            if (!owner.CanAct) return;
            remaining = owner.Spells.ActiveRemaining(DungeonSpell.Decoy);
            if (remaining <= 0) { Destroy(gameObject); return; }
            DungeonEffectLights.Glow(this, transform.position, 1.4f, .45f * Mathf.Min(1,remaining/.5f), new Color(.85f,.9f,1));
            if (visual != null) visual.color = new Color(1,1,1,.45f * Mathf.Min(1,remaining/.5f));
            Vector2 feet = transform.position;
            motes.Begin(feet + Vector2.up * .5f, feet, false, .5f, false, true);
            if (!DungeonSettings.ReducedMotion)
                for (int i = 0; i < 7; i++)
                {
                    float phase = (5 - remaining + i * .19f) % 1;
                    float spread = remaining < .5f ? (.5f - remaining) * .8f : 0;
                    float side = i % 2 == 0 ? -1 : 1;
                    Vector2 point = feet + new Vector2(side * (.2f + spread), phase * .9f);
                    motes.Pixel(point, new Color(.85f,.92f,1,(1-phase)*.55f*Mathf.Min(1,remaining/.2f)));
                }
            motes.End();
        }
    }
}
