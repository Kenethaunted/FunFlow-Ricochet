using System.Collections.Generic;
using UnityEngine;

namespace FunFlow
{
    public sealed partial class RicochetGame
    {
        public List<GamePickup> Pickups { get; } = new List<GamePickup>();
        private float medkitClock, shieldClock, pickupToastTime;
        private PickupKind pickupToastKind;
        private int pickupHealthRestored;

        private void ResetPickupSchedule()
        {
            medkitClock = Random.Range(18f, 26f);
            shieldClock = Random.Range(26f, 36f);
            pickupToastTime = 0;
        }

        private void ClearPickups()
        {
            foreach (var item in Pickups) if (item) item.Retire();
            Pickups.Clear();
        }

        public GamePickup SpawnPickup(PickupKind kind, Vector2 position)
        {
            if (!TankPositionClear(position, .6f)) return null;
            var obj = new GameObject(kind == PickupKind.Medkit ? "Medkit" : "Shield pickup");
            obj.transform.SetParent(actors);
            obj.transform.position = position;
            var item = obj.AddComponent<GamePickup>();
            item.Initialize(this, kind);
            Pickups.Add(item);
            Burst(position, kind == PickupKind.Medkit ? Coral : new Color(.1f, .6f, 1f), 8, .8f);
            return item;
        }

        public bool TrySpawnRarePickup(PickupKind kind)
        {
            foreach (var item in Pickups) if (item && item.Active && item.Kind == kind) return false;
            for (int i = 0; i < 40; i++)
            {
                Vector2 position = new Vector2(Random.Range(-10.1f, 10.1f), Random.Range(-4.8f, 4.8f));
                if (!TankPositionClear(position, .7f) || (Player && Vector2.Distance(position, Player.transform.position) < 2)) continue;
                bool occupied = false;
                foreach (var tank in Tanks) if (tank && tank.Alive && Vector2.Distance(position, tank.transform.position) < 1.2f) occupied = true;
                foreach (var item in Pickups) if (item && item.Active && Vector2.Distance(position, item.transform.position) < 1.5f) occupied = true;
                if (occupied) continue;
                return SpawnPickup(kind, position) != null;
            }
            return false;
        }

        public void TickPickups(float dt)
        {
            if (State != GameState.Playing) return;
            for (int i = Pickups.Count - 1; i >= 0; i--)
            {
                if (!Pickups[i] || !Pickups[i].Active) { Pickups.RemoveAt(i); continue; }
                Pickups[i].Tick(dt);
            }
            medkitClock -= dt;
            shieldClock -= dt;
            pickupToastTime = Mathf.Max(0, pickupToastTime - dt);
            if (medkitClock <= 0) { TrySpawnRarePickup(PickupKind.Medkit); medkitClock = Random.Range(24f, 38f); }
            if (shieldClock <= 0) { TrySpawnRarePickup(PickupKind.Shield); shieldClock = Random.Range(32f, 46f); }
        }

        public void OnPickupCollected(PickupKind kind, Vector2 position, int restoredHealth)
        {
            pickupToastKind = kind;
            pickupHealthRestored = restoredHealth;
            pickupToastTime = .9f;
            Burst(position, kind == PickupKind.Medkit ? Teal : new Color(.1f, .6f, 1f), 18, 2.2f);
            Sound(SoundKind.Pickup);
        }
    }
}
