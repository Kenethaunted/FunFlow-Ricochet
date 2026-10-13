using UnityEngine;

namespace FunFlow
{
    public sealed partial class RicochetGame
    {
        [System.NonSerialized] public bool QAIconPreview;

        private void DrawMenu()
        {
            Panel(new Rect(0, 0, 1280, 720), Cream);
            Panel(new Rect(0, 0, 1280, 7), Purple);
            Icon(new Rect(81, 60, 56, 56), UiIcon.Target, Purple);
            Text(new Rect(153, 38, 730, 100), "РИКОШЕТ", new GUIStyle(title) { fontSize = 76 });
            Icon(new Rect(87, 162, 26, 26), UiIcon.Play, Teal);
            Text(new Rect(125, 151, 340, 46), "Выбери режим", new GUIStyle(label) { fontSize = 23 }, MutedText);
            TankIllustration(new Vector2(986, 134), 75, Teal, 34);
            if (IconButton(new Rect(1100, 43, 48, 48), UiIcon.Speaker, muted ? "Включить звук" : "Выключить звук")) muted = !muted;
            if (muted) Line(new Vector2(1106, 84), new Vector2(1142, 50), 3, Coral);
            if (IconButton(new Rect(1167, 43, 48, 48), UiIcon.Info, "Как играть")) OpenHelp();

            if (ModeCard(new Rect(90, 235, 520, 266), false)) BeginGame(GameMode.Clearance);
            if (ModeCard(new Rect(670, 235, 520, 266), true)) BeginGame(GameMode.Capture);

            Panel(new Rect(90, 550, 1100, 89), new Color(.94f, .90f, 1f));
            QuickHint(new Vector2(116, 566), UiIcon.Keyboard, "WASD", "Движение");
            QuickHint(new Vector2(392, 566), UiIcon.Mouse, "Мышь + ЛКМ", "Прицел и огонь");
            QuickHint(new Vector2(700, 566), UiIcon.Medkit, "+2 здоровья", "Аптечка");
            QuickHint(new Vector2(962, 566), UiIcon.Shield, "8 секунд", "Щит");
            Icon(new Rect(91, 671, 16, 16), UiIcon.Star, Purple);
            Text(new Rect(119, 660, 700, 38), "FunFlow × ТПУ", new GUIStyle(small) { fontSize = 15 }, MutedText);
        }

