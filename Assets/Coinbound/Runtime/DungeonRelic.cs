using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonRelic
    {
        public readonly string Id, Name, Subtitle;
        public readonly int Tier;
        public readonly bool Positive;
        public readonly float Health, Defense, Damage, Speed, Rate, Critical, Luck, EnemyDamage, EnemySpeed, EnemyDefense;
        public Texture2D HudIcon => Resources.Load<Texture2D>("InterfaceArt/ItemsNoBg/" + Id) ?? Icon;
        public Texture2D Icon => Resources.Load<Texture2D>("InterfaceArt/Items/" + Id);
        public DungeonRelic(string id, string name, string subtitle, int tier, bool positive,
            float hp=0, float defense=0, float damage=0, float speed=0, float rate=0, float critical=0, float luck=0,
            float enemyDamage=0, float enemySpeed=0, float enemyDefense=0)
        {
            Id=id; Name=name; Subtitle=subtitle; Tier=tier; Positive=positive;
            Health=hp; Defense=defense; Damage=damage; Speed=speed; Rate=rate; Critical=critical; Luck=luck;
            EnemyDamage=enemyDamage; EnemySpeed=enemySpeed; EnemyDefense=enemyDefense;
        }
        public string Effects
        {
            get
            {
                var lines = new List<string>();
                void Add(float value, string label, bool percentage=false, bool relative=false)
                {
                    if(value==0)return;
                    lines.Add((value>0?"+":"−")+Math.Abs(value*(percentage||relative?100:1)).ToString("0.##",CultureInfo.InvariantCulture)
                        +(percentage?" п.п.":relative?"%":"")+" "+label);
                }
                Add(Health,"макс. здоровья"); Add(Defense,"защиты"); Add(Damage,"урона",relative:true);
                Add(Speed,"скорости",relative:true); Add(Rate,"темпа атак",relative:true); Add(Critical,"шанса крита",true); Add(Luck,"удачи");
                Add(EnemyDamage,"урона врагов рядом",relative:true); Add(EnemySpeed,"скорости врагов рядом",relative:true); Add(EnemyDefense,"защиты врагов рядом",relative:true);
                if(EnemyDamage!=0||EnemySpeed!=0||EnemyDefense!=0)lines.Add("Радиус ауры: 5 клеток");
                return string.Join("\n",lines);
            }
        }
    }

    public static class DungeonRelics
    {
        public static readonly IReadOnlyList<DungeonRelic> All = Array.AsReadOnly(new[] {
            new DungeonRelic("01_amber_heart","Янтарное сердце","Жизнь, сохранённая в смоле.",1,true,hp:15),
            new DungeonRelic("02_saints_tooth","Зуб святого","Защита павших.",1,true,defense:1),
            new DungeonRelic("03_execution_ash","Пепел казни","Ярость умирающего.",1,true,damage:.10f),
            new DungeonRelic("04_night_owl_feather","Перо ночной совы","Лёгкость и тень.",1,true,speed:.08f),
            new DungeonRelic("05_hangmans_knot","Узел палача","Ускорение ударов.",1,true,rate:.10f),
            new DungeonRelic("06_martyrs_eye","Око мученика","Зрение сквозь боль.",1,true,critical:.14f),
            new DungeonRelic("07_blind_kings_coin","Монета слепого короля","Удача без судьбы.",1,true,luck:3),
            new DungeonRelic("08_wax_doll","Восковая кукла","Жизнь в обмен на подвижность.",1,true,hp:20,speed:-.04f),
            new DungeonRelic("09_black_bile","Чёрная желчь","Сила ценой скорости.",1,true,damage:.18f,rate:-.05f),
            new DungeonRelic("10_stone_tomb_seal","Печать каменной гробницы","Стойкость ценой подвижности.",1,true,defense:2,speed:-.04f),
            new DungeonRelic("11_dead_mans_hourglass","Песочные часы мертвеца","Замедление врагов.",1,true,enemySpeed:-.14f),
            new DungeonRelic("12_tongueless_bell","Безъязыкий колокол","Ослабление врагов.",1,true,enemyDamage:-.10f),
            new DungeonRelic("13_ark_of_the_unborn","Ковчег нерождённого","Дар забытой жизни.",2,true,hp:24,defense:1,speed:-.04f),
            new DungeonRelic("14_amber_beast_fang","Клык янтарного зверя","Сокрушительная сила.",2,true,damage:.28f,rate:-.06f),
            new DungeonRelic("15_fever_rosary","Чётки лихорадки","Стремительность и точность.",2,true,hp:-12,rate:.20f,critical:.12f),
            new DungeonRelic("16_fugitive_dead_brand","Клеймо беглого мертвеца","Удача ценой защиты.",2,true,speed:.12f,luck:4,defense:-1),
            new DungeonRelic("17_golden_thread_idol","Идол золотой нити","Благословение судьбы.",2,true,luck:4,critical:.16f),
            new DungeonRelic("18_bone_dust_vessel","Сосуд костяной пыли","Ослабление вражеской защиты.",2,true,enemyDefense:-.25f),
            new DungeonRelic("19_stone_giants_tear","Слеза каменного великана","Непреклонная защита.",2,true,defense:3,hp:16,rate:-.08f),
            new DungeonRelic("20_ancient_gods_heart","Сердце древнего бога","Мощь погребённого божества.",3,true,hp:40,defense:3,speed:-.06f),
            new DungeonRelic("21_eye_of_eternal_night","Око вечной ночи","Разрушительная сила и меткость.",3,true,damage:.45f,critical:.20f,hp:-15,rate:-.06f),
            new DungeonRelic("22_empty_cradle","Пустая колыбель","Угасание жизненных сил.",1,false,hp:-10),
            new DungeonRelic("23_seal_of_rot","Печать гниения","Ослабление защиты.",1,false,defense:-1),
            new DungeonRelic("24_grey_ash","Серый прах","Угасание силы.",1,false,damage:-.08f),
            new DungeonRelic("25_lead_anchor","Свинцовый якорь","Тяжесть утопленника.",1,false,speed:-.04f),
            new DungeonRelic("26_dead_sinew_knot","Узел мёртвых жил","Скованность движений.",1,false,rate:-.08f),
            new DungeonRelic("27_blind_mirror_shard","Осколок слепого зеркала","Утрата точности.",2,false,critical:-.08f,damage:-.06f),
            new DungeonRelic("28_debtors_coin","Монета должника","Проклятая удача.",1,false,luck:-2),
            new DungeonRelic("29_bloody_wick","Кровавый фитиль","Усиление ближайших врагов.",2,false,enemyDamage:.15f),
            new DungeonRelic("30_fading_clock","Часы угасания","Истощение тела.",2,false,damage:-.12f,rate:-.08f),
            new DungeonRelic("31_second_death_shroud","Саван второй смерти","Утрата жизненных сил.",3,false,hp:-25,defense:-2,speed:-.06f),
            new DungeonRelic("32_abyss_icon","Икона бездны","Усиление ближайших врагов.",3,false,enemyDamage:.20f,enemyDefense:.15f)
        });

        public static int RollTier(float luck, double roll)
        {
            float three=Mathf.Clamp(.05f+Mathf.Clamp(luck,-10,15)*.005f,.01f,.125f);
            float two=Mathf.Clamp(.25f+Mathf.Clamp(luck,-10,15)*.01f,.15f,.40f);
            return roll<three?3:roll<three+two?2:1;
        }
    }

    public sealed class DungeonChestOffer
    {
        public readonly DungeonRelic Good, Bad;
        private readonly bool star;
        internal bool Star => star;
        internal DungeonChestOffer(DungeonRelic good,DungeonRelic bad,bool star,bool resolved){Good=good;Bad=bad;this.star=star;Resolved=resolved;}
        public bool Resolved { get; private set; }
        public DungeonRelic Result => Resolved ? (star?Good:Bad) : null;
        public DungeonChestOffer(PlayerStats stats, System.Random random)
        {
            int tier=DungeonRelics.RollTier(stats.Luck,random.NextDouble());
            var good=new List<DungeonRelic>();var bad=new List<DungeonRelic>();
            foreach(var item in DungeonRelics.All)if(item.Tier==tier)(item.Positive?good:bad).Add(item);
            Good=good[random.Next(good.Count)];Bad=bad[random.Next(bad.Count)];
            star=random.NextDouble()<stats.GoodDropChance(); // One luck point adds one percentage point to the star chance.
        }
        public DungeonRelic Resolve(){Resolved=true;return Result;}
    }
}
