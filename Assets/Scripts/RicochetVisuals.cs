using UnityEngine;

namespace FunFlow
{
    public sealed partial class RicochetGame
    {
        private Sprite floorSprite;
        private Texture2D softCircle;
        private readonly Color Purple = new Color(.54f, .36f, .91f);
        private readonly Color MutedText = new Color(.43f, .37f, .56f);
        private readonly Color PalePurple = new Color(.90f, .84f, 1f);

        private void CreateDesignTextures()
        {
            CreateIcons();
            const int width = 512, height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float noise = Mathf.PerlinNoise(x * .63f, y * .63f) * .035f;
                bool tile = ((x / 32) + (y / 32)) % 2 == 0;
                Color color = tile ? new Color(.43f, .81f, .71f) : new Color(.40f, .77f, .69f);
                color += new Color(noise, noise, noise, 0);
                if (x % 32 == 0 || y % 32 == 0) color = new Color(.34f, .71f, .65f);
                if ((x % 32 == 3 && y % 32 == 3) || (x % 32 == 28 && y % 32 == 28)) color = new Color(.66f, .91f, .79f);
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply();
            floorSprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, width);
            // Keep the floor's unit dimensions independent of its rectangular texture.
            softCircle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] glow = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * 32) / 32;
                glow[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2));
            }
            softCircle.SetPixels(glow); softCircle.Apply();
        }

        private void DecorateFloor()
        {
            // Sprite.Create uses a single pixels-per-unit value: stretch y back to the requested arena size.
            var floor = arena.GetChild(0);
            floor.localScale = new Vector3(23.2f, 24.4f, 1);
            for (int x = -10; x <= 10; x += 2)
            {
                Part(arena, "Arena chevron", new Vector2(x, -5.45f), new Vector2(.36f, .07f), new Color(.87f, .98f, .67f), -8);
                Part(arena, "Arena chevron", new Vector2(x, 5.45f), new Vector2(.36f, .07f), new Color(.87f, .98f, .67f), -8);
            }
            foreach (float x in new[] { -10.6f, 10.6f })
                Part(arena, "Side lane", new Vector2(x, 0), new Vector2(.035f, 10.2f), new Color(.76f, .96f, .7f), -8);
            for (int i = 0; i < 8; i++)
            {
                float x = -9.8f + i * 2.8f;
                Part(arena, "Boundary warning", new Vector2(x, 5.85f), new Vector2(.4f, .12f), Gold, 5);
                Part(arena, "Boundary warning", new Vector2(-x, -5.85f), new Vector2(.4f, .12f), Gold, 5);
            }
        }

        private void DecorateWall(Transform parent, Vector2 position, Vector2 size)
        {
            bool horizontal = size.x >= size.y;
            float longSide = horizontal ? size.x : size.y;
            int segments = Mathf.Clamp(Mathf.FloorToInt(longSide / .9f), 1, 24);
            for (int i = 0; i < segments; i++)
            {
                float offset = (i - (segments - 1) * .5f) * longSide / segments;
                Vector2 at = position + (horizontal ? new Vector2(offset, 0) : new Vector2(0, offset));
                Part(parent, "Reflective marker", at, horizontal ? new Vector2(.16f, .07f) : new Vector2(.07f, .16f), Gold, 5);
            }
            if (size.x > .5f && size.y > .5f && size.x < 6 && size.y < 6)
            {
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
                {
                    Vector2 at = position + new Vector2(x * (size.x * .5f - .13f), y * (size.y * .5f - .13f));
                    Part(parent, "Panel bolt", at, Vector2.one * .065f, Cream, 5, true);
                }
            }
        }

        private void Dot(Rect rect, Color color, bool soft = false)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, soft ? softCircle : circle.texture);
            GUI.color = previous;
        }

        private void Line(Vector2 start, Vector2 end, float width, Color color)
        {
            Matrix4x4 before = GUI.matrix;
            Vector2 delta = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            Panel(new Rect(start.x, start.y - width * .5f, delta.magnitude, width), color);
            GUI.matrix = before;
        }

        private void Frame(Rect rect, Color color, float edge = 2)
        {
            Panel(new Rect(rect.x, rect.y, rect.width, edge), color);
            Panel(new Rect(rect.x, rect.yMax - edge, rect.width, edge), color);
            Panel(new Rect(rect.x, rect.y, edge, rect.height), color);
            Panel(new Rect(rect.xMax - edge, rect.y, edge, rect.height), color);
        }

        private void Card(Rect rect, Color color)
        {
            Panel(new Rect(rect.x + 5, rect.y + 6, rect.width, rect.height), new Color(Ink.r, Ink.g, Ink.b, .15f));
            Panel(rect, color);
            Frame(rect, Ink, 2);
        }

        private void TankIllustration(Vector2 center, float unit, Color color, float angle)
        {
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            float x = center.x, y = center.y;
            Dot(new Rect(x - unit * .7f, y - unit * .5f, unit * 1.5f, unit * 1.5f), new Color(Ink.r, Ink.g, Ink.b, .18f));
            Panel(new Rect(x - unit * .58f, y - unit * .58f, unit * .28f, unit * 1.18f), Ink);
            Panel(new Rect(x + unit * .30f, y - unit * .58f, unit * .28f, unit * 1.18f), Ink);
            for (int i = 0; i < 7; i++)
            {
                float at = y - unit * .5f + i * unit * .16f;
                Panel(new Rect(x - unit * .54f, at, unit * .2f, unit * .055f), PalePurple);
                Panel(new Rect(x + unit * .34f, at, unit * .2f, unit * .055f), PalePurple);
            }
            Panel(new Rect(x - unit * .38f, y - unit * .54f, unit * .76f, unit * 1.06f), Ink);
            Panel(new Rect(x - unit * .33f, y - unit * .49f, unit * .66f, unit * .96f), color);
            Panel(new Rect(x - unit * .28f, y - unit * .44f, unit * .08f, unit * .81f), Color.Lerp(color, Color.white, .6f));
            for (int i = 0; i < 3; i++) Panel(new Rect(x - unit * .16f + i * unit * .13f, y + unit * .3f, unit * .05f, unit * .1f), Ink);
            Panel(new Rect(x - unit * .09f, y - unit * 1.04f, unit * .18f, unit * .84f), Ink);
            Panel(new Rect(x - unit * .045f, y - unit * 1.0f, unit * .09f, unit * .78f), Cream);
            Panel(new Rect(x - unit * .14f, y - unit * 1.03f, unit * .28f, unit * .13f), Gold);
            Dot(new Rect(x - unit * .27f, y - unit * .28f, unit * .54f, unit * .54f), Ink);
            Dot(new Rect(x - unit * .22f, y - unit * .23f, unit * .44f, unit * .44f), Color.Lerp(color, Color.white, .2f));
            Dot(new Rect(x - unit * .13f, y - unit * .13f, unit * .19f, unit * .19f), Cream);
            GUI.matrix = before;
        }

        private void DrawDamageEdges()
        {
            float alpha = hitOverlay * .7f;
            for (int i = 0; i < 6; i++)
            {
                float thickness = 8;
                Color color = new Color(1, .25f, .14f, alpha * (1 - i / 6f));
                Panel(new Rect(i * thickness, 73, thickness, 592), color);
                Panel(new Rect(1280 - (i + 1) * thickness, 73, thickness, 592), color);
                Panel(new Rect(0, 73 + i * thickness, 1280, thickness), color);
                Panel(new Rect(0, 665 - (i + 1) * thickness, 1280, thickness), color);
            }
        }

        private void DrawActorBars()
        {
            foreach (var tank in Tanks)
            {
                if (!tank || !tank.Alive || !tank.IsBoss) continue;
                Vector3 pixel = WorldCamera.WorldToScreenPoint(tank.transform.position + Vector3.up * .96f);
                Vector3 gui = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(pixel.x, Screen.height - pixel.y, 0));
                Panel(new Rect(gui.x - 43, gui.y - 5, 86, 10), Ink);
                Panel(new Rect(gui.x - 40, gui.y - 2, 80f * tank.Health / tank.MaxHealth, 4), Gold);
                Icon(new Rect(gui.x - 10, gui.y - 30, 20, 20), UiIcon.Crown, Ink);
            }
        }

        private void DrawCrosshair()
        {
            Vector3 mouse = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0));
            if (mouse.y < 75 || mouse.y > 662) return;
            Dot(new Rect(mouse.x - 6, mouse.y - 6, 12, 12), Ink);
            Dot(new Rect(mouse.x - 3, mouse.y - 3, 6, 6), Cream);
            Line(new Vector2(mouse.x - 14, mouse.y), new Vector2(mouse.x - 8, mouse.y), 2, Ink);
            Line(new Vector2(mouse.x + 8, mouse.y), new Vector2(mouse.x + 14, mouse.y), 2, Ink);
            Line(new Vector2(mouse.x, mouse.y - 14), new Vector2(mouse.x, mouse.y - 8), 2, Ink);
            Line(new Vector2(mouse.x, mouse.y + 8), new Vector2(mouse.x, mouse.y + 14), 2, Ink);
        }
    }
}
