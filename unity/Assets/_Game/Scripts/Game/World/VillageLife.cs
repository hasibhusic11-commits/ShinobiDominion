using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Little figures walking around the village, so it feels lived in. Purely for show: they read the
    /// layout, use their own random numbers, and nothing in the game's rules knows they exist.
    /// </summary>
    public sealed class VillageLife : MonoBehaviour
    {
        private const float WalkSpeed = 0.9f;
        private const float Scale = 1.1f;
        private const int MaxWalkers = 18;

        private sealed class Walker
        {
            public Transform Body;
            public int X;
            public int Z;
            public int GoalX;
            public int GoalZ;
            public Vector3 Next;
            public float Wait;
            public float Phase;
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private Village _village;
        private System.Random _random;
        private Mesh _mesh;

        public int WalkerCount => _walkers.Count;

        public void Initialize(Village village, string troopId, int count)
        {
            _village = village;
            _random = new System.Random(12345);
            _mesh = BuildingMeshes.Troop(troopId).Build("VillageWalker");
            for (var i = 0; i < Mathf.Min(count, MaxWalkers); i++) AddWalker();
        }

        /// <summary>Walkers gather near buildings, not out in the empty grass.</summary>
        private void PickGoal(Walker walker)
        {
            var buildings = _village.Buildings.ToList();
            if (buildings.Count == 0) return;
            var near = buildings[_random.Next(buildings.Count)];
            for (var tries = 0; tries < 12; tries++)
            {
                var x = near.Origin.X + _random.Next(-3, 7);
                var z = near.Origin.Z + _random.Next(-3, 7);
                if (!Free(x, z)) continue;
                walker.GoalX = x;
                walker.GoalZ = z;
                return;
            }
        }

        private bool Free(int x, int z)
        {
            return VillageGrid.InBounds(x, z) && _village.Grid.OccupantAt(new GridCoord(x, z)) == VillageGrid.NoOccupant;
        }

        private void AddWalker()
        {
            var go = new GameObject("VillageWalker");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Scale;
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var walker = new Walker { Body = go.transform, Phase = (float)_random.NextDouble() * 6f, Wait = (float)_random.NextDouble() * 3f };
            for (var tries = 0; tries < 40; tries++)
            {
                walker.X = _random.Next(VillageGrid.Size);
                walker.Z = _random.Next(VillageGrid.Size);
                if (Free(walker.X, walker.Z)) break;
            }
            // Start beside a building, not out in the empty grass.
            PickGoal(walker);
            if (Free(walker.GoalX, walker.GoalZ))
            {
                walker.X = walker.GoalX;
                walker.Z = walker.GoalZ;
            }
            PickGoal(walker);
            walker.Next = new Vector3(walker.X + 0.5f, 0f, walker.Z + 0.5f);
            go.transform.localPosition = walker.Next;
            _walkers.Add(walker);
        }

        private void Update()
        {
            foreach (var walker in _walkers)
            {
                if (walker.Wait > 0f)
                {
                    walker.Wait -= Time.deltaTime;
                    continue;
                }
                var position = walker.Body.localPosition;
                position.y = 0f;
                var moved = Vector3.MoveTowards(position, walker.Next, WalkSpeed * Time.deltaTime);
                var heading = walker.Next - position;
                // Turns at a corner over a few frames rather than snapping round.
                if (heading.sqrMagnitude > 0.0001f)
                    walker.Body.localRotation = Quaternion.RotateTowards(walker.Body.localRotation, Quaternion.LookRotation(-heading.normalized), 720f * Time.deltaTime);
                walker.Phase += Time.deltaTime * 11f;
                moved.y = Mathf.Abs(Mathf.Sin(walker.Phase)) * 0.05f;
                walker.Body.localPosition = moved;
                if ((new Vector3(moved.x, 0f, moved.z) - walker.Next).sqrMagnitude < 0.0001f) Step(walker);
            }
        }

        /// <summary>One cell closer to the goal. A blocked or finished walker rests, then heads somewhere new.</summary>
        private void Step(Walker walker)
        {
            var dx = System.Math.Sign(walker.GoalX - walker.X);
            var dz = System.Math.Sign(walker.GoalZ - walker.Z);
            if (dx != 0 && Free(walker.X + dx, walker.Z)) walker.X += dx;
            else if (dz != 0 && Free(walker.X, walker.Z + dz)) walker.Z += dz;
            else
            {
                walker.Wait = 1f + (float)_random.NextDouble() * 4f;
                PickGoal(walker);
                return;
            }
            walker.Next = new Vector3(walker.X + 0.5f, 0f, walker.Z + 0.5f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
