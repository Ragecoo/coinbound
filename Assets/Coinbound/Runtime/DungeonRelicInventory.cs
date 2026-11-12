using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonRelicInventory
    {
        private readonly List<DungeonRelic> items=new List<DungeonRelic>();
        public IReadOnlyList<DungeonRelic> Items=>items.AsReadOnly();
        public PlayerStats CurrentStats {get;private set;}
        public float EnemyDamage {get;private set;}
        public float EnemySpeed {get;private set;}
        public float EnemyDefense {get;private set;}
        public const float AuraRadius=5;
        public void Clear(){items.Clear();CurrentStats=null;EnemyDamage=EnemySpeed=EnemyDefense=0;}
        public void Add(DungeonRelic relic,PlayerStats basis)
        {
            items.Add(relic);
            float hp=basis.MaxHealth,def=basis.Defense,dmg=basis.Damage,speed=basis.MoveSpeed,rate=basis.AttackRate,crit=basis.CriticalRate,luck=basis.Luck;
            EnemyDamage=EnemySpeed=EnemyDefense=0;
            foreach(var item in items)
            {
                hp+=item.Health;def+=item.Defense;dmg+=basis.Damage*item.Damage;speed+=basis.MoveSpeed*item.Speed;rate+=basis.AttackRate*item.Rate;crit+=item.Critical;luck+=item.Luck;
                EnemyDamage+=item.EnemyDamage;EnemySpeed+=item.EnemySpeed;EnemyDefense+=item.EnemyDefense;
            }
            EnemyDamage=Mathf.Clamp(EnemyDamage,-.40f,.60f);
            EnemySpeed=Mathf.Clamp(EnemySpeed,-.35f,.35f);
            EnemyDefense=Mathf.Clamp(EnemyDefense,-.40f,.35f);
            CurrentStats=new PlayerStats(Mathf.Clamp(hp,basis.MaxHealth*.4f,basis.MaxHealth*2.5f),basis.MaxMana,
                Mathf.Clamp(dmg,basis.Damage*.45f,basis.Damage*2.5f),Mathf.Clamp(speed,basis.MoveSpeed*.65f,basis.MoveSpeed*1.6f),
                Mathf.Clamp(rate,basis.AttackRate*.6f,basis.AttackRate*1.8f),Mathf.Clamp(crit,0,.6f),Mathf.Clamp(luck,-10,15),Mathf.Clamp(def,-5,8));
        }
    }
}