        private bool ModeCard(Rect rect, bool capture)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Card(rect, hover ? new Color(.88f, .96f, .92f) : Color.white);
            Panel(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, 6), capture ? Teal : Purple);
            Icon(new Rect(rect.x + 34, rect.y + 42, 84, 84), capture ? UiIcon.Flag : UiIcon.Target, capture ? Teal : Purple);
            Text(new Rect(rect.x + 145, rect.y + 35, 346, 55), capture ? "Захват" : "Зачистка", new GUIStyle(title) { fontSize = 37 });
            Text(new Rect(rect.x + 145, rect.y + 92, 346, 35), capture ? "Удерживай точку" : "Уничтожь всех врагов", new GUIStyle(small) { fontSize = 20 }, MutedText);
            Icon(new Rect(rect.x + 39, rect.y + 151, 30, 30), capture ? UiIcon.Flag : UiIcon.Waves, capture ? Teal : Purple);
            Text(new Rect(rect.x + 83, rect.y + 144, 174, 44), capture ? "90 секунд" : "5 волн", new GUIStyle(label) { fontSize = 24 });
            Icon(new Rect(rect.x + 292, rect.y + 151, 30, 30), UiIcon.Clock, MutedText);
            Text(new Rect(rect.x + 336, rect.y + 144, 156, 44), "3 минуты", new GUIStyle(label) { fontSize = 23 });
            Icon(new Rect(rect.x + 42, rect.y + 214, 27, 27), UiIcon.Play, Ink);
            Text(new Rect(rect.x + 83, rect.y + 204, 342, 46), "Играть", new GUIStyle(label) { fontSize = 23, fontStyle = FontStyle.Bold });
            Icon(new Rect(rect.x + 449, rect.y + 211, 32, 32), UiIcon.Arrow, Ink);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void QuickHint(Vector2 position, UiIcon icon, string main, string detail)
        {
            Color color = icon == UiIcon.Medkit ? Coral : (icon == UiIcon.Shield ? new Color(.05f, .47f, 1f) : Purple);
            Icon(new Rect(position.x, position.y + 4, 40, 40), icon, color);
            Text(new Rect(position.x + 56, position.y - 5, 236, 33), main, new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold });
            Text(new Rect(position.x + 56, position.y + 28, 230, 24), detail, new GUIStyle(small) { fontSize = 16 }, MutedText);
        }

        private void DrawHUD()
        {
            Panel(new Rect(0, 0, 1280, 73), Cream);
            Panel(new Rect(0, 70, 1280, 3), Ink);
            Icon(new Rect(25, 16, 36, 36), Mode == GameMode.Capture ? UiIcon.Flag : UiIcon.Target, Mode == GameMode.Capture ? Teal : Purple);
            Text(new Rect(76, 15, 175, 40), Mode == GameMode.Capture ? "Захват" : "Зачистка", new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold });
            HudStat(new Vector2(277, 13), UiIcon.Waves, Wave + " / 5", "Волна", Purple);
            HudStat(new Vector2(493, 13), Mode == GameMode.Capture ? UiIcon.Flag : UiIcon.Target,
                Mode == GameMode.Capture ? Mathf.FloorToInt(CaptureProgress) + " / 90" : EnemiesLeft.ToString(), Mode == GameMode.Capture ? "Захват" : "Враги", Mode == GameMode.Capture ? Teal : Coral);
            if (Mode == GameMode.Capture)
            {
                Panel(new Rect(543, 62, 118, 4), PalePurple);
                Panel(new Rect(543, 62, 118 * CaptureProgress / CaptureGoal, 4), CaptureContested ? Coral : Teal);
            }
            HudStat(new Vector2(741, 13), UiIcon.Star, Score.ToString(), "Очки", Purple);
            int remaining = Mathf.CeilToInt(Mathf.Max(0, 180 - Elapsed));
            HudStat(new Vector2(950, 13), UiIcon.Clock, (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00"), "Время", remaining < 30 ? Coral : Purple);
            if (IconButton(new Rect(1153, 13, 44, 44), UiIcon.Speaker, muted ? "Звук выключен" : "Звук включён")) muted = !muted;
            if (muted) Line(new Vector2(1159, 51), new Vector2(1191, 19), 3, Coral);
            if (IconButton(new Rect(1214, 13, 44, 44), UiIcon.Pause, "Пауза · ESC")) Pause();

            Panel(new Rect(0, 665, 1280, 55), Cream);
            Panel(new Rect(0, 665, 1280, 2), Ink);
            Icon(new Rect(26, 677, 29, 29), UiIcon.Heart, Coral);
            for (int i = 0; i < 5; i++)
            {
                Panel(new Rect(70 + i * 29, 682, 22, 18), Ink);
                Panel(new Rect(73 + i * 29, 685, 16, 12), Player && i < Player.Health ? Teal : PalePurple);
            }
            bool shield = Player && Player.Shielded;
            if (pickupToastTime > 0 && pickupToastKind == PickupKind.Medkit)
            {
                float alpha = Mathf.Clamp01(pickupToastTime / .2f);
                Text(new Rect(218, 676, 32, 29), "+" + pickupHealthRestored, new GUIStyle(small) { fontSize = 16, fontStyle = FontStyle.Bold }, new Color(Teal.r, Teal.g, Teal.b, alpha));
            }
            Icon(new Rect(258, 676, 31, 31), UiIcon.Shield, shield ? new Color(.05f, .47f, 1f) : MutedText);
            Text(new Rect(302, 675, 92, 29), shield ? Mathf.CeilToInt(Player.ShieldRemaining) + " с" : "—", new GUIStyle(label) { fontSize = 21 }, shield ? Ink : MutedText);
            if (shield)
            {
                Panel(new Rect(301, 705, 67, 3), PalePurple);
                Panel(new Rect(301, 705, 67 * Mathf.Clamp01(Player.ShieldRemaining / 8f), 3), new Color(.05f, .47f, 1f));
            }
            ControlHint(493, UiIcon.Keyboard, "WASD");
            ControlHint(711, UiIcon.Mouse, "ЛКМ");
            ControlHint(914, UiIcon.Pause, "ESC");
            ControlHint(1120, UiIcon.Speaker, "M");
            if (Mode == GameMode.Capture)
            {
                string status = CaptureContested ? "Враг на точке" : (PlayerInZone ? "Захват идёт" : "Войди в зону");
                Card(new Rect(473, 612, 334, 36), Cream);
                Icon(new Rect(486, 619, 22, 22), UiIcon.Flag, CaptureContested ? Coral : Teal);
                Text(new Rect(522, 612, 270, 36), status, new GUIStyle(small) { fontSize = 18, fontStyle = FontStyle.Bold });
            }
        }

        private void HudStat(Vector2 position, UiIcon icon, string value, string caption, Color color)
        {
            Icon(new Rect(position.x, position.y + 3, 33, 33), icon, color);
            Text(new Rect(position.x + 51, position.y - 8, 170, 41), value, new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold });
            Text(new Rect(position.x + 52, position.y + 29, 170, 21), caption, new GUIStyle(small) { fontSize = 13 }, MutedText);
        }

        private void ControlHint(float x, UiIcon icon, string key)
        {
            Icon(new Rect(x, 678, 25, 25), icon, MutedText);
            Text(new Rect(x + 40, 672, 128, 39), key, new GUIStyle(small) { fontSize = 17 }, MutedText);
        }

        private void DrawPause()
        {
            Panel(new Rect(0, 73, 1280, 592), new Color(Ink.r, Ink.g, Ink.b, .53f));
            Card(new Rect(390, 174, 500, 365), Cream);
            Icon(new Rect(430, 199, 44, 44), UiIcon.Pause, Purple);
            Text(new Rect(495, 190, 360, 63), "Пауза", new GUIStyle(title) { fontSize = 47 });
            if (ActionButton(new Rect(430, 282, 420, 58), UiIcon.Play, "Продолжить", true, "ESC")) Resume();
            if (ActionButton(new Rect(430, 359, 420, 58), UiIcon.Home, "Главное меню")) ReturnToMenu();
            if (ActionButton(new Rect(430, 436, 420, 58), UiIcon.Info, "Управление")) OpenHelp();
        }

        private void DrawResult()
        {
            bool won = State == GameState.Won;
            Panel(new Rect(0, 73, 1280, 592), new Color(Ink.r, Ink.g, Ink.b, .53f));
            Card(new Rect(286, 161, 708, 403), Cream);
            Panel(new Rect(286, 161, 708, 6), won ? Teal : Coral);
            Icon(new Rect(334, 190, 56, 56), won ? UiIcon.Trophy : UiIcon.Skull, won ? Teal : Coral);
            Text(new Rect(415, 178, 530, 85), won ? "Победа!" : "Поражение", new GUIStyle(title) { fontSize = 51 });
            Text(new Rect(337, 267, 605, 32), EndReason, new GUIStyle(small) { fontSize = 20 }, MutedText);
            ResultStat(new Vector2(333, 327), UiIcon.Star, Score.ToString(), "Очки");
            ResultStat(new Vector2(545, 327), UiIcon.Target, Kills.ToString(), "Танки");
            ResultStat(new Vector2(757, 327), UiIcon.Clock, Mathf.FloorToInt(Elapsed) + " с", "Время");
            if (ActionButton(new Rect(332, 438, 312, 63), UiIcon.Restart, "Ещё раз", true, "R")) BeginGame();
            if (ActionButton(new Rect(666, 438, 280, 63), UiIcon.Home, "Меню")) ReturnToMenu();
        }

        private void ResultStat(Vector2 position, UiIcon icon, string value, string labelText)
        {
            Icon(new Rect(position.x, position.y + 2, 32, 32), icon, Purple);
            Text(new Rect(position.x + 48, position.y - 6, 162, 45), value, new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold });
            Text(new Rect(position.x + 50, position.y + 36, 155, 28), labelText, new GUIStyle(small) { fontSize = 16 }, MutedText);
        }

        private void DrawIconPreview()
        {
            Panel(new Rect(0, 0, 1280, 720), Cream);
            Text(new Rect(40, 20, 1190, 55), "Проверка иконок: крупный и рабочий размер", new GUIStyle(label) { fontSize = 25, fontStyle = FontStyle.Bold });
            UiIcon[] icons = (UiIcon[])System.Enum.GetValues(typeof(UiIcon));
            for (int i = 0; i < icons.Length; i++)
            {
                float x = 35 + (i % 6) * 208, y = 90 + (i / 6) * 148;
                Panel(new Rect(x, y, 193, 131), Color.white);
                Icon(new Rect(x + 17, y + 12, 66, 66), icons[i], Purple);
                Icon(new Rect(x + 111, y + 30, 29, 29), icons[i], Ink);
                Text(new Rect(x + 15, y + 84, 168, 34), icons[i].ToString(), new GUIStyle(small) { fontSize = 16 });
            }
        }
    }
}
