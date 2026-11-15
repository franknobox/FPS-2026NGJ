using System;
using System.Linq;
using ClientFlow;
using SlugWhitebox;
using UnityEngine;
namespace NGJ.Progression
{
    public static class EvolutionScreen
    {
        public static void Draw(ClientUI ui,SlugGame game,WeaponFamily selected,Action<WeaponFamily> select)
        {
            var growth=game.progression;var state=growth.State;ui.Clear();
            ui.Box(ui.root,"强化遮罩",Vector2.zero,new Vector2(4000,2400),new Color(.035f,.025f,.015f,.87f));
            var panel=ui.Frame(ui.root,"进化",Vector2.zero,new Vector2(1560,880));
            ui.Label(panel,"进化",new Vector2(-560,365),new Vector2(320,65),40,ClientUI.Cream,style:FontStyle.Bold);
            ui.Label(panel,$"Lv.{state.Level}    进化点 {state.Points}",new Vector2(400,365),new Vector2(610,60),30,ClientUI.Gold,TextAnchor.MiddleRight,FontStyle.Bold);
            WeaponFamily[] order={WeaponFamily.Player,WeaponFamily.Shotgun,WeaponFamily.Rifle,WeaponFamily.Sniper,WeaponFamily.Explosive};
            for(int i=0;i<order.Length;i++)
            {
                var family=order[i];var weapon=growth.Weapon(family);string name=family==WeaponFamily.Player?"玩家":weapon?weapon.displayName:family.ToString();
                int pending=state.PendingSpecial(family);if(pending>0)name+=" · 特殊 "+pending;
                ui.Button(panel,name,new Vector2(-560+i*280,275),new Vector2(260,60),()=>select(family),family==selected);
            }
            ui.Label(panel,growth.Attributes(selected),new Vector2(0,170),new Vector2(1370,105),21,ClientUI.Cream);
            var progress=state.weapons[(int)selected];int threshold=Mathf.Max(1,growth.Settings.normalUpgradesPerSpecial);
            int unlocked=state.PendingSpecial(selected);bool special=unlocked>0;
            bool complete=progress.specialPurchases>=growth.Settings.options.Count(x=>x.special&&x.Supports(selected));
            string heading=special?"特殊进化 · 免费选择":complete?"普通进化 · 1 进化点    特殊进化已集齐":$"普通进化 · 1 进化点    特殊进化计数 {progress.normalCredits%threshold}/{threshold}";
            ui.Label(panel,heading,new Vector2(0,82),new Vector2(1360,36),20,special?ClientUI.Gold:ClientUI.Muted);
            var offers=state.Offers(selected,special);
            if(offers.Length==0)
                ui.Label(panel,"进化点不足，继续战斗收集生物质。",new Vector2(0,-50),new Vector2(1300,150),27,ClientUI.Muted,TextAnchor.MiddleCenter);
            for(int i=0;i<offers.Length;i++)
            {
                var option=offers[i];float width=offers.Length==3?430:offers.Length==2?650:880;
                float x=(i-(offers.Length-1)*.5f)*(width+28);
                var card=ui.Frame(panel,option.title,new Vector2(x,-73),new Vector2(width,260));
                int layers=progress.Count(option.id);
                ui.Label(card,option.title+(layers>0?$" · {layers}层":""),new Vector2(0,77),new Vector2(width-48,44),30,ClientUI.Gold,TextAnchor.MiddleCenter,FontStyle.Bold);
                ui.Label(card,option.description,new Vector2(0,5),new Vector2(width-60,112),21,ClientUI.Cream,TextAnchor.MiddleCenter);
                ui.Button(card,special?"获得强化":"强化",new Vector2(0,-88),new Vector2(width-48,56),()=>state.Buy(selected,special,option.id),true);
            }
            string owned=string.Join("  ·  ",progress.owned.Select(x=>{
                var option=Array.Find(growth.Settings.options,o=>o.id==x.id);return (option?.title??x.id)+(x.count>1?$"×{x.count}":"");}));
            ui.Label(panel,owned.Length>0?"已获得："+owned:"尚未强化",new Vector2(0,-260),new Vector2(1370,72),18,ClientUI.Muted);
            ui.Label(panel,state.Points>0?"可保留进化点至下一次清场。":"",new Vector2(-390,-365),new Vector2(600,42),19,ClientUI.Muted);
            ui.Button(panel,"继续前进",new Vector2(490,-365),new Vector2(350,68),growth.Continue,true);
        }
    }
}
