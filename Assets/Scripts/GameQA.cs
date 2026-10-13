using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FunFlow
{
    /// <summary>Opt-in standalone verification. Inactive during normal play.</summary>
    public sealed class GameQA : MonoBehaviour
    {
        private RicochetGame game;
        private string folder;
        private int failures;
        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-qaOutput");
            if (index < 0 || index + 1 >= args.Length) { enabled = false; yield break; }
            folder = args[index + 1];
            Directory.CreateDirectory(folder);
            game = GetComponent<RicochetGame>();
            game.ControlledByQA = true;
            if (Array.IndexOf(args, "-qaNoticesOnly") >= 0)
            {
                yield return CaptureNotices();
                Application.Quit(0);
                yield break;
            }
            yield return new WaitForSeconds(.5f);
            yield return Capture("01-menu");
            game.QAIconPreview = true;
            yield return Capture("00-icons");
            game.QAIconPreview = false;
            bool iconsValid = true;
            foreach (UiIcon kind in Enum.GetValues(typeof(UiIcon)))
            {
                Texture2D texture = game.IconSprite(kind).texture;
                Color[] pixels = texture.GetPixels();
                int opaque = 0;
                for (int i = 0; i < pixels.Length; i++) if (pixels[i].a > .5f) opaque++;
                iconsValid &= opaque > 150;
                for (int i = 0; i < texture.width; i++)
                    iconsValid &= pixels[i].a == 0 && pixels[(texture.height - 1) * texture.width + i].a == 0 && pixels[i * texture.width].a == 0 && pixels[i * texture.width + texture.width - 1].a == 0;
            }
            Check(iconsValid, "All 22 icons have visible shapes and transparent unclipped borders");
            game.OpenHelp();
            yield return Capture("10-help");
            game.CloseHelp();
            game.BeginGame();
            game.enabled = false;
            Check(game.Player && game.Player.Health == 5 && game.Wave == 1, "Start initializes health and first wave");
            game.Player.transform.position = new Vector2(-10, 0);
            Physics2D.SyncTransforms();
            game.Player.Move(Vector2.left, 4.2f, 5f);
            Check(game.Player.transform.position.x > -10.96f, "Swept movement cannot cross outer wall");
            game.Player.transform.position = new Vector2(-.79f, 1.0f);
            game.Player.Move(Vector2.left, 4.2f, .25f);
            Check(game.Player.transform.position.x < -1.6f && game.TankPositionClear(game.Player.transform.position, game.Player.Radius), "Tank exits an overlapping convex corner without sticking");
            game.Player.transform.position = new Vector2(-1f, 1.3f);
            for (int i = 0; i < 120; i++) game.Player.Move(new Vector2(.2f, -1f).normalized, 4.2f, 1f / 60);
            Check(game.Player.transform.position.y < -2f && game.TankPositionClear(game.Player.transform.position, game.Player.Radius), "Tank slides along a face and clears both corners");
            bool layoutsClear = true, reachable = true;
            string previousLayout = "";
            for (int i = 1; i <= 5; i++)
            {
                game.ApplyLayout(i);
                layoutsClear &= game.LayoutName != previousLayout && game.TankPositionClear(new Vector2(-7.5f, 0), .56f);
                foreach (var point in new[] { new Vector2(-9, 4), new Vector2(9, 4), new Vector2(-9, -4), new Vector2(9, -4), new Vector2(0, 4.5f), new Vector2(0, -4.5f) })
                    layoutsClear &= game.TankPositionClear(point, .56f);
                reachable &= ZoneReachable();
                previousLayout = game.LayoutName;
            }
            Check(layoutsClear, "All five distinct layouts leave player and boss spawn points clear");
            Check(reachable, "Hot zone is reachable from staging area in every layout");
            game.ApplyLayout(1);
            game.Player.transform.position = new Vector2(-9, 0);
            Physics2D.SyncTransforms();
            Shell shell = game.SpawnShell(game.Player, new Vector2(-10, 0), Vector2.left);
            shell.Tick(.25f);
            Check(shell.Active && shell.Bounces == 1 && shell.transform.position.x > -11, "Shell reflects and consumes remaining travel");
            Shell cover = game.SpawnShell(game.Player, new Vector2(-2, 0), Vector2.right);
            cover.Tick(.25f);
            Check(cover.Bounces > 0 && cover.transform.position.x < -.6f, "Shell cannot tunnel through central cover");
            game.Player.transform.position = new Vector2(-10.8f, 0);
            game.Player.Aim(Vector2.left);
            Physics2D.SyncTransforms();
            bool fired = game.Player.Fire();
            Shell close = game.Shells[game.Shells.Count - 1];
            close.Tick(.1f);
            Check(fired && close.Bounces > 0 && close.transform.position.x > -11.35f, "Barrel at wall cannot shoot through it");
            game.Finish(false, "QA");
            Check(game.State == GameState.Lost, "Defeat enters result state");
            game.BeginGame();
            Check(game.Score == 0 && game.Kills == 0 && game.Shells.Count == 0 && game.Tanks.Count == 1, "Restart removes old actors and resets progress");
            // Spawn enemies through the real wave scheduler; age their entry protection in real time.
            for (int i = 0; i < 3; i++) game.TickGame(3f);
            Check(game.AliveEnemies == 3 && game.EnemiesLeft == 4, "First wave schedules three regular enemies and a final boss");
            yield return new WaitForSeconds(1.5f);
            game.Player.transform.position = new Vector2(-7.5f, -.4f);
            game.Player.Aim(new Vector2(1, .5f));
            game.enabled = true;
            Time.timeScale = 0;
            yield return Capture("02-gameplay");
            game.enabled = false;
            Time.timeScale = 1;
            TankActor target = null;
            foreach (var tank in game.Tanks) if (!tank.IsPlayer) { target = tank; break; }
            if (target)
            {
                target.transform.position = new Vector2(-7.5f, 2f);
                game.Player.transform.position = new Vector2(-7.5f, 0);
                Physics2D.SyncTransforms();
                Shell hit = game.SpawnShell(game.Player, new Vector2(-7.5f, .7f), Vector2.up);
                hit.Tick(.15f);
                Check(!target.Alive && game.Kills == 1 && game.Score == 100, "Projectile hit destroys enemy and awards score once");
            }
            TankActor shooter = null, teammate = null;
            foreach (var tank in game.Tanks)
            {
                if (!tank || tank.IsPlayer || !tank.Alive) continue;
                if (!shooter) shooter = tank; else { teammate = tank; break; }
            }
            if (shooter && teammate)
            {
                teammate.transform.position = new Vector2(-7, -3);
                Shell orphan = game.SpawnShell(shooter, new Vector2(-9, -3), Vector2.right);
                int health = teammate.Health;
                shooter.Damage(99, false);
                yield return null;
                Physics2D.SyncTransforms();
                orphan.Tick(.3f);
                Check(orphan.Active && teammate.Health == health, "Destroyed shooter does not change projectile team");
            }
            int playerHealth = game.Player.Health;
            game.Player.Damage(1, false);
            game.Player.Damage(1, false);
            Check(game.Player.Health == playerHealth - 1, "Damage protection prevents repeated damage in one instant");
            for (int wave = 1; wave <= RicochetGame.TotalWaves; wave++)
            {
                int guard = 0;
                while (game.Wave == wave && game.State == GameState.Playing && guard++ < 12)
                {
                    foreach (var tank in game.Tanks.ToArray()) if (tank && !tank.IsPlayer && tank.Alive) tank.Damage(99, true);
                    game.TickGame(3f);
                    yield return new WaitForSeconds(1.35f);
                }
            }
            Check(game.State == GameState.Won && game.Wave == 5 && game.Kills == 30, "All five waves and five bosses finish with victory and 30 kills");
            Check(game.LayoutIndex == 5 && game.LayoutName == "ЦИТАДЕЛЬ", "Wave scheduler advances through all cover layouts");
            game.enabled = true;
            yield return Capture("03-victory");
            game.BeginGame();
            game.enabled = false;
            game.TickGame(181f);
            Check(game.State == GameState.Lost, "Three-minute limit finishes attempt");
            game.enabled = true;
            yield return Capture("04-defeat");
            game.BeginGame();
            game.enabled = false;
            yield return new WaitForSeconds(.9f);
            game.Player.Damage(99, false);
            Check(game.State == GameState.Lost && !game.Player.Alive, "Lost armor finishes attempt");
            game.enabled = true;
            yield return Capture("06-armor-defeat");
            game.BeginGame();
            game.Pause();
            yield return Capture("05-pause");
            game.Resume();
            Check(game.State == GameState.Playing && Time.timeScale == 1, "Resume restores active play");
            game.BeginGame(GameMode.Capture);
            game.enabled = false;
            game.Player.transform.position = game.CaptureCenter;
            game.UpdateCaptureProgress(2);
            Check(game.CaptureProgress == 2 && game.PlayerInZone && !game.CaptureContested, "Capture progresses inside an uncontested hot zone");
            GameObject bossObject = new GameObject("QA Boss");
            var boss = bossObject.AddComponent<TankActor>();
            boss.Initialize(game, false, 3);
            game.Tanks.Add(boss);
            boss.transform.position = game.CaptureCenter + Vector2.right;
            game.UpdateCaptureProgress(2);
            Check(game.CaptureProgress == 2 && game.CaptureContested, "Enemy presence pauses capture without losing progress");
            Check(boss.IsBoss && boss.Health > 2 && boss.Radius > game.Player.Radius, "Boss has more armor and a larger silhouette");
            boss.transform.position = new Vector2(8, 4);
            game.UpdateCaptureProgress(1);
            game.enabled = true;
            Time.timeScale = 0;
            yield return Capture("08-capture");
            game.enabled = false;
            Time.timeScale = 1;
            boss.transform.position = new Vector2(-7, 2);
            game.Player.transform.position = new Vector2(-7, 0);
            yield return new WaitForSeconds(1.3f);
            Physics2D.SyncTransforms();
            Shell heavy = game.SpawnShell(boss, new Vector2(-7, 1), Vector2.down);
            Check(heavy.DamageAmount == 2 && heavy.Radius == .18f, "Boss projectile has double damage and double width");
            heavy.Tick(.2f);
            Check(game.Player.Health == 3 && game.ShakeStrength >= .2f && game.SparkCount >= 30, "Boss hit deals two armor damage and creates shake and sparks");
            game.enabled = true;
            yield return new WaitForSeconds(.07f);
            Time.timeScale = 0;
            yield return Capture("07-hit-sparks");
            game.enabled = false;
            Time.timeScale = 1;
            game.Player.transform.position = game.CaptureCenter;
            boss.transform.position = new Vector2(8, 4);
            game.UpdateCaptureProgress(1);
            game.UpdateCaptureProgress(100);
            game.TickGame(0);
            Check(game.State == GameState.Won && game.CaptureProgress == RicochetGame.CaptureGoal, "Capture goal finishes separate mode with victory");
            game.BeginGame(GameMode.Capture);
            game.Player.transform.position = game.CaptureCenter;
            bool hotBoss = game.TrySpawnHotBoss();
            TankActor hot = null;
            foreach (var tank in game.Tanks) if (tank && tank.IsBoss) hot = tank;
            Check(hotBoss && hot && Vector2.Distance(hot.transform.position, game.CaptureCenter) <= 3.1f && game.TankPositionClear(hot.transform.position, hot.Radius), "Hot-zone boss spawns nearby with a safe clear position");
            game.BeginGame(GameMode.Capture);
            game.enabled = false;
            game.Player.transform.position = new Vector2(-3, 0);
            int oldHealth = game.Player.Health;
            game.TickGame(33f);
            Check(game.LayoutIndex == 2 && game.Player.Health == oldHealth && game.TankPositionClear(game.Player.transform.position, game.Player.Radius), "Capture terrain transition preserves armor and relocates overlapping tanks safely");
            game.BeginGame(GameMode.Clearance);
            game.enabled = false;
            game.Player.transform.position = new Vector2(-7, 0);
            yield return new WaitForSeconds(.95f);
            game.Player.Damage(3, false);
            var medkit = game.SpawnPickup(PickupKind.Medkit, game.Player.transform.position);
            Check(medkit && medkit.TryCollect(game.Player) && game.Player.Health == 4 && !medkit.Active, "Medkit restores exactly two health and is consumed once");
            game.Player.Heal(2);
            Check(game.Player.Health == 5, "Healing cannot exceed maximum health");
            var fullMedkit = game.SpawnPickup(PickupKind.Medkit, new Vector2(-7, 0));
            Check(fullMedkit && !fullMedkit.TryCollect(game.Player) && fullMedkit.Active, "Full-health player leaves medkit available for later");
            var enemyObject = new GameObject("Pickup QA enemy");
            var enemy = enemyObject.AddComponent<TankActor>();
            enemy.Initialize(game, false, 0);
            enemy.transform.position = new Vector2(-7, 0);
            game.Tanks.Add(enemy);
            Check(!fullMedkit.TryCollect(enemy) && fullMedkit.Active && enemy.Health == enemy.MaxHealth, "Enemy cannot collect or consume a medkit");
            var shield = game.SpawnPickup(PickupKind.Shield, new Vector2(-6, 0));
            Check(shield && !shield.TryCollect(enemy) && shield.Active && !enemy.Shielded, "Enemy cannot collect or consume a shield");
            Check(shield.TryCollect(game.Player) && game.Player.ShieldRemaining == 8 && !shield.Active, "Player collects an eight-second shield");
            game.Player.Damage(2, false);
            Check(game.Player.Health == 5, "Shield blocks even double-damage boss hits");
            game.Player.Tick(3f);
            float shieldTime = game.Player.ShieldRemaining;
            game.enabled = true;
            game.Pause();
            yield return new WaitForSecondsRealtime(.25f);
            Check(game.Player.ShieldRemaining == shieldTime, "Pause freezes shield duration");
            game.Resume();
            game.enabled = false;
            fullMedkit.Retire();
            enemy.transform.position = new Vector2(8, 4);
            var displayedMed = game.SpawnPickup(PickupKind.Medkit, new Vector2(-6, -3));
            var displayedShield = game.SpawnPickup(PickupKind.Shield, new Vector2(-2, -3));
            game.enabled = true;
            Time.timeScale = 0;
            yield return Capture("09-pickups-shield");
            game.enabled = false;
            Time.timeScale = 1;
            game.Player.Tick(6f);
            yield return new WaitForSeconds(.9f);
            game.Player.Damage(1, false);
            Check(!game.Player.Shielded && game.Player.Health == 4, "Expired shield restores normal damage behavior");
            displayedMed.Tick(19f);
            Check(!displayedMed.Active, "Uncollected pickups expire after eighteen seconds");
            game.BeginGame(GameMode.Clearance);
            Check(game.Pickups.Count == 0 && !game.Player.Shielded, "Restart removes pickups and active shield");
            game.enabled = false;
            game.TickPickups(10f);
            Check(game.Pickups.Count == 0, "Rare pickups do not spawn during the first ten seconds");
            game.TickPickups(18f);
            bool rareClear = game.Pickups.Count >= 1 && game.Pickups.Count <= 2;
            foreach (var item in game.Pickups) rareClear &= item && item.Active && game.TankPositionClear(item.transform.position, .6f);
            Check(rareClear && !game.TrySpawnRarePickup(PickupKind.Medkit), "Rare scheduler spawns on clear ground and prevents duplicate types");
            game.BeginGame(GameMode.Clearance);
            game.ReturnToMenu();
            game.enabled = true;
            File.AppendAllText(Path.Combine(folder, "results.txt"), "FAILURES=" + failures + Environment.NewLine);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator CaptureNotices()
        {
            game.BeginGame(GameMode.Clearance);
            game.enabled = false;
            for (int i = 0; i < 3; i++) game.TickGame(3);
            yield return new WaitForSeconds(1.4f);
            foreach (var tank in game.Tanks.ToArray()) if (tank && !tank.IsPlayer) tank.Damage(99, false);
            game.TickGame(0);
            game.enabled = true;
            yield return new WaitForSeconds(.45f);
            game.enabled = false;
            game.Player.Damage(2, false);
            var medkit = game.SpawnPickup(PickupKind.Medkit, game.Player.transform.position);
            medkit.TryCollect(game.Player);
            game.enabled = true;
            Time.timeScale = 0;
            yield return Capture("01-medkit-small-notice");
            game.enabled = false;
            Time.timeScale = 1;
            var shield = game.SpawnPickup(PickupKind.Shield, game.Player.transform.position);
            shield.TryCollect(game.Player);
            game.Player.Tick(0);
            game.enabled = true;
            Time.timeScale = 0;
            yield return Capture("02-shield-no-popup");
            Time.timeScale = 1;
        }

        private bool ZoneReachable()
        {
            const int width = 55, height = 28;
            const float step = .4f;
            var visited = new bool[width, height];
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            Vector2 origin = new Vector2(-10.8f, -5.4f);
            Vector2Int start = new Vector2Int(8, 14);
            queue.Enqueue(start); visited[start.x, start.y] = true;
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                Vector2 position = origin + new Vector2(cell.x, cell.y) * step;
                if (Vector2.Distance(position, game.CaptureCenter) < .6f) return true;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height || visited[next.x, next.y]) continue;
                    Vector2 destination = origin + new Vector2(next.x, next.y) * step;
                    if (!game.TankPositionClear(destination, .45f)) continue;
                    if (Vector2.Distance(game.MoveTankCircle(position, destination - position, .43f), destination) > .025f) continue;
                    visited[next.x, next.y] = true; queue.Enqueue(next);
                }
            }
            return false;
        }

        private void Check(bool passed, string message)
        {
            if (!passed) failures++;
            string line = (passed ? "PASS " : "FAIL ") + message;
            Debug.Log(line);
            File.AppendAllText(Path.Combine(folder, "results.txt"), line + Environment.NewLine);
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.35f);
        }
    }
}
