using System;
using System.Collections.Generic;
using System.Linq;
namespace NGJ.Progression
{
    [Serializable] public sealed class UpgradeStack { public string id;public int count; }
    [Serializable] public sealed class WeaponGrowth
    {
        public WeaponFamily family;
        public int normalCredits,normalPurchases,specialPurchases;
        public readonly List<UpgradeStack> owned=new List<UpgradeStack>();
        internal string[] normalOffers,specialOffers;
        public int Count(string id)=>owned.Find(x=>x.id==id)?.count??0;
    }
    // 纯状态模型：抽选不随面板重开刷新；只有合法购买才扣点，特殊强化每项最多一次。
    public sealed class EvolutionState
    {
        public readonly ProgressionSettings settings;
        public readonly WeaponGrowth[] weapons;
        readonly Random random;
        public int Level{get;private set;}=1;
        public int Experience{get;private set;}
        public int TotalBiomass{get;private set;}
        public int Points{get;private set;}
        public int NextLevelCost=>(int)Math.Min(int.MaxValue,Math.Max(1,(long)settings.firstLevelBiomass+(long)(Level-1)*settings.biomassGrowthPerLevel));
        public event Action Changed;
        public EvolutionState(ProgressionSettings config,int seed)
        {
            settings=config??throw new ArgumentNullException(nameof(config));random=new Random(seed);
            weapons=Enum.GetValues(typeof(WeaponFamily)).Cast<WeaponFamily>().Select(x=>new WeaponGrowth{family=x}).ToArray();
        }
        public int AddBiomass(int amount)
        {
            if(amount<=0)return 0;
            TotalBiomass=(int)Math.Min(int.MaxValue,(long)TotalBiomass+amount);Experience=(int)Math.Min(int.MaxValue,(long)Experience+amount);
            int levels=0;
            while(Experience>=NextLevelCost){Experience-=NextLevelCost;Level++;Points+=Math.Max(1,settings.pointsPerLevel);levels++;}
            Changed?.Invoke();return levels;
        }
        public int PendingSpecial(WeaponFamily family)
        {
            var w=weapons[(int)family];int max=settings.options.Count(x=>x.special&&x.Supports(family));
            return Math.Max(0,Math.Min(max,w.normalCredits/Math.Max(1,settings.normalUpgradesPerSpecial))-w.specialPurchases);
        }
        public EvolutionOption[] Offers(WeaponFamily family,bool special)
        {
            var w=weapons[(int)family];
            if(special?PendingSpecial(family)<=0:Points<=0)return Array.Empty<EvolutionOption>();
            var ids=special?w.specialOffers:w.normalOffers;
            if(ids==null)
            {
                var pool=settings.options.Where(x=>x.special==special&&x.Supports(family)&&(!special||w.Count(x.id)==0)).ToList();
                int choices=!special&&family==WeaponFamily.Player?3:2;
                var chosen=new List<string>();while(chosen.Count<choices&&pool.Count>0){int n=random.Next(pool.Count);chosen.Add(pool[n].id);pool.RemoveAt(n);}
                ids=chosen.ToArray();if(special)w.specialOffers=ids;else w.normalOffers=ids;
            }
            return ids.Select(id=>settings.options.Single(x=>x.id==id)).ToArray();
        }
        public bool Buy(WeaponFamily family,bool special,string id)
        {
            var options=Offers(family,special);var option=Array.Find(options,x=>x.id==id);if(option==null)return false;
            var w=weapons[(int)family];
            if(special){if(PendingSpecial(family)<=0||w.Count(id)>0)return false;w.specialPurchases++;w.specialOffers=null;}
            else{if(Points<1)return false;Points--;w.normalPurchases++;w.normalCredits+=Math.Max(1,option.normalCredits);w.normalOffers=null;}
            var owned=w.owned.Find(x=>x.id==id);if(owned==null)w.owned.Add(new UpgradeStack{id=id,count=1});else owned.count++;
            Changed?.Invoke();return true;
        }
    }
}
