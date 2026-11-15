using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlugWhitebox
{
    /// <summary>固定地图方向的小地图占位。角色始终居中，人物箭头与镜头视野分别读取世界朝向。</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10010)]
    [AddComponentMenu("循声虫族/UI/Minimap")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIMinimap : MaskableGraphic
    {
        [Header("跟随来源")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera viewCamera;
        [Header("圆形地图")]
        [SerializeField, Range(0f, 1f)] private float gridOpacity = 0.75f;
        [SerializeField, Range(4, 24)] private int gridCells = 12;
        [SerializeField, Range(0.5f, 3f)] private float gridLineWidth = 0.8f;
        [SerializeField] private Color borderColor = new Color(.035f, .03f, .025f, 1f);
        [SerializeField, Min(0.5f)] private float borderWidth = 4f;
        [SerializeField] private Color innerShadowColor = new Color(0f, 0f, 0f, 0.3f);
        [SerializeField, Min(0f)] private float innerShadowWidth = 7f;
        [Header("人物箭头")]
        [SerializeField] private Color arrowColor = new Color(1f, .76f, .28f, 1f);
        [SerializeField] private Color arrowOutlineColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField, Min(4f)] private float arrowSize = 23f;
        [SerializeField, Min(0f)] private float arrowOutlineWidth = 1.6f;
        [SerializeField] private Color arrowGlowColor = new Color32(244, 164, 44, 150);
        [SerializeField, Range(0f, 16f)] private float arrowGlowWidth = 7f;
        [Header("镜头视野")]
        [SerializeField] private bool useCameraFieldOfView = true;
        [SerializeField, Range(10f, 160f)] private float viewConeAngle = 80f;
        [SerializeField] private Color viewConeColor = new Color(1f, .72f, .32f, 0.30f);

        private const int CircleSegments = 96;
        private static readonly Vector2[] ArrowShape =
        {
            new Vector2(0f, 0.6f), new Vector2(-0.38f, -0.4f),
            new Vector2(0f, -0.15f), new Vector2(0.38f, -0.4f)
        };
        private readonly Vector2[] _circle = new Vector2[CircleSegments];
        private List<Vector2> _clip = new List<Vector2>(12);
        private List<Vector2> _scratch = new List<Vector2>(12);
        private float _playerHeading;
        private float _viewHeading;
        private float _coneAngle = 80f;

        public Transform Player => player;
        public Camera ViewCamera => viewCamera;
        public float PlayerHeadingDegrees => _playerHeading;
        public float ViewHeadingDegrees => _viewHeading;
        public float ViewConeDegrees => _coneAngle;
        public float GridOpacity => gridOpacity;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            RefreshNow();
        }

        public void Bind(Transform character, Camera camera)
        {
            player = character;
            viewCamera = camera;
            RefreshNow();
            SetVerticesDirty();
        }

        private void LateUpdate() => RefreshNow();

        public void RefreshNow()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            float characterHeading = Heading(player != null ? player.forward : Vector3.forward, _playerHeading);
            float cameraHeading = Heading(viewCamera != null ? viewCamera.transform.forward : Vector3.forward, _viewHeading);
            float cone = viewConeAngle;
            if (useCameraFieldOfView && viewCamera != null && !viewCamera.orthographic)
            {
                cone = 2f * Mathf.Atan(Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f)
                    * viewCamera.aspect) * Mathf.Rad2Deg;
            }
            cone = Mathf.Clamp(cone, 10f, 160f);
            if (Mathf.Abs(Mathf.DeltaAngle(characterHeading, _playerHeading)) < 0.02f
                && Mathf.Abs(Mathf.DeltaAngle(cameraHeading, _viewHeading)) < 0.02f
                && Mathf.Abs(cone - _coneAngle) < 0.02f) return;
            _playerHeading = characterHeading;
            _viewHeading = cameraHeading;
            _coneAngle = cone;
            SetVerticesDirty();
        }

        private static float Heading(Vector3 direction, float fallback)
        {
            return direction.x * direction.x + direction.z * direction.z > 0.0001f
                ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : fallback;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.5f - borderWidth);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                _circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            DrawGrid(vh, center, radius);
            DrawViewCone(vh, center, radius);
            DrawRing(vh, center, radius, Mathf.Max(0f, radius - innerShadowWidth),
                innerShadowColor, new Color(innerShadowColor.r, innerShadowColor.g, innerShadowColor.b, 0f));
            DrawRing(vh, center, radius + borderWidth, radius, borderColor, borderColor);
            DrawArrowGlow(vh, center, arrowSize + arrowOutlineWidth * 2f);
            DrawArrow(vh, center, arrowSize + arrowOutlineWidth * 2f, arrowOutlineColor);
            DrawArrow(vh, center, arrowSize, arrowColor);
        }

        private void DrawGrid(VertexHelper vh, Vector2 center, float radius)
        {
            float step = radius * 2f / gridCells;
            float halfLine = Mathf.Min(gridLineWidth, step * 0.25f) * 0.5f;
            Color white = new Color(.51f, .39f, .24f, gridOpacity);
            Color black = new Color(.10f, .09f, .07f, gridOpacity);
            // White cells and black strips tile without overlapping, preserving 75% opacity at crossings.
            for (int x = 0; x < gridCells; x++)
            {
                float left = -radius + x * step + (x > 0 ? halfLine : 0f);
                float right = -radius + (x + 1) * step - (x + 1 < gridCells ? halfLine : 0f);
                for (int y = 0; y < gridCells; y++)
                {
                    float bottom = -radius + y * step + (y > 0 ? halfLine : 0f);
                    float top = -radius + (y + 1) * step - (y + 1 < gridCells ? halfLine : 0f);
                    DrawClippedRect(vh, center, radius, left, bottom, right, top, white);
                    if (y > 0)
                    {
                        float lineY = -radius + y * step;
                        DrawClippedRect(vh, center, radius, left, lineY - halfLine, right, lineY + halfLine, black);
                    }
                }
                if (x > 0)
                {
                    float lineX = -radius + x * step;
                    DrawClippedRect(vh, center, radius, lineX - halfLine, -radius, lineX + halfLine, radius, black);
                }
            }
        }

        private void DrawClippedRect(VertexHelper vh, Vector2 center, float radius,
            float left, float bottom, float right, float top, Color tint)
        {
            Vector2 closest = new Vector2(Mathf.Clamp(0f, left, right), Mathf.Clamp(0f, bottom, top));
            if (closest.sqrMagnitude >= radius * radius) return;
            _clip.Clear();
            _clip.Add(new Vector2(left, bottom));
            _clip.Add(new Vector2(right, bottom));
            _clip.Add(new Vector2(right, top));
            _clip.Add(new Vector2(left, top));
            float farX = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right));
            float farY = Mathf.Max(Mathf.Abs(bottom), Mathf.Abs(top));
            if (farX * farX + farY * farY > radius * radius)
            {
                for (int edge = 0; edge < CircleSegments && _clip.Count > 0; edge++)
                {
                    Vector2 a = _circle[edge], b = _circle[(edge + 1) % CircleSegments];
                    _scratch.Clear();
                    Vector2 previous = _clip[_clip.Count - 1];
                    float previousSide = Cross(b - a, previous - a);
                    foreach (Vector2 point in _clip)
                    {
                        float side = Cross(b - a, point - a);
                        if ((side >= 0f) != (previousSide >= 0f))
                            _scratch.Add(Vector2.LerpUnclamped(previous, point, previousSide / (previousSide - side)));
                        if (side >= 0f) _scratch.Add(point);
                        previous = point;
                        previousSide = side;
                    }
                    (_clip, _scratch) = (_scratch, _clip);
                }
            }
            int start = vh.currentVertCount;
            foreach (Vector2 point in _clip) AddVertex(vh, center + point, tint);
            for (int i = 1; i + 1 < _clip.Count; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }

        private void DrawViewCone(VertexHelper vh, Vector2 center, float radius)
        {
            const int rays = 32;
            const int rings = 5;
            int start = vh.currentVertCount;
            for (int ring = 0; ring <= rings; ring++)
            for (int ray = 0; ray <= rays; ray++)
            {
                float distance = (float)ring / rings;
                float across = (float)ray / rays;
                float angle = _viewHeading + Mathf.Lerp(-_coneAngle * 0.5f, _coneAngle * 0.5f, across);
                Vector2 direction = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                Color tint = viewConeColor;
                tint.a *= Mathf.Lerp(1f, 0.08f, distance) * Mathf.Clamp01(Mathf.Min(across, 1f - across) * 12f);
                AddVertex(vh, center + direction * (radius * distance), tint);
                if (ring == 0 || ray == 0) continue;
                int a = start + (ring - 1) * (rays + 1) + ray - 1;
                int b = a + rays + 1;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(a + 1, b, b + 1);
            }
        }

        private void DrawRing(VertexHelper vh, Vector2 center, float outer, float inner, Color outside, Color inside)
        {
            if (outer <= inner) return;
            int start = vh.currentVertCount;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(vh, center + direction * outer, outside);
                AddVertex(vh, center + direction * inner, inside);
                if (i == 0) continue;
                int a = start + (i - 1) * 2;
                vh.AddTriangle(a, a + 2, a + 1);
                vh.AddTriangle(a + 1, a + 2, a + 3);
            }
        }

        private void DrawArrowGlow(VertexHelper vh, Vector2 center, float size)
        {
            if (arrowGlowWidth <= 0f || arrowGlowColor.a <= 0f) return;
            float left = -size * 0.38f - arrowGlowWidth;
            float bottom = -size * 0.4f - arrowGlowWidth;
            float width = size * 0.76f + arrowGlowWidth * 2f;
            float height = size + arrowGlowWidth * 2f;
            int columns = Mathf.Clamp(Mathf.CeilToInt(width / 1.5f), 4, 48);
            int rows = Mathf.Clamp(Mathf.CeilToInt(height / 1.5f), 4, 48);
            float angle = _playerHeading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 right = new Vector2(forward.y, -forward.x);
            int start = vh.currentVertCount;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                Vector2 point = new Vector2(left + width * x / columns, bottom + height * y / rows);
                float fade = 1f - Mathf.Clamp01(DistanceToArrow(point, size) / arrowGlowWidth);
                Color tint = arrowGlowColor;
                tint.a *= fade * fade;
                AddVertex(vh, center + right * point.x + forward * point.y, tint);
                if (x == 0 || y == 0) continue;
                int a = start + (y - 1) * (columns + 1) + x - 1;
                int b = a + columns + 1;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(a + 1, b, b + 1);
            }
        }

        private static float DistanceToArrow(Vector2 point, float size)
        {
            float nearestSquared = float.PositiveInfinity;
            bool inside = false;
            for (int i = 0; i < ArrowShape.Length; i++)
            {
                Vector2 a = ArrowShape[i] * size;
                Vector2 b = ArrowShape[(i + 1) % ArrowShape.Length] * size;
                Vector2 edge = b - a;
                float along = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
                nearestSquared = Mathf.Min(nearestSquared, (point - (a + edge * along)).sqrMagnitude);
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < a.x + edge.x * (point.y - a.y) / edge.y) inside = !inside;
            }
            return inside ? 0f : Mathf.Sqrt(nearestSquared);
        }

        private void DrawArrow(VertexHelper vh, Vector2 center, float size, Color tint)
        {
            float angle = _playerHeading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 right = new Vector2(forward.y, -forward.x);
            int start = vh.currentVertCount;
            foreach (Vector2 point in ArrowShape)
                AddVertex(vh, center + (right * point.x + forward * point.y) * size, tint);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper vh, Vector2 point, Color tint)
            => vh.AddVert(point, (Color32)(tint * color), Vector2.zero);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
