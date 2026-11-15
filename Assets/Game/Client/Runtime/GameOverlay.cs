using UnityEngine;
using SlugWhitebox;
namespace ClientFlow
{
    public sealed class GameOverlay : MonoBehaviour
    {
        SlugGame game;ClientUI ui;SlugGame.Mode shown=(SlugGame.Mode)(-1);bool settings,confirm;
        NGJ.Progression.WeaponFamily selectedFamily=NGJ.Progression.WeaponFamily.Player;
        public bool CanQuickRestart=>!settings&&!confirm;
        public void Initialize(SlugGame owner){game=owner;ui=gameObject.AddComponent<ClientUI>();ui.Build();if(game.progression)game.progression.Changed+=RefreshEvolution;}
        void RefreshEvolution(){if(game && game.mode==SlugGame.Mode.Evolution)ShowEvolution();}
        void ShowEvolution(){NGJ.Progression.EvolutionScreen.Draw(ui,game,selectedFamily,family=>{selectedFamily=family;ShowEvolution();});}
        void LateUpdate()
        {
            if(!game||game.mode==shown)return;shown=game.mode;settings=confirm=false;
            if(shown==SlugGame.Mode.Paused)ShowPause();else if(shown==SlugGame.Mode.Result)ShowResult();else if(shown==SlugGame.Mode.Evolution)ShowEvolution();else ui.Clear();
        }
        void Shade(){ui.Clear();ui.Box(ui.root,"遮罩",Vector2.zero,new Vector2(4000,2400),new Color(.025f,.025f,.02f,.80f));}
        void ShowPause()
        {
            settings=false;Shade();var panel=ui.Frame(ui.root,"暂停",Vector2.zero,new Vector2(700,640));
            ui.Label(panel,"任务暂停",new Vector2(0,230),new Vector2(560,70),40,ClientUI.Cream,TextAnchor.MiddleCenter,FontStyle.Bold);
            ui.Button(panel,"继续任务",new Vector2(0,105),new Vector2(520,72),game.Resume,true);
            ui.Button(panel,"设置",new Vector2(0,10),new Vector2(520,66),ShowSettings);
            ui.Button(panel,"重新开始",new Vector2(0,-80),new Vector2(520,66),()=>Ask("重新开始本局？当前进度将丢失。",game.BeginRun));
            ui.Button(panel,"返回主菜单",new Vector2(0,-170),new Vector2(520,66),()=>Ask("返回主菜单？当前进度将丢失。",()=>ClientUI.Load(ClientUI.MenuScene)));
            ui.Label(panel,"ESC 继续",new Vector2(0,-282),new Vector2(450,40),18,ClientUI.Muted,TextAnchor.MiddleCenter);
        }
        void ShowResult()
        {
            settings=false;Shade();var panel=ui.Frame(ui.root,"任务结算",Vector2.zero,new Vector2(840,690));
            string title=game.resultTitle.Split('/')[0].Trim();ui.Label(panel,title,new Vector2(0,220),new Vector2(700,80),43,ClientUI.Cream,TextAnchor.MiddleCenter,FontStyle.Bold);
            int areas=game.levelFlow?game.levelFlow.AreaCount:3;
            int reached=Mathf.Clamp(game.sector+1,0,areas);
            ui.Label(panel,"到达区域",new Vector2(-245,120),new Vector2(230,45),22,ClientUI.Muted);ui.Label(panel,reached+" / "+areas,new Vector2(210,120),new Vector2(300,45),28,ClientUI.Cream,TextAnchor.MiddleRight,FontStyle.Bold);
            ui.Label(panel,"击杀数",new Vector2(-245,55),new Vector2(230,45),22,ClientUI.Muted);ui.Label(panel,game.kills.ToString(),new Vector2(210,55),new Vector2(300,45),28,ClientUI.Cream,TextAnchor.MiddleRight,FontStyle.Bold);
            int seconds=Mathf.FloorToInt(game.RunTime);ui.Label(panel,"用时",new Vector2(-245,-10),new Vector2(230,45),22,ClientUI.Muted);ui.Label(panel,string.Format("{0:00}:{1:00}",seconds/60,seconds%60),new Vector2(210,-10),new Vector2(300,45),28,ClientUI.Cream,TextAnchor.MiddleRight,FontStyle.Bold);
            if(game.progression)ui.Label(panel,game.progression.Summary(),new Vector2(0,-90),new Vector2(740,70),19,ClientUI.Gold,TextAnchor.MiddleCenter);
            ui.Button(panel,"再来一局",new Vector2(-170,-235),new Vector2(300,72),game.BeginRun,true);
            ui.Button(panel,"返回主菜单",new Vector2(170,-235),new Vector2(300,72),()=>ClientUI.Load(ClientUI.MenuScene));
        }
        void ShowSettings(){settings=true;Shade();ui.Settings(ui.root,()=>{settings=false;ShowPause();});}
        void Ask(string text,System.Action yes){confirm=true;ui.Confirm(text,()=>{confirm=false;yes();},()=>confirm=false);}
        public void RequestRestart(){game.Pause();shown=SlugGame.Mode.Paused;ShowPause();Ask("重新开始本局？当前进度将丢失。",game.BeginRun);}
        public bool HandleEscape()
        {
            if(game.mode==SlugGame.Mode.Evolution)return true;
            if(game.mode!=SlugGame.Mode.Paused)return game.mode==SlugGame.Mode.Result;
            if(confirm)return true;
            if(settings){settings=false;ShowPause();return true;}
            game.Resume();return true;
        }
        void OnDestroy(){if(game && game.progression)game.progression.Changed-=RefreshEvolution;}
    }
}
