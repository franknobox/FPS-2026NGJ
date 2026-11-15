using NGJ.Combat;
using UnityEngine;
using UnityEngine.UI;
namespace SlugWhitebox
{
    // 数据直接读取统一战斗组件；HUD 不维护自己的资源和技能计时。
    public sealed class SlugHud : MonoBehaviour
    {
        static readonly Color Ink=ClientFlow.ClientUI.Ink, Gold=ClientFlow.ClientUI.Gold, Cream=ClientFlow.ClientUI.Cream, Muted=ClientFlow.ClientUI.Muted;
        static readonly Color Panel=new Color(.13f,.12f,.095f,.94f), Red=new Color(.55f,.065f,.035f);
        public SlugGame game; public Text counter;
        Font font;Text crosshair,notice,noiseValue,glandValue,resourceHint;
        Image noiseFill;RectTransform noiseCost,selectionArrow;
        Text evolutionLabel;Image experienceFill;
        SniperScope scope;
        readonly HudShape[] weaponShapes=new HudShape[4],skillShapes=new HudShape[3];
        readonly Text[] weaponValues=new Text[4],weaponNames=new Text[4],weaponKeys=new Text[4],skillValues=new Text[3],skillNames=new Text[3];
        readonly Vector2[] skillPositions={new Vector2(-23,40),new Vector2(30,0),new Vector2(-23,-40)};
        public void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var canvas=new GameObject("战斗HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var scopeRect=Node(canvas.transform,"狙击瞄准镜",new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            scopeRect.anchorMin=Vector2.zero;scopeRect.anchorMax=Vector2.one;scopeRect.offsetMin=scopeRect.offsetMax=Vector2.zero;
            scope=scopeRect.gameObject.AddComponent<SniperScope>();scope.raycastTarget=false;scope.gameObject.SetActive(false);
            var map=Node(canvas.transform,"小地图",new Vector2(0,1),new Vector2(114,-110),new Vector2(180,180));
            map.gameObject.AddComponent<UIMinimap>().Bind(game.player,game.view);
            counter=Label(canvas.transform,"区域与敌情",new Vector2(0,1),new Vector2(529,-42),new Vector2(610,44),20,Cream,TextAnchor.MiddleLeft);
            evolutionLabel=Label(canvas.transform,"成长进度",new Vector2(0,1),new Vector2(529,-75),new Vector2(610,26),16,Gold,TextAnchor.MiddleLeft);
            var experience=Node(canvas.transform,"生物质进度",new Vector2(0,1),new Vector2(434,-97),new Vector2(420,8));
            Box(experience,"经验底框",Vector2.zero,new Vector2(420,8),Ink);
            experienceFill=Box(experience,"经验填充",new Vector2(-208,0),new Vector2(416,4),Gold);((RectTransform)experienceFill.transform).pivot=new Vector2(0,.5f);
            notice=Label(canvas.transform,"关卡提示",new Vector2(1,1),new Vector2(-325,-45),new Vector2(600,58),19,Cream);
            crosshair=Label(canvas.transform,"准星",new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,100),26,Cream);
            var dock=Node(canvas.transform,"底部战斗信息",new Vector2(.5f,0),new Vector2(0,118),new Vector2(690,180));
            var meter=Frame(dock,"噪值槽",new Vector2(-285,0),new Vector2(38,138),new Color(.08f,.07f,.05f,.65f));
            noiseFill=Box(meter,"噪值填充",Vector2.zero,new Vector2(26,126),Gold);
            var fr=(RectTransform)noiseFill.transform;fr.pivot=new Vector2(.5f,0);fr.anchoredPosition=new Vector2(0,-63);
            for(int i=1;i<10;i++)Box(meter,"计量刻度",new Vector2(0,-63+i*12.6f),new Vector2(26,2),Ink);
            noiseCost=(RectTransform)Box(meter,"当前技能消耗线",Vector2.zero,new Vector2(46,3),Cream).transform;
            Label(dock,"噪值标题",new Vector2(.5f,.5f),new Vector2(-285,84),new Vector2(82,26),17,Gold).text="噪值";
            noiseValue=Label(dock,"噪值数值",new Vector2(.5f,.5f),new Vector2(-285,-85),new Vector2(94,26),16,Cream);
            Label(dock,"武器技能标题",new Vector2(.5f,.5f),new Vector2(-38,84),new Vector2(415,26),16,Muted).text="武器技能 / 右键 · 狙击为 E";
            for(int i=0;i<4;i++)
            {
                var pos=new Vector2(-188+i*100,19);
                weaponShapes[i]=Shape(dock,"武器技能槽 "+(i+1),pos,new Vector2(94,82),6,Panel);
                weaponValues[i]=Label(weaponShapes[i].transform,"冷却与状态",new Vector2(.5f,.5f),Vector2.zero,new Vector2(80,36),22,Cream);
                weaponKeys[i]=Label(dock,"武器切换键",new Vector2(.5f,.5f),pos+new Vector2(0,48),new Vector2(70,22),14,Muted);weaponKeys[i].text=(i+1).ToString();
                weaponNames[i]=Label(dock,"武器名",new Vector2(.5f,.5f),pos+new Vector2(0,-51),new Vector2(98,28),16,Cream);
            }
            var glands=Frame(dock,"声腺底框",new Vector2(-38,-65),new Vector2(410,45),Panel);
            glandValue=Label(glands,"声腺数量",new Vector2(.5f,.5f),new Vector2(-110,0),new Vector2(164,35),23,Gold,TextAnchor.MiddleLeft);
            resourceHint=Label(glands,"技能消耗",new Vector2(.5f,.5f),new Vector2(88,0),new Vector2(206,35),15,Cream,TextAnchor.MiddleRight);
            var wheel=Shape(dock,"声腺技能轮盘",new Vector2(238,0),new Vector2(154,154),48,Panel);
            for(int i=0;i<3;i++)
            {
                skillShapes[i]=Shape(wheel.transform,"声腺技能 "+i,skillPositions[i],new Vector2(52,52),40,Panel);
                skillValues[i]=Label(skillShapes[i].transform,"状态",new Vector2(.5f,.5f),new Vector2(0,10),new Vector2(52,24),17,Cream);
                skillNames[i]=Label(skillShapes[i].transform,"技能名称",new Vector2(.5f,.5f),new Vector2(0,-12),new Vector2(56,22),14,Cream);
            }
            selectionArrow=(RectTransform)Shape(wheel.transform,"当前技能箭头",new Vector2(-54,0),new Vector2(22,22),3,Gold).transform;
            Label(dock,"轮盘按键提示",new Vector2(.5f,.5f),new Vector2(238,-91),new Vector2(210,26),14,Muted).text="Z / X / C · Q 释放";
            Refresh();
        }
        static RectTransform Node(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;
        }
        static Image Box(Transform parent,string name,Vector2 pos,Vector2 size,Color tint)
        {
            var rt=Node(parent,name,new Vector2(.5f,.5f),pos,size);var image=rt.gameObject.AddComponent<Image>();image.color=tint;image.raycastTarget=false;return image;
        }
        static RectTransform Frame(Transform parent,string name,Vector2 pos,Vector2 size,Color tint)
        {
            var rt=(RectTransform)Box(parent,name,pos,size,Ink).transform;Box(rt,"内底",Vector2.zero,size-Vector2.one*10,tint);return rt;
        }
        static HudShape Shape(Transform parent,string name,Vector2 pos,Vector2 size,int sides,Color tint)
        {
            var rt=Node(parent,name,new Vector2(.5f,.5f),pos,size);var shape=rt.gameObject.AddComponent<HudShape>();shape.sides=sides;shape.border=4;shape.color=tint;shape.raycastTarget=false;shape.SetVerticesDirty();return shape;
        }
        Text Label(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color tint,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var rt=Node(parent,name,anchor,pos,size);var label=rt.gameObject.AddComponent<Text>();label.font=font;label.fontSize=fontSize;label.color=tint;label.alignment=align;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
            var outline=rt.gameObject.AddComponent<Outline>();outline.effectColor=Ink;outline.effectDistance=new Vector2(1,-1);return label;
        }
        public void Refresh()
        {
            if(!counter || !game || !game.stats)return;
            var g=game;var stats=g.stats;
            counter.text=$"区域 {Mathf.Max(0,g.sector+1):00} / {(g.levelFlow?g.levelFlow.AreaCount:3):00}    剩余 {g.Remaining}";
            var growth=g.progression?g.progression.State:null;
            evolutionLabel.text=growth!=null?$"Lv.{growth.Level}   生物质 {growth.Experience}/{growth.NextLevelCost}   进化点 {growth.Points}":"";
            ((RectTransform)experienceFill.transform).sizeDelta=new Vector2(growth!=null?416*Mathf.Clamp01((float)growth.Experience/growth.NextLevelCost):0,4);
            notice.text=stats.IsAbsorbing?$"共鸣吸收中 · {stats.AbsorbedNoise:0}":Time.time<g.noticeUntil?g.notice:g.levelFlow?g.levelFlow.ProgressText:"清场后推进至 EXIT";
            crosshair.text=g.Running?(Time.time<g.hitUntil?"×":"+"):g.levelFlow?"":g.mode==SlugGame.Mode.Result?g.resultTitle:g.mode==SlugGame.Mode.Paused?"已暂停 / Esc 继续":"";
            crosshair.color=Time.time<g.damageUntil?Red:Cream;
            scope.gameObject.SetActive(g.IsSniperAiming);crosshair.gameObject.SetActive(!g.IsSniperAiming);
            float noise=stats.maxNoise>0?Mathf.Clamp01(stats.Noise/stats.maxNoise):0;
            ((RectTransform)noiseFill.transform).sizeDelta=new Vector2(26,126*noise);noiseFill.enabled=noise>0;noiseFill.color=Color.Lerp(new Color(.78f,.65f,.43f),Red,noise);
            noiseValue.text=$"{stats.Noise:0}/{stats.maxNoise:0}";glandValue.text=$"声腺 {stats.Glands:0.0} / {stats.maxGlands:0}";
            var equipped=g.weapons?g.weapons.equipped:null;var weaponSkill=equipped?equipped.skill:null;
            noiseCost.gameObject.SetActive(weaponSkill && weaponSkill.resource==ResourceType.Noise && weaponSkill.cost<=stats.maxNoise);
            if(weaponSkill && stats.maxNoise>0)noiseCost.anchoredPosition=new Vector2(0,-63+126*Mathf.Clamp01(weaponSkill.cost/stats.maxNoise));
            resourceHint.text=weaponSkill?$"右键消耗 {weaponSkill.cost:0.#} {(weaponSkill.resource==ResourceType.Noise?"噪值":"声腺")}":"当前武器无技能";
            if(g.IsSniperEquipped)resourceHint.text=weaponSkill?$"右键瞄准 · E 技能 {weaponSkill.cost:0} 噪值":"按住右键瞄准";
            for(int i=0;i<4;i++)
            {
                int index=g.weaponLoadout!=null && i<g.weaponLoadout.Length?g.weaponLoadout[i]:-1;
                var definition=index>=0 && g.weaponDefinitions!=null && index<g.weaponDefinitions.Length?g.weaponDefinitions[index]:null;
                weaponShapes[i].gameObject.SetActive(definition);weaponNames[i].gameObject.SetActive(definition);weaponKeys[i].gameObject.SetActive(definition);if(!definition)continue;
                bool active=index==g.weapon;weaponShapes[i].color=active?Gold:Panel;weaponValues[i].color=active?Ink:Cream;
                weaponValues[i].GetComponent<Outline>().enabled=!active;
                weaponNames[i].text=definition.displayName;weaponNames[i].color=active?Gold:Muted;weaponKeys[i].color=active?Gold:Muted;weaponValues[i].text=SkillStatus(definition.skill);
            }
            for(int i=0;i<3;i++)
            {
                var ability=g.glandSkills!=null && i<g.glandSkills.Length?g.glandSkills[i]:null;
                bool selected=i==g.skill,ready=ability && g.runner && g.runner.CanCast(ability);
                skillShapes[i].color=selected?Gold:Panel;skillNames[i].color=selected?Ink:Muted;skillValues[i].color=selected?Ink:ready?Cream:Muted;
                skillNames[i].GetComponent<Outline>().enabled=!selected;skillValues[i].GetComponent<Outline>().enabled=!selected;
                skillNames[i].text=ability?ability.displayName:"未配置";skillValues[i].text=SkillStatus(ability);
            }
            var point=skillPositions[Mathf.Clamp(g.skill,0,2)];selectionArrow.anchoredPosition=point-point.normalized*39;selectionArrow.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(point.y,point.x)*Mathf.Rad2Deg);
        }
        string SkillStatus(SkillDefinition ability)
        {
            if(!ability)return "无技能";if(!game.runner)return "未就绪";
            float remaining=game.runner.CooldownRemaining(ability);if(remaining>0)return Mathf.CeilToInt(remaining).ToString();
            if(game.stats.IsAbsorbing)return "共鸣中";return game.runner.CanCast(ability)?"就绪":"不足";
        }
        void LateUpdate(){Refresh();}
        public void GlandFlash(bool gain){Refresh();}
        void OnDestroy(){if(font)Destroy(font);}
    }
}
