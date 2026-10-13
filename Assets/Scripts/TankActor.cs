using UnityEngine;

namespace FunFlow
{
    /// <summary>A tank slides around rounded contact geometry; hull and turret turn independently.</summary>
    public sealed class TankActor : MonoBehaviour
    {
        public bool IsPlayer { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool Alive => Health > 0;
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public bool IsBoss => EnemyTier >= 3;
        public float Radius => IsBoss ? .56f : .43f;
        private Color TankColor => IsPlayer ? game.Teal : (IsBoss ? new Color(1f, .47f, .05f) : (EnemyTier >= 2 ? new Color(1f, .67f, .24f) : game.Coral));
        public CircleCollider2D Hitbox { get; private set; }
        public int EnemyTier { get; private set; }
        private RicochetGame game;
        private Transform hull, turret;
        private SpriteRenderer body;
        private float cooldown, thinkTimer, flashTimer, safeUntil;
        private Vector2 travelDirection;
        private float stuckTimer;
        private Vector2 previousPosition;
        private float orbitSide;
        private Transform recoilBarrel;
        private float recoil;
        private SpriteRenderer armorGlow;
        private SpriteRenderer shieldAura, shieldFill;
        public float ShieldRemaining { get; private set; }
        public bool Shielded => IsPlayer && ShieldRemaining > 0;

        public void Initialize(RicochetGame owner, bool player, int tier)
        {
            game = owner;
            IsPlayer = player;
            EnemyTier = tier;
            MaxHealth = player ? 5 : (IsBoss ? 5 + owner.Wave : (tier >= 2 ? 2 : 1));
            Health = MaxHealth;
            safeUntil = Time.time + (player ? 0.8f : 1.2f);
            gameObject.layer = 9;
            Hitbox = gameObject.AddComponent<CircleCollider2D>();
            Hitbox.radius = Radius;
            Hitbox.isTrigger = true;
            if (player)
            {
                shieldFill = game.Part(transform, "Shield glow", Vector2.zero, Vector2.one * 1.5f, new Color(.1f, .6f, 1f, 0), 8, true);
                shieldAura = game.Part(transform, "Shield rim", Vector2.zero, Vector2.one * 1.65f, new Color(.1f, .6f, 1f, 0), 19);
                shieldAura.sprite = game.IconSprite(UiIcon.Ring);
            }
            hull = new GameObject("Hull").transform;
            hull.SetParent(transform, false);
            Color color = TankColor;
            if (IsBoss) hull.localScale = Vector3.one * 1.3f;
            game.Part(hull, "Shadow", new Vector2(0.08f, -0.1f), new Vector2(0.92f, 1.05f), new Color(0, 0, 0, 0.3f), 9);
            game.Part(hull, "Left track", new Vector2(-0.34f, 0), new Vector2(0.22f, 0.96f), game.Ink, 10);
            game.Part(hull, "Right track", new Vector2(0.34f, 0), new Vector2(0.22f, 0.96f), game.Ink, 10);
            for (int i = 0; i < 5; i++)
            {
                float y = -0.36f + i * 0.18f;
                game.Part(hull, "Tread", new Vector2(-0.34f, y), new Vector2(0.2f, 0.06f), game.Slate, 11);
                game.Part(hull, "Tread", new Vector2(0.34f, y), new Vector2(0.2f, 0.06f), game.Slate, 11);
            }
            body = game.Part(hull, "Armor", Vector2.zero, new Vector2(0.61f, 0.8f), color, 12);
            game.Part(hull, "Front plate", new Vector2(0, 0.3f), new Vector2(0.5f, 0.1f), Color.Lerp(color, Color.white, 0.35f), 13);
            game.Part(hull, "Rear plate", new Vector2(0, -0.29f), new Vector2(0.4f, 0.08f), Color.Lerp(color, Color.black, 0.3f), 13);
            game.Part(hull, "Armor bevel left", new Vector2(-.27f, 0), new Vector2(.055f, .65f), Color.Lerp(color, Color.white, .4f), 13);
            for (int i = 0; i < 3; i++) game.Part(hull, "Cooling vent", new Vector2(-.12f + i * .12f, -.23f), new Vector2(.05f, .13f), game.Ink, 14);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
                game.Part(hull, "Armor rivet", new Vector2(x * .22f, y * .29f), Vector2.one * .05f, new Color(.72f, .8f, .8f), 14, true);
            armorGlow = game.Part(hull, "Damage armor glow", Vector2.zero, new Vector2(.72f, .89f), new Color(1, 1, 1, 0), 18);
            turret = new GameObject("Turret").transform;
            turret.SetParent(transform, false);
            if (IsBoss) turret.localScale = Vector3.one * 1.3f;
            game.Part(turret, "Barrel outline", new Vector2(0, 0.47f), new Vector2(0.18f, 0.67f), game.Ink, 14);
            recoilBarrel = game.Part(turret, "Barrel", new Vector2(0, 0.48f), new Vector2(0.1f, 0.58f), color, 15).transform;
            game.Part(turret, "Muzzle collar", new Vector2(0, .72f), new Vector2(.2f, .12f), new Color(.75f, .83f, .82f), 16);
            game.Part(turret, "Turret", Vector2.zero, new Vector2(0.4f, 0.4f), Color.Lerp(color, Color.black, 0.2f), 16, true);
            game.Part(turret, "Hatch", new Vector2(-0.04f, -0.04f), new Vector2(0.16f, 0.16f), Color.Lerp(color, Color.white, 0.5f), 17, true);
            if (IsBoss)
            {
                game.Part(hull, "Boss stripe", new Vector2(0, .08f), new Vector2(.55f, .1f), game.Cream, 14);
                game.Part(turret, "Boss crown", new Vector2(0, .04f), new Vector2(.23f, .07f), game.Gold, 18);
            }
            cooldown = player ? 0 : Random.Range(1.2f, 2f);
            previousPosition = transform.position;
            orbitSide = Random.value < .5f ? -1f : 1f;
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            ShieldRemaining = Mathf.Max(0, ShieldRemaining - dt);
            if (shieldAura)
            {
                float alpha = Shielded ? (ShieldRemaining < 2 ? .45f + Mathf.Sin(Time.time * 18) * .35f : .9f) : 0;
                shieldAura.color = new Color(.05f, .45f, 1f, alpha);
                shieldFill.color = new Color(.1f, .65f, 1f, alpha * .14f);
            }
            cooldown -= dt;
            flashTimer -= dt;
            recoil = Mathf.MoveTowards(recoil, 0, dt * .7f);
            recoilBarrel.localPosition = new Vector3(0, .48f - recoil, 0);
            armorGlow.color = new Color(1, .83f, .53f, Mathf.Clamp01(flashTimer * 4f));
            Color normal = TankColor;
            body.color = flashTimer > 0 ? Color.white : normal;
            if (IsPlayer)
            {
                if (game.ControlledByQA) return;
                Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                Move(Vector2.ClampMagnitude(move, 1f), 4.2f, dt);
                Vector2 mouse = game.WorldCamera.ScreenToWorldPoint(Input.mousePosition);
                Aim(mouse - (Vector2)transform.position);
                if (Input.GetMouseButton(0) && game.PointerInArena()) Fire();
            }
            else TickEnemy(dt);
        }

        private void TickEnemy(float dt)
        {
            if (!game.Player || !game.Player.Alive) return;
            thinkTimer -= dt;
            Vector2 toPlayer = game.Player.transform.position - transform.position;
            if (thinkTimer <= 0)
            {
                thinkTimer = Random.Range(0.35f, 0.65f);
                // A direct shot or a single bank shot; no projectile can pass through cover.
                Vector2 shot = game.FindShot(transform.position, game.Player.transform.position);
                if (shot != Vector2.zero) Aim(shot);
                else Aim(toPlayer);
                Vector2 desired = toPlayer.normalized;
                if (toPlayer.magnitude < 3f) desired = -desired;
                else if (toPlayer.magnitude < 5.5f) desired = new Vector2(-desired.y, desired.x) * orbitSide;
                travelDirection = desired;
                RaycastHit2D obstacle = Physics2D.CircleCast(transform.position, Radius + .06f, desired, 1.25f, RicochetGame.WallMask);
                if (obstacle.collider)
                {
                    Vector2 left = new Vector2(-desired.y, desired.x);
                    Vector2 right = -left;
                    float l = game.FreeDistance(transform.position, left, 2f);
                    float r = game.FreeDistance(transform.position, right, 2f);
                    travelDirection = l > r ? left : right;
                }
            }
            Move(travelDirection, IsBoss ? 1.45f : 1.3f + EnemyTier * .18f, dt);
            if (Vector2.Distance(previousPosition, transform.position) < .005f) stuckTimer += dt;
            else stuckTimer = 0;
            previousPosition = transform.position;
            if (stuckTimer > .5f) { travelDirection = Quaternion.Euler(0, 0, 90) * travelDirection; stuckTimer = 0; }
            if (cooldown <= 0 && Time.time >= safeUntil)
            {
                Vector2 shot = game.FindShot(transform.position, game.Player.transform.position);
                if (shot != Vector2.zero) { Aim(shot); Fire(); }
                else cooldown = .3f;
            }
        }

        public void Aim(Vector2 direction)
        {
            if (direction.sqrMagnitude < .001f) return;
            AimDirection = direction.normalized;
            turret.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-AimDirection.x, AimDirection.y) * Mathf.Rad2Deg);
        }

