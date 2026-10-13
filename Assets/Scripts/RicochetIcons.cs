using System;
using System.Collections.Generic;
using UnityEngine;

namespace FunFlow
{
    public enum UiIcon { Target, Flag, Waves, Heart, Medkit, Shield, Star, Clock, Play, Pause, Home, Restart, Keyboard, Mouse, Speaker, Info, Trophy, Skull, Crown, Check, Arrow, Ring }

    public sealed partial class RicochetGame
    {
        private readonly Dictionary<UiIcon, Texture2D> iconTextures = new Dictionary<UiIcon, Texture2D>();
        private readonly Dictionary<UiIcon, Sprite> iconSprites = new Dictionary<UiIcon, Sprite>();
        private bool showHelp;
        private UiIcon noticeIcon = UiIcon.Waves;

        private sealed class IconPath
        {
            public readonly List<Func<Vector2, float>> Shapes = new List<Func<Vector2, float>>();
            public void Line(float x1, float y1, float x2, float y2, float thickness = .055f)
            {
                Vector2 a = new Vector2(x1, y1), b = new Vector2(x2, y2);
                Shapes.Add(p => SegmentDistance(p, a, b) - thickness * .5f);
            }
            public void Circle(float x, float y, float radius, float thickness = .055f)
            {
                Vector2 center = new Vector2(x, y);
                Shapes.Add(p => Mathf.Abs(Vector2.Distance(p, center) - radius) - thickness * .5f);
            }
            public void Disk(float x, float y, float radius)
            {
                Vector2 center = new Vector2(x, y);
                Shapes.Add(p => Vector2.Distance(p, center) - radius);
            }
            public void Arc(float x, float y, float radius, float start, float finish, int segments = 24)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = Mathf.Lerp(start, finish, (float)i / segments);
                    float b = Mathf.Lerp(start, finish, (float)(i + 1) / segments);
                    Line(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius);
                }
            }
            public void Box(float x, float y, float width, float height, bool fill = false)
            {
                if (fill) Polygon(true, x, y, x + width, y, x + width, y + height, x, y + height);
                else Polygon(false, x, y, x + width, y, x + width, y + height, x, y + height);
            }
            public void Polygon(bool fill, params float[] coordinates)
            {
                var points = new Vector2[coordinates.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
                if (!fill)
                {
                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector2 a = points[i], b = points[(i + 1) % points.Length];
                        Line(a.x, a.y, b.x, b.y);
                    }
                    return;
                }
                Shapes.Add(p =>
                {
                    float distance = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    {
                        Vector2 a = points[j], b = points[i];
                        distance = Mathf.Min(distance, SegmentDistance(p, a, b));
                        if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    return inside ? -distance : distance;
                });
            }
            private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 direction = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, direction) / Mathf.Max(.00001f, direction.sqrMagnitude));
                return Vector2.Distance(p, a + direction * t);
            }
        }

        private void CreateIcons()
        {
            foreach (UiIcon kind in Enum.GetValues(typeof(UiIcon)))
            {
                var path = new IconPath();
                switch (kind)
                {
                    case UiIcon.Target:
                        path.Circle(.5f, .5f, .28f); path.Circle(.5f, .5f, .12f);
                        path.Line(.08f, .5f, .31f, .5f); path.Line(.69f, .5f, .92f, .5f);
                        path.Line(.5f, .08f, .5f, .31f); path.Line(.5f, .69f, .5f, .92f); break;
                    case UiIcon.Flag:
                        path.Line(.25f, .14f, .25f, .89f); path.Line(.14f, .89f, .48f, .89f);
                        path.Polygon(false, .25f, .17f, .82f, .17f, .7f, .4f, .82f, .59f, .25f, .59f); break;
                    case UiIcon.Waves:
                        for (int i = 0; i < 3; i++) { float y = .24f + .24f * i; path.Line(.15f, y + .13f, .5f, y); path.Line(.5f, y, .85f, y + .13f); } break;
                    case UiIcon.Heart:
                        path.Disk(.33f, .34f, .22f); path.Disk(.67f, .34f, .22f);
                        path.Polygon(true, .115f, .43f, .885f, .43f, .5f, .91f); break;
                    case UiIcon.Medkit:
                        path.Box(.12f, .27f, .76f, .58f); path.Box(.33f, .13f, .34f, .14f);
                        path.Box(.44f, .4f, .12f, .33f, true); path.Box(.33f, .505f, .34f, .12f, true); break;
                    case UiIcon.Shield:
                        path.Polygon(false, .19f, .16f, .81f, .16f, .78f, .6f, .68f, .77f, .5f, .9f, .32f, .77f, .22f, .6f);
                        path.Line(.35f, .49f, .46f, .61f); path.Line(.46f, .61f, .67f, .36f); break;
                    case UiIcon.Star:
                        var star = new float[20];
                        for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2 + i * Mathf.PI / 5; float r = i % 2 == 0 ? .41f : .18f; star[i * 2] = .5f + Mathf.Cos(a) * r; star[i * 2 + 1] = .5f + Mathf.Sin(a) * r; }
                        path.Polygon(true, star); break;
                    case UiIcon.Clock:
                        path.Circle(.5f, .53f, .33f); path.Line(.5f, .29f, .5f, .53f); path.Line(.5f, .53f, .69f, .61f);
                        path.Line(.38f, .08f, .62f, .08f); path.Line(.5f, .08f, .5f, .17f); break;
                    case UiIcon.Play: path.Polygon(true, .29f, .15f, .86f, .5f, .29f, .85f); break;
                    case UiIcon.Pause: path.Box(.26f, .17f, .17f, .66f, true); path.Box(.57f, .17f, .17f, .66f, true); break;
                    case UiIcon.Home:
                        path.Line(.09f, .45f, .5f, .12f); path.Line(.5f, .12f, .91f, .45f);
                        path.Polygon(false, .22f, .38f, .22f, .85f, .41f, .85f, .41f, .6f, .59f, .6f, .59f, .85f, .78f, .85f, .78f, .38f); break;
                    case UiIcon.Restart:
                        path.Arc(.5f, .5f, .32f, -2.55f, -7.65f, 36);
                        path.Polygon(true, .36f, .16f, .61f, .11f, .55f, .36f); break;
                    case UiIcon.Keyboard:
                        path.Box(.38f, .13f, .24f, .28f); path.Box(.08f, .48f, .24f, .28f); path.Box(.38f, .48f, .24f, .28f); path.Box(.68f, .48f, .24f, .28f);
                        path.Line(.17f, .86f, .83f, .86f); break;
                    case UiIcon.Mouse:
                        path.Arc(.5f, .39f, .26f, Mathf.PI, Mathf.PI * 2);
                        path.Arc(.5f, .61f, .26f, 0, Mathf.PI);
                        path.Line(.24f, .39f, .24f, .61f); path.Line(.76f, .39f, .76f, .61f);
                        path.Line(.5f, .13f, .5f, .47f); path.Line(.24f, .47f, .76f, .47f); path.Box(.33f, .29f, .10f, .11f, true); break;
                    case UiIcon.Speaker:
                        path.Polygon(false, .09f, .38f, .3f, .38f, .52f, .2f, .52f, .8f, .3f, .62f, .09f, .62f);
                        path.Arc(.53f, .5f, .23f, -.85f, .85f, 14);
                        path.Arc(.53f, .5f, .38f, -.85f, .85f, 18); break;
                    case UiIcon.Info:
                        path.Circle(.5f, .5f, .36f); path.Disk(.5f, .31f, .045f); path.Line(.5f, .45f, .5f, .73f); break;
                    case UiIcon.Trophy:
                        path.Polygon(false, .29f, .15f, .71f, .15f, .69f, .48f, .6f, .61f, .4f, .61f, .31f, .48f);
                        path.Line(.5f, .61f, .5f, .83f); path.Line(.31f, .87f, .69f, .87f);
                        path.Line(.29f, .23f, .12f, .23f); path.Line(.12f, .23f, .17f, .46f); path.Line(.17f, .46f, .34f, .5f);
                        path.Line(.71f, .23f, .88f, .23f); path.Line(.88f, .23f, .83f, .46f); path.Line(.83f, .46f, .66f, .5f); break;
                    case UiIcon.Skull:
                        path.Polygon(false, .31f, .14f, .69f, .14f, .85f, .31f, .85f, .6f, .71f, .67f, .69f, .87f, .31f, .87f, .29f, .67f, .15f, .6f, .15f, .31f);
                        path.Disk(.33f, .44f, .07f); path.Disk(.67f, .44f, .07f); path.Polygon(true, .5f, .54f, .45f, .64f, .55f, .64f);
                        path.Line(.44f, .73f, .44f, .87f); path.Line(.56f, .73f, .56f, .87f); break;
                    case UiIcon.Crown:
                        path.Polygon(false, .15f, .29f, .33f, .48f, .5f, .15f, .67f, .48f, .85f, .29f, .77f, .75f, .23f, .75f);
                        path.Line(.23f, .88f, .77f, .88f); break;
                    case UiIcon.Check: path.Line(.17f, .48f, .39f, .71f, .10f); path.Line(.39f, .71f, .84f, .24f, .10f); break;
                    case UiIcon.Arrow: path.Line(.14f, .5f, .84f, .5f, .07f); path.Line(.59f, .24f, .85f, .5f, .07f); path.Line(.85f, .5f, .59f, .76f, .07f); break;
                    case UiIcon.Ring: path.Circle(.5f, .5f, .41f, .035f); break;
                }
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2((x + .5f) / size, 1f - (y + .5f) / size);
                    float distance = float.MaxValue;
                    foreach (var shape in path.Shapes) distance = Mathf.Min(distance, shape(point));
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - distance * size));
                }
                texture.SetPixels(pixels); texture.Apply();
                iconTextures[kind] = texture;
                iconSprites[kind] = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            }
        }

        public Sprite IconSprite(UiIcon kind) => iconSprites[kind];
        public void OpenHelp() { if (State == GameState.Playing) Pause(); showHelp = true; }
        public void CloseHelp() { showHelp = false; }

        public bool PointerInArena()
        {
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float x = (Input.mousePosition.x - (Screen.width - 1280 * scale) * .5f) / scale;
            float y = (Screen.height - Input.mousePosition.y - (Screen.height - 720 * scale) * .5f) / scale;
            return x > 0 && x < 1280 && y > 73 && y < 665;
        }

        private void Icon(Rect rect, UiIcon kind, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, iconTextures[kind]);
            GUI.color = previous;
        }

        private bool IconButton(Rect rect, UiIcon icon, string tooltip)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Panel(rect, hover ? PalePurple : Cream);
            Icon(new Rect(rect.x + 7, rect.y + 7, rect.width - 14, rect.height - 14), icon, Ink);
            if (hover) Tooltip(new Vector2(Mathf.Clamp(rect.center.x, 130, 1150), rect.yMax + 10), tooltip);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void Tooltip(Vector2 position, string text)
        {
            Panel(new Rect(position.x - 120, position.y, 240, 32), Ink);
            Text(new Rect(position.x - 115, position.y, 230, 32), text, new GUIStyle(small) { fontSize = 15, alignment = TextAnchor.MiddleCenter }, Cream);
        }

        private bool ActionButton(Rect rect, UiIcon icon, string text, bool primary = false, string key = null)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Panel(new Rect(rect.x + 3, rect.y + 5, rect.width, rect.height), Ink);
            Panel(rect, primary ? (hover ? new Color(1f, .83f, .28f) : Gold) : (hover ? PalePurple : new Color(.94f, .9f, 1f)));
            Icon(new Rect(rect.x + 17, rect.y + (rect.height - 32) / 2, 32, 32), icon, Ink);
            Text(new Rect(rect.x + 57, rect.y, rect.width - 75, rect.height), text, new GUIStyle(button) { alignment = TextAnchor.MiddleLeft, fontSize = 23 });
            if (key != null) Text(new Rect(rect.xMax - 52, rect.y, 38, rect.height), key, new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, MutedText);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void Notice(string message, UiIcon icon, float duration = 1.6f)
        {
            banner = message;
            noticeIcon = icon;
            bannerUntil = Time.time + Mathf.Min(duration, 1.5f);
        }

        private void DrawNotice()
        {
            if (Time.time >= bannerUntil) return;
            float alpha = Mathf.Clamp01((bannerUntil - Time.time) / .25f);
            Color color = noticeIcon == UiIcon.Crown ? Coral : MutedText;
            color.a = alpha;
            Icon(new Rect(77, 52, 14, 14), noticeIcon, color);
            Text(new Rect(98, 46, 153, 24), banner, new GUIStyle(small) { fontSize = 12, wordWrap = false }, color);
        }

        private void DrawHelp()
        {
            Panel(new Rect(0, 0, 1280, 720), new Color(Ink.r, Ink.g, Ink.b, .5f));
            Card(new Rect(325, 132, 630, 456), Cream);
            Icon(new Rect(359, 156, 36, 36), UiIcon.Info, Purple);
            Text(new Rect(410, 149, 470, 50), "Как играть", new GUIStyle(title) { fontSize = 35 });
            if (IconButton(new Rect(886, 148, 44, 44), UiIcon.Check, "Понятно")) showHelp = false;
            HelpRow(223, UiIcon.Keyboard, "WASD / стрелки", "Движение");
            HelpRow(279, UiIcon.Mouse, "Мышь + ЛКМ", "Прицел и огонь");
            HelpRow(335, UiIcon.Medkit, "+2 здоровья", "Наедь на аптечку");
            HelpRow(391, UiIcon.Shield, "8 секунд защиты", "Наедь на щит");
            HelpRow(447, UiIcon.Crown, "Минибосс", "Больше здоровья, двойной урон");
            HelpRow(503, UiIcon.Pause, "ESC", "Пауза");
        }

        private void HelpRow(float y, UiIcon icon, string main, string detail)
        {
            Icon(new Rect(360, y, 35, 35), icon, icon == UiIcon.Medkit ? Coral : Purple);
            Text(new Rect(415, y - 3, 225, 42), main, new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold });
            Text(new Rect(655, y - 3, 270, 42), detail, new GUIStyle(small) { fontSize = 17 }, MutedText);
        }
    }
}
