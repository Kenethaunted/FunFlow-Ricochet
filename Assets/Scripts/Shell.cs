using UnityEngine;

namespace FunFlow
{
    /// <summary>Sweeps the full distance each tick and reflects the unused distance at contact.</summary>
    public sealed class Shell : MonoBehaviour
    {
        public int Bounces { get; private set; }
        public bool Active { get; private set; } = true;
        private RicochetGame game;
        private TankActor owner;
        private bool playerShell;
        private Vector2 direction;
        private float lifetime;
        public float Radius { get; private set; } = .09f;
        public int DamageAmount { get; private set; } = 1;
        private float speed;
        private readonly RaycastHit2D[] contacts = new RaycastHit2D[24];
        private ContactFilter2D filter;
        private TrailRenderer trail;

        public void Initialize(RicochetGame manager, TankActor shooter, Vector2 aim)
        {
            game = manager;
            owner = shooter;
            playerShell = shooter.IsPlayer;
            Radius = shooter.IsBoss ? .18f : .09f;
            DamageAmount = shooter.IsBoss ? 2 : 1;
            speed = shooter.IsPlayer ? 12f : (shooter.IsBoss ? 7f : 8f);
            direction = aim.normalized;
            Color color = shooter.IsPlayer ? game.Gold : (shooter.IsBoss ? new Color(1, .4f, .05f) : game.Coral);
            game.Part(transform, "Outline", Vector2.zero, Vector2.one * Radius * 2.5f, game.Ink, 20, true);
            game.Part(transform, "Shell", Vector2.zero, Vector2.one * Radius * 2f, color, 21, true);
            game.Part(transform, "Hot center", Vector2.zero, Vector2.one * Radius, game.Cream, 22, true);
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = .14f;
            trail.startWidth = Radius * 1.3f;
            trail.endWidth = .01f;
            trail.material = game.SpriteMaterial;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0);
            trail.sortingOrder = 19;
            trail.minVertexDistance = .05f;
            filter = new ContactFilter2D();
            filter.SetLayerMask(RicochetGame.WallMask | RicochetGame.TankMask);
            filter.useTriggers = true;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            lifetime += dt;
            if (lifetime > 4.5f) { Retire(); return; }
            float remaining = speed * dt;
            Vector2 position = transform.position;
            for (int iteration = 0; iteration < 8 && remaining > .0001f; iteration++)
            {
                int count = Physics2D.CircleCast(position, Radius, direction, filter, contacts, remaining);
                RaycastHit2D nearest = default;
                float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    TankActor target = contacts[i].collider.GetComponent<TankActor>();
                    // Teams do not damage themselves. Player shells always remain readable and predictable.
                    if (target && (target == owner || !target.Alive || target.IsPlayer == playerShell)) continue;
                    if (contacts[i].distance < nearestDistance) { nearest = contacts[i]; nearestDistance = contacts[i].distance; }
                }
                if (!nearest.collider) { position += direction * remaining; remaining = 0; break; }
                position += direction * Mathf.Max(0, nearest.distance);
                remaining -= Mathf.Max(0, nearest.distance);
                TankActor tank = nearest.collider.GetComponent<TankActor>();
                if (tank)
                {
                    tank.Damage(DamageAmount, Bounces > 0);
                    transform.position = position;
                    Retire(); return;
                }
                if (Bounces >= 3) { game.Burst(position, game.Gold, 3, .7f); Retire(); return; }
                direction = Vector2.Reflect(direction, nearest.normal).normalized;
                position += nearest.normal * .015f;
                remaining = Mathf.Max(0, remaining - .015f);
                Bounces++;
                game.Burst(nearest.point, game.Gold, 4, 1f);
                game.Sound(SoundKind.Bounce);
            }
            transform.position = position;
        }

        private void Retire()
        {
            Active = false;
            if (trail) trail.emitting = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
