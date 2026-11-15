using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace ClientFlow
{
    // Shared, deliberately small UI kit; all elements are generated from reusable styles.
    public sealed class ClientUI : MonoBehaviour
    {
        public const string MenuScene="客户端菜单";
        public const string LevelScene="荒野前哨";
        public static readonly Color Ink=new Color(.035f,.032f,.025f,1), Panel=new Color(.12f,.085f,.055f,.97f), Gold=new Color(.95f,.64f,.20f), Cream=new Color(.93f,.88f,.73f), Muted=new Color(.62f,.61f,.55f);
        Font font; public RectTransform root;
        public void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var canvas=new GameObject("客户端界面",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=50;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            root=canvas.GetComponent<RectTransform>();
            if(!EventSystem.current){var es=new GameObject("客户端输入",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform,false);es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
        }
        public RectTransform Box(Transform parent,string name,Vector2 center,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=center;rt.sizeDelta=size;go.GetComponent<Image>().color=color;return rt;
        }
        public RectTransform Frame(Transform parent,string name,Vector2 center,Vector2 size)
        {
            var outer=Box(parent,name,center,size,Ink);Box(outer,"内底",Vector2.zero,size-new Vector2(10,10),Panel);
            Box(outer,"顶部色条",new Vector2(0,size.y*.5f-13),new Vector2(size.x-20,6),Gold);return outer;
        }
        public Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject("文字",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.fontStyle=style;text.color=color;text.alignment=alignment;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        public Button Button(Transform parent,string value,Vector2 pos,Vector2 size,Action action,bool primary=false)
        {
            var frame=Box(parent,value,pos,size,Ink);var fill=Box(frame,"按钮底",Vector2.zero,size-new Vector2(8,8),primary?Gold:new Color(.19f,.20f,.19f));
            var button=fill.gameObject.AddComponent<Button>();button.targetGraphic=fill.GetComponent<Image>();var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.84f,.57f);colors.pressedColor=new Color(.72f,.53f,.30f);button.colors=colors;
            Label(fill,value,Vector2.zero,size-new Vector2(24,8),24,primary?Ink:Cream,TextAnchor.MiddleCenter,FontStyle.Bold);button.onClick.AddListener(()=>action());return button;
        }
        public void Settings(Transform parent,Action back)
        {
            var panel=Frame(parent,"设置",Vector2.zero,new Vector2(760,620));Label(panel,"设置",new Vector2(0,250),new Vector2(660,60),36,Cream,TextAnchor.MiddleCenter,FontStyle.Bold);
            SliderRow(panel,"主音量",145,0,1,ClientSettings.Volume,ClientSettings.SetVolume,v=>Mathf.RoundToInt(v*100)+"%");
            SliderRow(panel,"鼠标灵敏度",45,.25f,2,ClientSettings.Sensitivity,ClientSettings.SetSensitivity,v=>v.ToString("0.00")+" 倍");
            var full=Button(panel,"",new Vector2(180,-60),new Vector2(250,58),()=>ClientSettings.SetFullscreen(!ClientSettings.Fullscreen));
            var text=Label(full.transform,"",Vector2.zero,new Vector2(240,56),22,Cream,TextAnchor.MiddleCenter);Action update=()=>text.text=ClientSettings.Fullscreen?"全屏：开启":"全屏：关闭";full.onClick.AddListener(()=>update());update();
            Label(panel,"显示模式",new Vector2(-195,-60),new Vector2(250,55),24,Cream);Button(panel,"返回",new Vector2(0,-230),new Vector2(260,66),back,true);
            if(ClientSettings.Resolutions().Length>0)
            {
                Label(panel,"分辨率",new Vector2(-195,-145),new Vector2(250,55),24,Cream);
                var resolution=Button(panel,"",new Vector2(180,-145),new Vector2(250,58),ClientSettings.NextResolution);
                var value=Label(resolution.transform,"",Vector2.zero,new Vector2(240,56),22,Cream,TextAnchor.MiddleCenter);
                Action refresh=()=>{var size=ClientSettings.Resolution;value.text=size.x+" × "+size.y;};resolution.onClick.AddListener(()=>refresh());refresh();
            }
        }
        void SliderRow(Transform parent,string title,float y,float min,float max,float value,Action<float> change,Func<float,string> format)
        {
            Label(parent,title,new Vector2(-195,y),new Vector2(250,50),24,Cream);
            var track=Box(parent,title+"滑条",new Vector2(130,y),new Vector2(320,18),Ink);var fill=Box(track,"填充",Vector2.zero,new Vector2(310,10),Gold);fill.anchorMin=new Vector2(0,.5f);fill.anchorMax=new Vector2(1,.5f);fill.offsetMin=new Vector2(5,-5);fill.offsetMax=new Vector2(-5,5);
            var handle=Box(track,"滑块",Vector2.zero,new Vector2(22,36),Cream);var slider=track.gameObject.AddComponent<Slider>();slider.targetGraphic=handle.GetComponent<Image>();slider.fillRect=fill;slider.handleRect=handle;slider.minValue=min;slider.maxValue=max;slider.SetValueWithoutNotify(value);
            var number=Label(parent,format(value),new Vector2(325,y),new Vector2(95,48),20,Gold,TextAnchor.MiddleRight);slider.onValueChanged.AddListener(v=>{change(v);number.text=format(v);});
        }
        public void Clear(){for(int i=root.childCount-1;i>=0;i--){var child=root.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}}
        public void Confirm(string prompt,Action yes,Action no)
        {
            var shade=Box(root,"确认遮罩",Vector2.zero,new Vector2(4000,2400),new Color(0,0,0,.75f));var frame=Frame(shade,"确认",Vector2.zero,new Vector2(720,300));
            Label(frame,prompt,new Vector2(0,55),new Vector2(620,90),27,Cream,TextAnchor.MiddleCenter);Button(frame,"取消",new Vector2(-150,-75),new Vector2(220,60),()=>{Remove(shade.gameObject);no?.Invoke();});Button(frame,"确认",new Vector2(150,-75),new Vector2(220,60),()=>{Remove(shade.gameObject);yes();},true);
        }
        static void Remove(GameObject go){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        public static void Load(string scene){Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;SceneManager.LoadScene(scene);}
        void OnDestroy(){if(font){if(Application.isPlaying)Destroy(font);else DestroyImmediate(font);}}
    }
}
