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
        public const float FrameSeconds = .12f;
        private static readonly List<DungeonChest> Active = new List<DungeonChest>();
        private readonly Sprite[] frames = new Sprite[8];
        private static readonly string[] FrameNames={"00_closed","01_opening","02_opening","03_open","04_open_hold","05_closing","06_closing","07_closed"};
        public string Facing {get;private set;}
        private SpriteRenderer visual;
        private Collider2D body;
        private float elapsed;
        public ChestState State { get; private set; }
        public int FrameIndex { get; private set; }
        public event Action<DungeonChest> Opened;
        public static event Action<DungeonChest, DungeonPlayer> RewardRequested;
        private DungeonPlayer opener;
        public DungeonChestOffer Offer { get; private set; }
        public bool RewardClaimed { get; private set; }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        public void Initialize(string facing)
        {
            visual = GetComponent<SpriteRenderer>();
            FrameIndex=0;SetFacing(facing);
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.625f, .25f);
            collider.offset = Vector2.zero;
            body = collider;
            State = ChestState.Closed; elapsed = 0; FrameIndex = 0;
        }

        public void SetFacing(string facing)
        {
            Facing=facing=="n"||facing=="e"||facing=="w"?facing:"s";
            string folder=Facing switch{"n"=>"north","w"=>"west","e"=>"east",_=>"south"};
            for(int i=0;i<frames.Length;i++)
            {
                frames[i]=Resources.Load<Sprite>("ChestArtIndigo/"+folder+"/"+FrameNames[i]);
                if(frames[i]==null)throw new InvalidOperationException("Missing indigo chest frame: "+folder+"/"+FrameNames[i]);
            }
            if(visual==null)visual=GetComponent<SpriteRenderer>();
            visual.sprite=frames[FrameIndex];
        }

        public bool CanInteract(DungeonPlayer player)
        {
            if (visual == null || !isActiveAndEnabled || State == ChestState.Opening || RewardClaimed || player == null ||
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
            if (State == ChestState.Open) { RewardRequested?.Invoke(this, player); return true; }
            opener = player;
            State = ChestState.Opening;
            elapsed = 0;
            return true;
        }

        private void Update()
        {
            if (State != ChestState.Opening) return;
            elapsed += Time.deltaTime;
            FrameIndex = Mathf.Min(4, Mathf.FloorToInt(elapsed / FrameSeconds));
            visual.sprite = frames[FrameIndex];
            if (FrameIndex < 4) return;
            State = ChestState.Open;
            if (opener != null)
                Offer = new DungeonChestOffer(opener.Stats, new System.Random(Guid.NewGuid().GetHashCode()));
            Opened?.Invoke(this);
            if (opener != null) RewardRequested?.Invoke(this, opener);
        }
        internal void RestoreCheckpoint(DungeonPlayer player,DungeonChestOffer offer,bool claimed)
        {
            opener=player;Offer=offer;RewardClaimed=claimed;
            State=offer!=null||claimed?ChestState.Open:ChestState.Closed;
            FrameIndex=State==ChestState.Open?4:0;visual.sprite=frames[FrameIndex];
        }
        public bool ClaimReward(DungeonPlayer player)
        {
            if (player == null || player != opener || RewardClaimed || Offer == null || !Offer.Resolved) return false;
            RewardClaimed = true;
            player.ReceiveRelic(Offer.Result);
            return true;
        }
    }
}
