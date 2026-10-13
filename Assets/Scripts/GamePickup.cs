using UnityEngine;

namespace FunFlow
{
    public enum PickupKind { Medkit, Shield }

    public sealed class GamePickup : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        public bool Active { get; private set; } = true;
        public float Lifetime { get; private set; } = 18f;
        private RicochetGame game;
        private Transform visual;
        private float phase;

        public void Initialize(RicochetGame manager, PickupKind kind)
        {
            game = manager;
            Kind = kind;
            phase = Random.Range(0f, 6f);
            visual = new GameObject("Pickup icon").transform;
            visual.SetParent(transform, false);
            Color accent = kind == PickupKind.Medkit ? game.Coral : new Color(.05f, .47f, 1f);
            game.Part(transform, "Pickup shadow", new Vector2(.05f, -.1f), new Vector2(.72f, .64f), new Color(game.Ink.r, game.Ink.g, game.Ink.b, .2f), 8, true);
            game.Part(visual, "Pickup outline", Vector2.zero, Vector2.one * .7f, game.Ink, 30);
            game.Part(visual, "Pickup card", Vector2.zero, Vector2.one * .61f, game.Cream, 31);
            var icon = game.Part(visual, "Universal pickup symbol", Vector2.zero, Vector2.one * .5f, accent, 32);
            icon.sprite = game.IconSprite(kind == PickupKind.Medkit ? UiIcon.Medkit : UiIcon.Shield);
            game.Part(visual, "Top highlight", new Vector2(0, .29f), new Vector2(.52f, .035f), Color.white, 33);
        }

        public void Tick(float dt)
        {
            if (!Active || game.State != GameState.Playing) return;
            Lifetime -= dt;
            if (Lifetime <= 0) { Retire(); return; }
            visual.localPosition = new Vector3(0, Mathf.Sin(Time.time * 3 + phase) * .065f, 0);
            visual.localScale = Vector3.one * (Lifetime < 3 ? .94f + Mathf.Sin(Time.time * 15) * .06f : 1f);
            if (game.Player && Vector2.Distance(game.Player.transform.position, transform.position) < .65f) TryCollect(game.Player);
        }

        public bool TryCollect(TankActor collector)
        {
            if (!Active || game.State != GameState.Playing || !collector || !collector.Alive || !collector.IsPlayer) return false;
            int previousHealth = collector.Health;
            if (Kind == PickupKind.Medkit)
            {
                if (!collector.Heal(2)) return false;
            }
            else collector.GrantShield(8f);
            game.OnPickupCollected(Kind, transform.position, collector.Health - previousHealth);
            Retire();
            return true;
        }

        public void Retire()
        {
            if (!Active) return;
            Active = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
