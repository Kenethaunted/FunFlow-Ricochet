using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FunFlow
{
    public enum GameState { Menu, Playing, Paused, Won, Lost }
    public enum SoundKind { Shot, EnemyShot, Bounce, Hit, Hurt, Destroy, Wave, Win, Pickup }
    public enum GameMode { Clearance, Capture }

    public sealed partial class RicochetGame : MonoBehaviour
    {
        public const int WallMask = 1 << 8;
        public const int TankMask = 1 << 9;
        public const int TotalWaves = 5;
        public readonly Color Ink = new Color(.13f, .10f, .30f);
        public readonly Color Slate = new Color(.46f, .42f, .67f);
        public readonly Color Teal = new Color(.04f, .69f, .57f);
        public readonly Color Coral = new Color(1f, .22f, .37f);
        public readonly Color Gold = new Color(1f, .72f, .09f);
        public readonly Color Cream = new Color(1f, .97f, .88f);
        public GameState State { get; private set; } = GameState.Menu;
        public Camera WorldCamera { get; private set; }
        public TankActor Player { get; private set; }
        public List<TankActor> Tanks { get; } = new List<TankActor>();
        public List<Shell> Shells { get; } = new List<Shell>();
        public Material SpriteMaterial { get; private set; }
        public int Wave { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public float Elapsed { get; private set; }
        public string EndReason { get; private set; }
        public GameMode Mode { get; private set; }
        public float CaptureProgress { get; private set; }
        public const float CaptureGoal = 90f;
        public Vector2 CaptureCenter { get; private set; }
        public bool CaptureContested { get; private set; }
        public bool PlayerInZone { get; private set; }
        private float captureWaveClock, captureSpawnClock, bossClock, bossWarning;
        private bool waveBossSpawned;
        private Transform captureZone;
        [System.NonSerialized] public bool ControlledByQA;
        public int EnemiesLeft => enemiesPending + AliveEnemies + (Mode == GameMode.Clearance && !waveBossSpawned ? 1 : 0);
        public int AliveEnemies
        {
            get { int count = 0; foreach (var t in Tanks) if (t && t.Alive && !t.IsPlayer) count++; return count; }
        }
        private Sprite square, circle;
        private Transform arena, actors, effects, obstacles;
        private readonly List<Rect> solidWalls = new List<Rect>();
        public string LayoutName { get; private set; } = "ПЕРИМЕТР";
        public int LayoutIndex { get; private set; }
        public int SparkCount => particles.Count;
        public float ShakeStrength => shake;
        private AudioSource audioSource;
        private readonly Dictionary<SoundKind, AudioClip> clips = new Dictionary<SoundKind, AudioClip>();
        private readonly List<ParticleBit> particles = new List<ParticleBit>();
        private LineRenderer aimLine;
        private float spawnTimer, nextWaveTimer, bannerUntil, shake, hitOverlay;
        private int enemiesPending, spawnIndex;
        private readonly Vector2[] spawnPoints = { new Vector2(-9, 4), new Vector2(9, 4), new Vector2(-9, -4), new Vector2(9, -4), new Vector2(0, 4.5f), new Vector2(0, -4.5f) };
        private GUIStyle label, small, title, button;
        private bool muted;
        private string banner;
        private Vector2 cameraBase = new Vector2(0, .2f);

        private sealed class ParticleBit
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life, MaxLife;
            public Color Color;
            public bool Spark;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Physics2D.queriesHitTriggers = true;
            square = MakeSprite(false);
            circle = MakeSprite(true);
            CreateDesignTextures();
            SpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            WorldCamera = Camera.main;
            if (!WorldCamera) WorldCamera = new GameObject("Game Camera", typeof(Camera)).GetComponent<Camera>();
            WorldCamera.tag = "MainCamera";
            WorldCamera.orthographic = true;
            WorldCamera.backgroundColor = new Color(.36f, .30f, .63f);
            WorldCamera.clearFlags = CameraClearFlags.SolidColor;
            WorldCamera.transform.position = new Vector3(cameraBase.x, cameraBase.y, -10);
            if (!WorldCamera.GetComponent<AudioListener>()) WorldCamera.gameObject.AddComponent<AudioListener>();
            actors = new GameObject("Actors").transform;
            effects = new GameObject("Effects").transform;
            BuildArena();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = .22f;
            CreateSounds();
            aimLine = new GameObject("First ricochet guide").AddComponent<LineRenderer>();
            aimLine.material = SpriteMaterial;
            aimLine.startWidth = .025f;
            aimLine.endWidth = .012f;
            aimLine.startColor = new Color(Ink.r, Ink.g, Ink.b, .65f);
            aimLine.endColor = new Color(Ink.r, Ink.g, Ink.b, .18f);
            aimLine.sortingOrder = 7;
            aimLine.enabled = false;
            ResizeCamera();
        }

        private Sprite MakeSprite(bool round)
        {
            int size = round ? 64 : 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size / 2f);
                pixels[y * size + x] = round ? new Color(1, 1, 1, Mathf.Clamp01(size / 2f - distance)) : Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        public SpriteRenderer Part(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order, bool round = false)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = round ? circle : square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void BuildArena()
        {
            arena = new GameObject("Arena - single handcrafted layout").transform;
            var floor = Part(arena, "Industrial tile floor", Vector2.zero, new Vector2(23.2f, 12.2f), Color.white, -10);
            floor.sprite = floorSprite;
            DecorateFloor();
            Wall(new Vector2(-11.6f, 0), new Vector2(.5f, 12.7f));
            Wall(new Vector2(11.6f, 0), new Vector2(.5f, 12.7f));
            Wall(new Vector2(0, -6.1f), new Vector2(23.7f, .5f));
            Wall(new Vector2(0, 6.1f), new Vector2(23.7f, .5f));
            ApplyLayout(1);
        }

        private void Wall(Vector2 position, Vector2 size, Transform parent = null)
        {
            Transform root = parent ? parent : arena;
            solidWalls.Add(new Rect(position - size * .5f, size));
            SpriteRenderer wall = Part(root, "Reflective wall", position, size, new Color(.28f, .20f, .52f), 2);
            wall.gameObject.layer = 8;
            var collider = wall.gameObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            Part(root, "Wall shadow", position + new Vector2(.12f, -.18f), size + Vector2.one * .07f, new Color(.13f, .10f, .30f, .3f), 1);
            Part(root, "Steel panel", position, size - Vector2.one * .12f, new Color(.59f, .47f, .91f), 3);
            Part(root, "Bevel light", position + new Vector2(0, size.y / 2 - .045f), new Vector2(size.x - .07f, .055f), new Color(.87f, .79f, 1f), 4);
            Part(root, "Bevel shade", position - new Vector2(0, size.y / 2 - .045f), new Vector2(size.x - .07f, .055f), new Color(.36f, .26f, .65f), 4);
            DecorateWall(root, position, size);
        }

        public void ApplyLayout(int wave)
        {
            if (obstacles) { obstacles.gameObject.SetActive(false); Destroy(obstacles.gameObject); }
            if (solidWalls.Count > 4) solidWalls.RemoveRange(4, solidWalls.Count - 4);
            obstacles = new GameObject("Wave cover layout").transform;
            obstacles.SetParent(arena, false);
            LayoutIndex = Mathf.Clamp(wave, 1, 5);
            string[] names = { "ПЕРИМЕТР", "КОРИДОР", "ФЛАНГИ", "КРЕСТ", "ЦИТАДЕЛЬ" };
            LayoutName = names[LayoutIndex - 1];
            switch (LayoutIndex)
            {
                case 1:
                    Cover(-4.5f, 2.1f, 3.4f, .7f); Cover(4.5f, -2.1f, 3.4f, .7f);
                    Cover(-4.5f, -2.1f, .7f, 2.4f); Cover(4.5f, 2.1f, .7f, 2.4f); Cover(0, 0, 1.1f, 1.5f); break;
                case 2:
                    Cover(-3, 0, .75f, 4.8f); Cover(3, 0, .75f, 4.8f);
                    Cover(0, 3.2f, 2.6f, .7f); Cover(0, -3.2f, 2.6f, .7f); Cover(0, 0, 1.1f, 1.1f); break;
                case 3:
                    Cover(-5, 2.4f, 3.7f, .75f); Cover(5, -2.4f, 3.7f, .75f);
                    Cover(-1.6f, -.8f, .75f, 3f); Cover(1.6f, .8f, .75f, 3f); Cover(0, 3.3f, 1.2f, .7f); break;
                case 4:
                    Cover(-3.7f, 0, 2.7f, .75f); Cover(3.7f, 0, 2.7f, .75f);
                    Cover(0, 2.7f, .75f, 2.2f); Cover(0, -2.7f, .75f, 2.2f);
                    Cover(-6, -3f, 1.1f, 1.1f); Cover(6, 3f, 1.1f, 1.1f); break;
                default:
                    Cover(-2.8f, 0, .75f, 2.3f); Cover(2.8f, 0, .75f, 2.3f);
                    Cover(0, 2.7f, 2.5f, .75f); Cover(0, -2.7f, 2.5f, .75f);
                    Cover(-6, 2.5f, 1.6f, .8f); Cover(6, -2.5f, 1.6f, .8f); break;
            }
            if (captureZone) { captureZone.gameObject.SetActive(false); Destroy(captureZone.gameObject); }
            CaptureCenter = LayoutIndex == 1 ? new Vector2(0, -3.3f) : (LayoutIndex == 2 ? new Vector2(6f, 0) : Vector2.zero);
            if (Mode == GameMode.Capture && State != GameState.Menu) CreateCaptureZone();
            Physics2D.SyncTransforms();
        }

        private void Cover(float x, float y, float width, float height) => Wall(new Vector2(x, y), new Vector2(width, height), obstacles);

        /// <summary>Small geometric steps resolve penetration against the same rectangles as the colliders.
        /// This avoids zero-distance cast normals which previously trapped tanks at convex corners.</summary>
        public Vector2 MoveTankCircle(Vector2 position, Vector2 delta, float radius)
        {
            position = SeparateFromWalls(position, radius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .10f));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++) position = SeparateFromWalls(position + step, radius);
            return position;
        }

        public Vector2 SeparateFromWalls(Vector2 position, float radius)
        {
            float padded = radius + .012f;
            position.x = Mathf.Clamp(position.x, -11.35f + padded, 11.35f - padded);
            position.y = Mathf.Clamp(position.y, -5.85f + padded, 5.85f - padded);
            for (int pass = 0; pass < 6; pass++)
            {
                bool overlap = false;
                foreach (Rect wall in solidWalls)
                {
                    Vector2 closest = new Vector2(Mathf.Clamp(position.x, wall.xMin, wall.xMax), Mathf.Clamp(position.y, wall.yMin, wall.yMax));
                    Vector2 offset = position - closest;
                    float distanceSquared = offset.sqrMagnitude;
                    if (distanceSquared >= padded * padded) continue;
                    overlap = true;
                    if (distanceSquared > .0000001f) position = closest + offset.normalized * padded;
                    else
                    {
                        float left = position.x - wall.xMin, right = wall.xMax - position.x;
                        float bottom = position.y - wall.yMin, top = wall.yMax - position.y;
                        float minimum = Mathf.Min(left, right, bottom, top);
                        if (minimum == left) position.x = wall.xMin - padded;
                        else if (minimum == right) position.x = wall.xMax + padded;
                        else if (minimum == bottom) position.y = wall.yMin - padded;
                        else position.y = wall.yMax + padded;
                    }
                }
                if (!overlap) break;
            }
            return position;
        }

        public bool TankPositionClear(Vector2 position, float radius)
        {
            if (Mathf.Abs(position.x) > 11.35f - radius || Mathf.Abs(position.y) > 5.85f - radius) return false;
            foreach (Rect wall in solidWalls)
            {
                Vector2 nearest = new Vector2(Mathf.Clamp(position.x, wall.xMin, wall.xMax), Mathf.Clamp(position.y, wall.yMin, wall.yMax));
                if ((position - nearest).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        public void BeginGame()
        {
            ClearAttempt();
            State = GameState.Playing;
            Time.timeScale = 1;
            Score = 0; Kills = 0; Wave = 0; Elapsed = 0;
            CaptureProgress = 0; CaptureContested = false; PlayerInZone = false;
            ResetPickupSchedule();
            captureWaveClock = 0; captureSpawnClock = 0; bossClock = Random.Range(32f, 45f); bossWarning = 0;
            EndReason = "";
            Player = SpawnTank(true, new Vector2(-7.5f, 0), 0);
            StartWave();
            Physics2D.SyncTransforms();
        }

        public void BeginGame(GameMode mode) { Mode = mode; BeginGame(); }

        public void ReturnToMenu()
        {
            Time.timeScale = 1;
            ClearAttempt();
            State = GameState.Menu;
            if (captureZone) captureZone.gameObject.SetActive(false);
        }

        private void ClearAttempt()
        {
            ClearPickups();
            showHelp = false;
            foreach (var tank in Tanks) if (tank) { tank.gameObject.SetActive(false); Destroy(tank.gameObject); }
            foreach (var shell in Shells) if (shell) { shell.gameObject.SetActive(false); Destroy(shell.gameObject); }
            foreach (var bit in particles) if (bit.Transform) Destroy(bit.Transform.gameObject);
            Tanks.Clear(); Shells.Clear(); particles.Clear();
            Player = null; enemiesPending = 0; nextWaveTimer = 0; spawnTimer = 0; shake = 0; hitOverlay = 0;
            aimLine.enabled = false;
        }

        private TankActor SpawnTank(bool player, Vector2 position, int tier)
        {
            GameObject tank = new GameObject(player ? "Player tank" : "Enemy tank");
            tank.transform.SetParent(actors);
            tank.transform.position = position;
            TankActor actor = tank.AddComponent<TankActor>();
            actor.Initialize(this, player, tier);
            Tanks.Add(actor);
            Burst(position, player ? Teal : Coral, 12, 1f);
            return actor;
        }

        private void StartWave()
        {
            ClearPickups();
            Wave++;
            waveBossSpawned = false;
            foreach (var shell in Shells) if (shell) { shell.gameObject.SetActive(false); Destroy(shell.gameObject); }
            Shells.Clear();
            ApplyLayout(Wave);
            if (Player)
            {
                Player.transform.position = Mode == GameMode.Clearance ? new Vector2(-7.5f, 0) : SeparateFromWalls(Player.transform.position, Player.Radius);
                if (Mode == GameMode.Clearance) Player.Aim(Vector2.right);
            }
            foreach (var tank in Tanks) if (tank && tank.Alive && !tank.IsPlayer) tank.transform.position = SeparateFromWalls(tank.transform.position, tank.Radius);
            Physics2D.SyncTransforms();
            enemiesPending = 2 + Wave;
            spawnTimer = 1.2f;
            nextWaveTimer = -1;
            Notice("Волна " + Wave + " / 5", UiIcon.Waves, 2.2f);
            Sound(SoundKind.Wave);
        }

        private void SpawnEnemy()
        {
            if (TrySpawnEnemy(Wave >= 4 ? 2 : (Wave >= 2 ? 1 : 0))) enemiesPending--;
            else spawnTimer = .5f;
        }

        private bool TrySpawnEnemy(int tier)
        {
            for (int attempt = 0; attempt < spawnPoints.Length; attempt++)
            {
                Vector2 point = spawnPoints[(spawnIndex + attempt) % spawnPoints.Length];
                bool clear = !Player || Vector2.Distance(point, Player.transform.position) > 3.5f;
                foreach (var tank in Tanks) if (tank && tank.Alive && Vector2.Distance(point, tank.transform.position) < 1.5f) clear = false;
                if (!clear) continue;
                spawnIndex = (spawnIndex + attempt + 1) % spawnPoints.Length;
                SpawnTank(false, point, tier);
                return true;
            }
            return false;
        }

        private void Update()
        {
            ResizeCamera();
            Cursor.visible = State != GameState.Playing;
            if (Input.GetKeyDown(KeyCode.M)) muted = !muted;
            audioSource.mute = muted;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (showHelp) showHelp = false;
                else if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }
            if (Input.GetKeyDown(KeyCode.R) && (State == GameState.Won || State == GameState.Lost)) BeginGame();
            if (State == GameState.Playing) TickGame(Time.deltaTime);
            UpdateParticles(Time.deltaTime);
            if (State != GameState.Paused) shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.1f);
            Vector2 kick = State == GameState.Paused ? Vector2.zero : new Vector2(Mathf.Sin(Time.time * 97f), Mathf.Cos(Time.time * 83f)) * shake;
            WorldCamera.transform.position = new Vector3(cameraBase.x + kick.x, cameraBase.y + kick.y, -10);
            if (State != GameState.Paused) hitOverlay = Mathf.MoveTowards(hitOverlay, 0, Time.deltaTime * 1.6f);
            if (State == GameState.Playing) UpdateAimGuide(); else aimLine.enabled = false;
        }

        public void TickGame(float dt)
        {
            if (State != GameState.Playing) return;
            Elapsed = Mathf.Min(180f, Elapsed + dt);
            if (Elapsed >= 180f) { Finish(false, "Время вышло"); return; }
            spawnTimer -= dt;
            if (enemiesPending > 0 && spawnTimer <= 0 && (Mode == GameMode.Clearance || AliveEnemies < 6))
            {
                spawnTimer = Mathf.Max(1.4f, 2.8f - Wave * .22f);
                SpawnEnemy();
            }
            int tankCount = Tanks.Count;
            for (int i = 0; i < tankCount; i++) if (Tanks[i] && Tanks[i].Alive) Tanks[i].Tick(dt);
            Physics2D.SyncTransforms();
            for (int i = Shells.Count - 1; i >= 0; i--)
            {
                if (!Shells[i] || !Shells[i].Active) { Shells.RemoveAt(i); continue; }
                Shells[i].Tick(dt);
                if (State != GameState.Playing) break;
            }
            Tanks.RemoveAll(t => !t || !t.Alive);
            if (State != GameState.Playing) return;
            TickPickups(dt);
            if (Mode == GameMode.Capture) { TickCapture(dt); return; }
            if (enemiesPending == 0 && AliveEnemies == 0)
            {
                if (!waveBossSpawned)
                {
                    if (TrySpawnEnemy(3))
                    {
                        waveBossSpawned = true;
                        Notice("Минибосс!", UiIcon.Crown, 2.5f);
                        Sound(SoundKind.Wave);
                    }
                    return;
                }
                if (Wave >= TotalWaves) { Finish(true, "Все 5 волн пройдены"); return; }
                if (nextWaveTimer < 0)
                {
                    nextWaveTimer = 3f;
                    Notice("Волна пройдена", UiIcon.Check, 2.5f);
                    Score += 100;
                }
                nextWaveTimer -= dt;
                if (nextWaveTimer <= 0) StartWave();
            }
        }

        private void TickCapture(float dt)
        {
            UpdateCaptureProgress(dt);
            if (CaptureProgress >= CaptureGoal) { Finish(true, "Точка удержана"); return; }
            captureWaveClock += dt;
            if (captureWaveClock >= 32f && Wave < 5)
            {
                captureWaveClock = 0;
                StartWave();
            }
            captureSpawnClock -= dt;
            if (enemiesPending == 0 && captureSpawnClock <= 0 && AliveEnemies < 6)
            {
                if (TrySpawnEnemy(Wave >= 4 ? 2 : 1)) captureSpawnClock = Random.Range(4f, 6f);
            }
            bossClock -= dt;
            if (bossClock <= 0 && bossWarning <= 0)
            {
                bool existingBoss = false;
                foreach (var tank in Tanks) if (tank && tank.Alive && tank.IsBoss) existingBoss = true;
                if (!existingBoss) { bossWarning = 2.5f; Notice("Минибосс близко", UiIcon.Crown, 2.5f); }
                bossClock = Random.Range(35f, 50f);
            }
            if (bossWarning > 0)
            {
                bossWarning -= dt;
                if (bossWarning <= 0) TrySpawnHotBoss();
            }
            if (captureZone)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3) * .025f;
                captureZone.localScale = Vector3.one * pulse;
            }
        }

        public bool TrySpawnHotBoss()
        {
            foreach (Vector2 offset in new[] { Vector2.left * 3, Vector2.right * 3, Vector2.up * 3, Vector2.down * 3 })
            {
                Vector2 point = CaptureCenter + offset;
                if (!TankPositionClear(point, .58f) || (Player && Vector2.Distance(point, Player.transform.position) < 2.4f)) continue;
                bool occupied = false;
                foreach (var tank in Tanks) if (tank && tank.Alive && Vector2.Distance(point, tank.transform.position) < 1.5f) occupied = true;
                if (occupied) continue;
                SpawnTank(false, point, 3);
                return true;
            }
            return TrySpawnEnemy(3);
        }

        public void UpdateCaptureProgress(float dt)
        {
            if (Mode != GameMode.Capture || State != GameState.Playing) return;
            PlayerInZone = Player && Player.Alive && Vector2.Distance(Player.transform.position, CaptureCenter) <= 1.45f;
            CaptureContested = false;
            foreach (var tank in Tanks)
                if (tank && tank.Alive && !tank.IsPlayer && Vector2.Distance(tank.transform.position, CaptureCenter) < 1.9f) CaptureContested = true;
            if (PlayerInZone && !CaptureContested) CaptureProgress = Mathf.Min(CaptureGoal, CaptureProgress + dt);
        }

        private void CreateCaptureZone()
        {
            captureZone = new GameObject("Hot zone").transform;
            captureZone.SetParent(arena, false);
            captureZone.position = CaptureCenter;
            Part(captureZone, "Zone ring", Vector2.zero, Vector2.one * 3.05f, new Color(1, .91f, .23f, .75f), -6, true);
            Part(captureZone, "Zone center", Vector2.zero, Vector2.one * 2.82f, new Color(.23f, .8f, .68f), -5, true);
            Part(captureZone, "Target horizontal", Vector2.zero, new Vector2(.5f, .09f), Cream, -4);
            Part(captureZone, "Target vertical", Vector2.zero, new Vector2(.09f, .5f), Cream, -4);
        }

        public Shell SpawnShell(TankActor owner, Vector2 position, Vector2 aim)
        {
            GameObject obj = new GameObject("Shell");
            obj.transform.SetParent(actors);
            obj.transform.position = position;
            Shell shell = obj.AddComponent<Shell>();
            shell.Initialize(this, owner, aim);
            Shells.Add(shell);
            return shell;
        }

        public void OnTankDestroyed(TankActor tank, bool bankShot)
        {
            Burst(tank.transform.position, tank.IsPlayer ? Teal : Coral, 22, 3f);
            Sound(SoundKind.Destroy);
            Shake(tank.IsPlayer ? .22f : .06f);
            if (tank.IsPlayer) Finish(false, "Танк уничтожен");
            else
            {
                Kills++;
                Score += tank.IsBoss ? 500 : (bankShot ? 150 : 100);
                if (tank.IsBoss) Notice("Минибосс: +500", UiIcon.Crown, 1.5f);
                if (bankShot && !tank.IsBoss) Notice("Рикошет: +150", UiIcon.Target, 1.2f);
            }
        }

        public void Finish(bool won, string reason)
        {
            if (State != GameState.Playing) return;
            State = won ? GameState.Won : GameState.Lost;
            EndReason = reason;
            if (won) { Score += 500; Sound(SoundKind.Win); }
            aimLine.enabled = false;
        }

        public void Resume() { State = GameState.Playing; Time.timeScale = 1; }
        public void Pause() { if (State == GameState.Playing) { State = GameState.Paused; Time.timeScale = 0; } }

        public float FreeDistance(Vector2 origin, Vector2 direction, float maximum)
        {
            RaycastHit2D hit = Physics2D.CircleCast(origin, .47f, direction, maximum, WallMask);
            return hit.collider ? hit.distance : maximum;
        }

        public Vector2 FindShot(Vector2 origin, Vector2 target)
        {
            Vector2 direct = target - origin;
            if (!Physics2D.CircleCast(origin, .1f, direct.normalized, Mathf.Max(0, direct.magnitude - .4f), WallMask).collider) return direct.normalized;
            // Reflect the target across each inner boundary, then verify both legs of the path.
            for (int i = 0; i < 4; i++)
            {
                Vector2 mirrored = target;
                if (i == 0) mirrored.x = 2 * -11.26f - target.x;
                if (i == 1) mirrored.x = 2 * 11.26f - target.x;
                if (i == 2) mirrored.y = 2 * -5.76f - target.y;
                if (i == 3) mirrored.y = 2 * 5.76f - target.y;
                Vector2 direction = (mirrored - origin).normalized;
                RaycastHit2D hit = Physics2D.CircleCast(origin, .09f, direction, 35f, WallMask);
                if (!hit.collider) continue;
                Vector2 center = hit.centroid;
                Vector2 reflected = Vector2.Reflect(direction, hit.normal);
                Vector2 toTarget = target - center;
                if (Vector2.Dot(reflected, toTarget.normalized) < .999f) continue;
                if (!Physics2D.CircleCast(center + hit.normal * .025f, .09f, reflected, Mathf.Max(0, toTarget.magnitude - .4f), WallMask).collider) return direction;
            }
            return Vector2.zero;
        }

        private void UpdateAimGuide()
        {
            if (!Player || !Player.Alive) { aimLine.enabled = false; return; }
            aimLine.enabled = true;
            Vector2 origin = (Vector2)Player.transform.position + Player.AimDirection * .69f;
            RaycastHit2D barrelWall = Physics2D.Raycast(Player.transform.position, Player.AimDirection, .79f, WallMask);
            if (barrelWall.collider) origin = (Vector2)Player.transform.position + Player.AimDirection * Mathf.Max(.05f, barrelWall.distance - .13f);
            RaycastHit2D hit = Physics2D.CircleCast(origin, .09f, Player.AimDirection, 7f, WallMask);
            if (hit.collider)
            {
                aimLine.positionCount = 3;
                aimLine.SetPosition(0, origin);
                aimLine.SetPosition(1, hit.centroid);
                Vector2 direction = Vector2.Reflect(Player.AimDirection, hit.normal);
                RaycastHit2D second = Physics2D.CircleCast(hit.centroid + hit.normal * .02f, .09f, direction, 3.5f, WallMask);
                aimLine.SetPosition(2, second.collider ? second.centroid : hit.centroid + direction * 3.5f);
            }
            else
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, origin);
                aimLine.SetPosition(1, origin + Player.AimDirection * 7f);
            }
        }

        private void ResizeCamera()
        {
            float aspect = Mathf.Max(.5f, (float)Screen.width / Screen.height);
            WorldCamera.orthographicSize = Mathf.Max(7.65f, 12.5f / aspect);
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); if (amount >= .14f) hitOverlay = .5f; }

        public void ArmorSparks(Vector2 position)
        {
            for (int i = 0; i < 30; i++)
            {
                Vector2 velocity = Random.insideUnitCircle.normalized * Random.Range(2f, 5.5f);
                Color color = i % 3 == 0 ? new Color(1f, .96f, .69f) : (i % 3 == 1 ? Gold : new Color(1f, .4f, .12f));
                var renderer = Part(effects, "Hot armor spark", position + Random.insideUnitCircle * .25f, new Vector2(Random.Range(.12f, .22f), .035f), color, 28);
                renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
                float life = Random.Range(.2f, .5f);
                particles.Add(new ParticleBit { Transform = renderer.transform, Renderer = renderer, Velocity = velocity, Life = life, MaxLife = life, Color = color, Spark = true });
            }
            Burst(position, Color.white, 8, 1.7f);
        }

        public void Burst(Vector2 position, Color color, int count, float speed)
        {
            if (particles.Count > 240) return;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = Part(effects, "Spark", position, Vector2.one * Random.Range(.045f, .13f), color, 25);
                float life = Random.Range(.18f, .45f);
                particles.Add(new ParticleBit { Transform = renderer.transform, Renderer = renderer, Velocity = Random.insideUnitCircle * speed, Life = life, MaxLife = life, Color = color });
            }
        }

        private void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                ParticleBit bit = particles[i];
                bit.Life -= dt;
                if (bit.Life <= 0 || !bit.Transform) { if (bit.Transform) Destroy(bit.Transform.gameObject); particles.RemoveAt(i); continue; }
                bit.Transform.position += (Vector3)(bit.Velocity * dt);
                if (bit.Spark) bit.Velocity += Vector2.down * 2.5f * dt;
                bit.Velocity *= Mathf.Exp(-3f * dt);
                bit.Renderer.color = new Color(bit.Color.r, bit.Color.g, bit.Color.b, bit.Life / bit.MaxLife);
            }
        }

        private void CreateSounds()
        {
            foreach (SoundKind kind in System.Enum.GetValues(typeof(SoundKind)))
            {
                float duration = kind == SoundKind.Win ? .7f : (kind == SoundKind.Wave ? .3f : .11f);
                int count = (int)(22050 * duration);
                float[] data = new float[count];
                float baseHz = kind == SoundKind.Pickup ? 720 : (kind == SoundKind.Bounce ? 950 : (kind == SoundKind.Hurt ? 100 : (kind == SoundKind.Destroy ? 65 : (kind == SoundKind.Wave ? 420 : 210))));
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / 22050;
                    float progress = (float)i / count;
                    float hz = kind == SoundKind.Win ? 440 * Mathf.Pow(2, Mathf.Floor(progress * 4) / 4f) : baseHz * (1 - progress * .6f);
                    float wave = Mathf.Sin(2 * Mathf.PI * hz * t);
                    if (kind == SoundKind.Shot || kind == SoundKind.Destroy || kind == SoundKind.Hurt) wave = wave * .5f + Random.Range(-.5f, .5f);
                    data[i] = wave * Mathf.Pow(1 - progress, 2) * .6f;
                }
                AudioClip clip = AudioClip.Create(kind.ToString(), count, 1, 22050, false);
                clip.SetData(data, 0);
                clips.Add(kind, clip);
            }
        }

        public void Sound(SoundKind kind) { if (audioSource && !muted && clips.TryGetValue(kind, out var clip)) audioSource.PlayOneShot(clip, kind == SoundKind.Bounce ? .35f : 1f); }

        private void SetupGUI()
        {
            if (label != null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = new GUIStyle { font = font, fontSize = 24, normal = { textColor = Ink }, alignment = TextAnchor.MiddleLeft };
            small = new GUIStyle(label) { fontSize = 17, wordWrap = true };
            title = new GUIStyle(label) { fontSize = 76, fontStyle = FontStyle.Bold };
            button = new GUIStyle(label) { fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void Panel(Rect rect, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private void Text(Rect rect, string value, GUIStyle style, Color? color = null)
        {
            Color previous = style.normal.textColor;
            if (color.HasValue) style.normal.textColor = color.Value;
            GUI.Label(rect, value, style);
            style.normal.textColor = previous;
        }

        private bool Button(Rect rect, string text, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Panel(new Rect(rect.x + 3, rect.y + 5, rect.width, rect.height), Ink);
            Panel(rect, primary ? (hover ? new Color(1f, .83f, .28f) : Gold) : (hover ? new Color(.87f, .8f, 1f) : new Color(.94f, .9f, 1f)));
            Text(rect, text, button, Ink);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void OnGUI()
        {
            SetupGUI();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !showHelp;
            if (State == GameState.Menu) DrawMenu();
            else
            {
                DrawHUD();
                if (State == GameState.Playing) DrawNotice();
                if (State == GameState.Paused) DrawPause();
                if (State == GameState.Won || State == GameState.Lost) DrawResult();
            }
            if (hitOverlay > 0 && State == GameState.Playing) DrawDamageEdges();
            if (State == GameState.Playing) { DrawActorBars(); DrawCrosshair(); }
            GUI.enabled = wasEnabled;
            if (showHelp) DrawHelp();
            if (ControlledByQA && QAIconPreview) DrawIconPreview();
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            foreach (var clip in clips.Values) if (clip) Destroy(clip);
            if (SpriteMaterial) Destroy(SpriteMaterial);
        }
    }
}
