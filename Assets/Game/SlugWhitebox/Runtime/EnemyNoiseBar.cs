using NGJ.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SlugWhitebox
{
    // 仅展示已受击敌人的真实噪值；位置和尺寸随敌人体型调整。
    public sealed class EnemyNoiseBar : MonoBehaviour
    {
        const float DisplaySeconds = 2f;
        const float BorderWidth = 4f;
        static readonly Color LowNoise = new Color(.78f, .65f, .43f);
        static readonly Color HighNoise = new Color(.38f, .035f, .025f);
        CombatTarget target;
        Camera view;
        Canvas canvas;
        Image fill;
        float height, radius, visibleUntil;

        public void Bind(CombatTarget combatTarget, Camera camera, float actorHeight, float actorRadius)
        {
            target = combatTarget;
            view = camera;
            height = Mathf.Max(.1f, actorHeight);
            radius = Mathf.Max(.01f, actorRadius);

            var root = new GameObject("敌人噪值条", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(24, 120);
            float scale = Mathf.Clamp(height * .9f, .65f, 1.6f) / 120f;
            rect.localScale = Vector3.one * scale;

            var background = MakeImage("底框", rect, 0);
            background.color = new Color(0, 0, 0, .2f);
            fill = MakeImage("噪值", rect, BorderWidth);
            // 四条实心边线保留中间的透明底框，不用实心黑矩形垫底。
            MakeBorder(rect, "左黑边", new Vector2(0, 0), new Vector2(0, 1), new Vector2(BorderWidth, 0));
            MakeBorder(rect, "右黑边", new Vector2(1, 0), new Vector2(1, 1), new Vector2(BorderWidth, 0));
            MakeBorder(rect, "下黑边", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, BorderWidth));
            MakeBorder(rect, "上黑边", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, BorderWidth));
            UpdateNoise(target.Noise, target.maxNoise);
            canvas.enabled = false;
            target.NoiseChanged += UpdateNoise;
            target.HitReceived += ShowOnHit;
            target.Died += Hide;
        }

        static Image MakeImage(string name, RectTransform parent, float inset)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
            var image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static void MakeBorder(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 size)
        {
            var image = MakeImage(name, parent, 0);
            image.color = Color.black;
            var rect = (RectTransform)image.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = min;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        void UpdateNoise(float value, float maximum)
        {
            float fraction = maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMax = new Vector2(1, fraction);
            fillRect.offsetMax = new Vector2(-BorderWidth, BorderWidth * (1 - 2 * fraction));
            fill.enabled = fraction > 0;
            fill.color = Color.Lerp(LowNoise, HighNoise, fraction);
        }

        void ShowOnHit(HitInfo hit)
        {
            if (!target.IsAlive) return;
            visibleUntil = Time.time + DisplaySeconds;
            canvas.enabled = true;
        }

        void Hide() { if (canvas) canvas.enabled = false; visibleUntil = 0; }

        void LateUpdate()
        {
            if (!canvas || !target || !view || !target.IsAlive || Time.time >= visibleUntil)
            {
                if (canvas) canvas.enabled = false;
                return;
            }
            canvas.enabled = true;
            canvas.transform.position = transform.position + Vector3.up * (height * .75f + .12f)
                + view.transform.right * (radius + .4f);
            canvas.transform.rotation = view.transform.rotation;
        }

        void OnDestroy()
        {
            if (!target) return;
            target.NoiseChanged -= UpdateNoise;
            target.HitReceived -= ShowOnHit;
            target.Died -= Hide;
        }
    }
}
