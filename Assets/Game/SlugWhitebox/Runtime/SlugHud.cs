using UnityEngine;
using UnityEngine.UI;
namespace SlugWhitebox
{
    // 状态直接读取新战斗组件，避免界面显示旧资源。
    public class SlugHud : MonoBehaviour
    {
        public SlugGame game;public Text counter;
        Text center,bottom;Font font;
        public void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var canvas=new GameObject("战斗状态与操作提示",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            counter=Label(canvas.transform,new Vector2(0,1),new Vector2(24,-18),new Vector2(750,150),22,TextAnchor.UpperLeft);
            center=Label(canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(800,150),26,TextAnchor.MiddleCenter);
            bottom=Label(canvas.transform,new Vector2(0,0),new Vector2(24,100),new Vector2(1200,95),19,TextAnchor.UpperLeft);
        }
        Text Label(Transform parent,Vector2 anchor,Vector2 position,Vector2 size,int fontsize,TextAnchor align)
        {
            var go=new GameObject("状态文本",typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontsize;text.color=Color.white;text.alignment=align;text.raycastTarget=false;
            go.GetComponent<Outline>().effectColor=new Color(0,0,0,.85f);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;return text;
        }
        public void Refresh()
        {
            if(!counter || !game.stats)return;
            var g=game;var s=g.stats;
            counter.text=$"{(g.levelFlow?g.levelFlow.Title:"循声虫族 / 战斗白盒")}   区域 {Mathf.Max(0,g.sector+1)}/{(g.levelFlow?g.levelFlow.AreaCount:3)}\n噪值 {s.Noise:0}/{s.maxNoise:0}（满值过载）   声腺 {s.Glands:0.0}/{s.maxGlands:0}\n{g.WeaponDisplayName}   击杀 {g.kills} / 剩余 {g.Remaining}\n{g.SkillNames[g.skill]}  冷却 {g.runner.CooldownRemaining(g.glandSkills[g.skill]):0.0}s";
            center.text=g.mode==SlugGame.Mode.Result?g.resultTitle:g.mode==SlugGame.Mode.Paused?"已暂停 / Esc 继续":Time.time<g.hitUntil?"×":"+";
            center.color=Time.time<g.damageUntil?new Color(1,.2f,.2f):Color.white;
            bottom.text=(s.IsAbsorbing?$"共鸣吸收中：{s.AbsorbedNoise:0}\n":Time.time<g.noticeUntil?g.notice+"\n":g.levelFlow?g.levelFlow.ProgressText+"\n":"清场后推进至 EXIT\n")+
                "WASD 移动 · 鼠标瞄准 · 左键普攻 · 右键武器技能（噪值）\n1/2 / 滚轮切换两把出战武器 · Z/X/C 选声腺技能 · Q 释放（1声腺） · R 重开 · Esc 暂停";
        }
        void LateUpdate(){Refresh();}
        public void GlandFlash(bool gain){Refresh();}
        void OnDestroy(){if(font)Destroy(font);}
    }
}
