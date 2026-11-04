using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>One-shot chest animation and interaction rules, independent of spawn planning.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DungeonChest : MonoBehaviour
    {
        public enum ChestState { Closed, Opening, Open }
        public const float InteractionDistance = 1.4f;
        public const float FrameSeconds = .065f;
        private static readonly List<DungeonChest> Active = new List<DungeonChest>();
        private readonly Sprite[] frames = new Sprite[9];
        private SpriteRenderer visual;
        private Collider2D body;
        private float elapsed;
        public ChestState State { get; private set; }
        public int FrameIndex { get; private set; }
        public event Action<DungeonChest> Opened;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        public void Initialize(string facing)
        {
            string folder = facing switch { "n" => "north", "w" => "west", "e" => "east", _ => "south" };
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>($"ChestArt/{folder}/chest_{i:00}");
                if (frames[i] == null) throw new InvalidOperationException($"Missing chest frame {folder}/{i:00}");
            }
            visual = GetComponent<SpriteRenderer>();
            visual.sprite = frames[0];
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.75f, .34f);
            collider.offset = new Vector2(0, .10f);
            body = collider;
            State = ChestState.Closed; elapsed = 0; FrameIndex = 0;
        }

        public bool CanInteract(DungeonPlayer player)
        {
            if (visual == null || !isActiveAndEnabled || State != ChestState.Closed || player == null ||
                !player.CanAct || player.IsRolling || player.IsAttacking || player.IsBlocking || player.IsHitStopped) return false;
            Vector2 offset = (Vector2)transform.position - (Vector2)player.transform.position;
            if (offset.sqrMagnitude < .001f || offset.sqrMagnitude > InteractionDistance * InteractionDistance) return false;
            if (Vector2.Dot(player.InteractionFacing, offset.normalized) < .65f) return false;
            return DungeonCombatPhysics.ClearLine(player.transform.position, transform.position, 0, body);
        }

        public static DungeonChest FindTarget(DungeonPlayer player)
        {
            DungeonChest result = null;
            float nearest = float.PositiveInfinity;
            foreach (DungeonChest chest in Active)
            {
                if (chest == null || !chest.CanInteract(player)) continue;
                float distance = ((Vector2)chest.transform.position - (Vector2)player.transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; result = chest;
            }
            return result;
        }

        public bool TryOpen(DungeonPlayer player)
        {
            if (!CanInteract(player)) return false;
            State = ChestState.Opening;
            elapsed = 0;
            return true;
        }

        private void Update()
        {
            if (State != ChestState.Opening) return;
            elapsed += Time.deltaTime;
            FrameIndex = Mathf.Min(8, Mathf.FloorToInt(elapsed / FrameSeconds));
            visual.sprite = frames[FrameIndex];
            if (FrameIndex < 8) return;
            State = ChestState.Open;
            Opened?.Invoke(this);
        }
    }
}
