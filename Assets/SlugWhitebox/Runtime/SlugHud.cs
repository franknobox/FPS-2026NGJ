using UnityEngine;
using UnityEngine.UI;

namespace SlugWhitebox
{
    // The blockout needs only a resource counter, without menus or other HUD.
    public class SlugHud : MonoBehaviour
    {
        public SlugGame game;
        public Text counter;
        Font font;
        public void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},26);
            var canvas=new GameObject("声腺计数器",typeof(Canvas),typeof(CanvasScaler));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var label=new GameObject("当前声腺数量",typeof(RectTransform),typeof(Text));label.transform.SetParent(canvas.transform,false);
            counter=label.GetComponent<Text>();counter.font=font;counter.fontSize=26;
            counter.color=new Color(.08f,.08f,.08f);counter.alignment=TextAnchor.MiddleLeft;counter.raycastTarget=false;
            var rect=label.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(24,-18);rect.sizeDelta=new Vector2(320,45);Refresh();
        }
        public void Refresh() => counter.text="声腺："+Mathf.FloorToInt(game.glands)+" / "+Mathf.RoundToInt(game.maxGlands);
        void LateUpdate() {Refresh();}
        public void GlandFlash(bool gain) {Refresh();}
        void OnDestroy() {if(font)Destroy(font);}
    }
}