        public void Move(Vector2 direction, float speed, float dt)
        {
            if (direction.sqrMagnitude < .01f) return;
            Vector2 position = game.MoveTankCircle(transform.position, direction * speed * dt, Radius);
            // Keep tanks separate, including spawn points and moving enemy groups.
            foreach (TankActor other in game.Tanks)
            {
                if (!other || other == this || !other.Alive) continue;
                Vector2 gap = position - (Vector2)other.transform.position;
                float distance = gap.magnitude;
                if (distance < Radius * 2f && distance > .001f)
                {
                    Vector2 correction = gap.normalized * (Radius * 2f - distance);
                    position = game.MoveTankCircle(position, correction, Radius);
                }
            }
            transform.position = position;
            float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            hull.rotation = Quaternion.RotateTowards(hull.rotation, Quaternion.Euler(0, 0, angle), 500f * dt);
        }

        public bool Fire()
        {
            if (cooldown > 0 || !Alive || game.State != GameState.Playing) return false;
            float muzzle = IsBoss ? .90f : .69f;
            Vector2 origin = (Vector2)transform.position + AimDirection * muzzle;
            // A barrel touching a wall must not create a shell on its far side.
            RaycastHit2D wall = Physics2D.Raycast(transform.position, AimDirection, muzzle + .1f, RicochetGame.WallMask);
            if (wall.collider) origin = (Vector2)transform.position + AimDirection * Mathf.Max(.05f, wall.distance - .13f);
            game.SpawnShell(this, origin, AimDirection);
            recoil = .1f;
            cooldown = IsPlayer ? .32f : (IsBoss ? 1.9f : Mathf.Max(1.3f, 2.5f - game.Wave * .15f)) + Random.Range(0, .45f);
            game.Burst(origin, IsPlayer ? game.Gold : game.Coral, 5, .8f);
            game.Sound(IsPlayer ? SoundKind.Shot : SoundKind.EnemyShot);
            return true;
        }

