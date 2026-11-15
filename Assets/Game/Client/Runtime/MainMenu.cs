using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClientFlow
{
    public sealed class MainMenu : MonoBehaviour
    {
        
        public Sprite backgroundArt,titleArt;
        ClientUI ui;
        public void Build()
        {
            if(!ui){ui=gameObject.AddComponent<ClientUI>();ui.Build();}
            ShowMain();
        }
        void Start()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;ClientSettings.Apply();Build();
        }
        void Background(string section=null,bool showTitle=false)
        {
            ui.Clear();
            var bg=ui.Box(ui.root,"荒野前哨背景",Vector2.zero,new Vector2(1920,1080),Color.white);
            var image=bg.GetComponent<Image>();image.sprite=backgroundArt;image.raycastTarget=false;
            bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;
            if(backgroundArt){var fit=bg.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=backgroundArt.rect.width/backgroundArt.rect.height;}
            else image.color=new Color(.55f,.39f,.20f);
            if(showTitle && titleArt)
            {
                var logo=ui.Box(ui.root,"荒野前哨标题",Vector2.zero,new Vector2(740,420),Color.white);
                logo.anchorMin=logo.anchorMax=new Vector2(0,1);logo.anchoredPosition=new Vector2(410,-237);
                var art=logo.GetComponent<Image>();art.sprite=titleArt;art.preserveAspect=true;art.raycastTarget=false;
            }
            if(!showTitle)
            {
                var shade=ui.Box(ui.root,"内容遮罩",Vector2.zero,new Vector2(4000,2400),new Color(.08f,.045f,.02f,.68f));shade.GetComponent<Image>().raycastTarget=false;
            }
        }
        void ShowMain()
        {
            Background(showTitle:true);
            var stack=ui.Box(ui.root,"主菜单选项",Vector2.zero,new Vector2(416,340),Color.clear);
            stack.anchorMin=stack.anchorMax=new Vector2(0,1);stack.anchoredPosition=new Vector2(340,-640);stack.GetComponent<Image>().raycastTarget=false;
            MenuButton(stack,"开始游戏",132,()=>ClientUI.Load(ClientUI.LevelScene),true);
            MenuButton(stack,"设置",44,ShowSettings);
            MenuButton(stack,"操作说明",-44,ShowControls);
            MenuButton(stack,"退出游戏",-132,()=>ui.Confirm("确定退出游戏？",Application.Quit,null));
        }
        void MenuButton(Transform parent,string label,float y,Action action,bool primary=false)
        {
            var button=ui.Button(parent,label,new Vector2(0,y),new Vector2(416,72),action,primary);
            button.targetGraphic.color=primary?new Color(.96f,.69f,.27f):new Color(.20f,.12f,.065f,.96f);
            var colors=button.colors;colors.highlightedColor=new Color(1,.89f,.68f);colors.pressedColor=new Color(.72f,.52f,.28f);button.colors=colors;
            var text=button.GetComponentInChildren<Text>();text.fontSize=28;text.fontStyle=FontStyle.Bold;
            ui.Box(button.transform,"左侧色标",new Vector2(-190,0),new Vector2(5,34),primary?ClientUI.Ink:ClientUI.Gold).GetComponent<Image>().raycastTarget=false;
        }
        void ShowControls()
        {
            Background("操作说明");var frame=ui.Frame(ui.root,"操作说明",Vector2.zero,new Vector2(960,820));
            ui.Label(frame,"操作说明",new Vector2(0,310),new Vector2(800,65),42,ClientUI.Cream,TextAnchor.MiddleCenter,FontStyle.Bold);
            string[] keys={"W / A / S / D","鼠标","左键","1–4 / 滚轮","右键","E","Z / X / C","Q","Esc"};
            string[] actions={"移动","瞄准方向","普通攻击","切换武器","武器技能 / 狙击开镜","狙击武器技能","选择声腺技能","释放选中声腺技能","暂停"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=192-i*45;
                ui.Label(frame,keys[i],new Vector2(-265,y),new Vector2(260,38),23,ClientUI.Gold);
                ui.Label(frame,actions[i],new Vector2(140,y),new Vector2(480,38),23,ClientUI.Cream);
            }
            ui.Label(frame,"命中敌人才积累开火噪值，满值会过载。\n接近声腺掉落物可自动吸附拾取。",new Vector2(0,-225),new Vector2(800,72),20,ClientUI.Gold,TextAnchor.MiddleCenter);
            ui.Button(frame,"返回",new Vector2(0,-335),new Vector2(330,68),ShowMain,true);
        }
        void ShowSettings(){Background("系统设置");ui.Settings(ui.root,ShowMain);}
    }
}
