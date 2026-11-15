using UnityEngine;
using NGJ.Combat;
namespace ClientFlow
{
    public sealed class MainMenu : MonoBehaviour
    {
        public WeaponDefinition primary,secondary;
        ClientUI ui;
        void Start()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;ClientSettings.Apply();
            ui=gameObject.AddComponent<ClientUI>();ui.Build();ShowMain();
        }
        void Background(string section)
        {
            ui.Clear();ui.Box(ui.root,"底色",Vector2.zero,new Vector2(4000,2400),new Color(.10f,.105f,.095f));
            var right=ui.Box(ui.root,"荒野色块",new Vector2(500,0),new Vector2(920,1100),new Color(.57f,.40f,.20f));
            ui.Box(right,"远景山脊",new Vector2(95,-90),new Vector2(900,280),new Color(.42f,.31f,.20f)).localRotation=Quaternion.Euler(0,0,-12);
            ui.Box(right,"深色山脊",new Vector2(95,-310),new Vector2(1100,320),new Color(.16f,.17f,.15f)).localRotation=Quaternion.Euler(0,0,9);
            for(int i=0;i<6;i++)ui.Box(right,"信号线",new Vector2(-110+i*42,180),new Vector2(12,300+i*28),new Color(.09f,.09f,.075f,.65f));
            ui.Label(ui.root,"荒野前哨  /  OUTPOST 01",new Vector2(520,-450),new Vector2(740,45),21,ClientUI.Cream,TextAnchor.MiddleRight,FontStyle.Bold);
            ui.Label(ui.root,section,new Vector2(-720,475),new Vector2(440,40),18,ClientUI.Gold,style:FontStyle.Bold);
        }
        void ShowMain()
        {
            Background("循声虫族  /  主菜单");ui.Label(ui.root,"循声虫族",new Vector2(-610,210),new Vector2(570,120),72,ClientUI.Cream,style:FontStyle.Bold);
            ui.Label(ui.root,"FOLLOW THE SOUND",new Vector2(-610,137),new Vector2(570,50),24,ClientUI.Gold,style:FontStyle.Bold);
            ui.Box(ui.root,"标题下划线",new Vector2(-610,96),new Vector2(570,7),ClientUI.Ink);
            ui.Button(ui.root,"开始游戏",new Vector2(-610,10),new Vector2(570,76),ShowBriefing,true);
            ui.Button(ui.root,"设置",new Vector2(-610,-90),new Vector2(570,70),ShowSettings);
            ui.Button(ui.root,"退出游戏",new Vector2(-610,-180),new Vector2(570,70),()=>ui.Confirm("确定退出游戏？",Application.Quit,null));
            ui.Label(ui.root,"寻找声源，穿过荒野。",new Vector2(-610,-345),new Vector2(570,48),22,ClientUI.Muted);
        }
        void ShowBriefing()
        {
            Background("任务简报  /  荒野前哨");var frame=ui.Frame(ui.root,"任务简报",new Vector2(-330,0),new Vector2(1100,850));
            ui.Label(frame,"荒野前哨",new Vector2(-235,325),new Vector2(620,70),46,ClientUI.Cream,style:FontStyle.Bold);
            ui.Label(frame,"清除三个区域的敌人，抵达撤离点。",new Vector2(-185,250),new Vector2(720,54),24,ClientUI.Gold);
            ui.Label(frame,"出战武器",new Vector2(-255,145),new Vector2(580,44),22,ClientUI.Muted);
            ui.Label(frame,"01   "+(primary?primary.displayName:"未配置" )+"\n02   "+(secondary?secondary.displayName:"未配置"),new Vector2(-100,70),new Vector2(900,115),29,ClientUI.Cream,style:FontStyle.Bold);
            ui.Box(frame,"分割线",new Vector2(0,-20),new Vector2(960,4),ClientUI.Ink);
            ui.Label(frame,"WASD 移动   ·   鼠标瞄准   ·   左键攻击\n1–4 / 滚轮切换武器   ·   Q 释放声腺技能   ·   Esc 暂停",new Vector2(0,-105),new Vector2(950,110),22,ClientUI.Cream);
            ui.Button(frame,"返回",new Vector2(-250,-315),new Vector2(280,70),ShowMain);
            ui.Button(frame,"进入荒野",new Vector2(190,-315),new Vector2(460,76),()=>ClientUI.Load(ClientUI.LevelScene),true);
        }
        void ShowSettings(){Background("系统设置");ui.Settings(ui.root,ShowMain);}
    }
}