        public void Damage(int amount, bool bankShot)
        {
            if (Alive && Shielded)
            {
                game.Burst(transform.position, new Color(.1f, .7f, 1f), 9, 2f);
                game.Sound(SoundKind.Bounce);
                return;
            }
            if (!Alive || Time.time < safeUntil) return;
            Health = Mathf.Max(0, Health - amount);
            flashTimer = .13f;
            if (IsPlayer)
            {
                safeUntil = Time.time + .85f;
                game.Shake(.23f);
                game.ArmorSparks(transform.position);
                game.Sound(SoundKind.Hurt);
            }
            else game.Sound(SoundKind.Hit);
            game.Burst(transform.position, IsPlayer ? game.Teal : game.Coral, 10, 2f);
            if (!Alive)
            {
                Hitbox.enabled = false;
                game.OnTankDestroyed(this, bankShot);
                Destroy(gameObject);
            }
        }

        public bool Heal(int amount)
        {
            if (!IsPlayer || !Alive || Health >= MaxHealth) return false;
            Health = Mathf.Min(MaxHealth, Health + Mathf.Max(0, amount));
            return true;
        }

        public void GrantShield(float duration)
        {
            if (IsPlayer && Alive) ShieldRemaining = Mathf.Max(0, duration);
        }
    }
}
