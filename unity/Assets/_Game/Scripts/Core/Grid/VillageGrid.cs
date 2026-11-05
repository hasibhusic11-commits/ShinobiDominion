using System;
using System.Collections.Generic;

namespace ShinobiDominion.Core
{
    /// <summary>A cell on the village grid. X and Z are each 0..49 when in bounds.</summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Z;

        public GridCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => $"({X},{Z})";
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }

    /// <summary>Quarter-turn building rotation. Serialized as its integer value.</summary>
    public enum Rotation
    {
        R0 = 0,
        R90 = 1,
        R180 = 2,
        R270 = 3
    }

    public static class RotationExtensions
    {
        public static Rotation Next(this Rotation rotation) => (Rotation)(((int)rotation + 1) % 4);
        public static bool SwapsAxes(this Rotation rotation) => rotation == Rotation.R90 || rotation == Rotation.R270;
    }

    /// <summary>
    /// The rectangle of cells a building covers. Origin is always the minimum (x,z) corner,
    /// whatever the rotation, so rotation only ever swaps width and depth.
    /// </summary>
    public readonly struct Footprint
    {
        public readonly GridCoord Origin;
        public readonly int Width;
        public readonly int Depth;

        public Footprint(GridCoord origin, int width, int depth)
        {
            if (width <= 0 || depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Footprint dimensions must be positive.");
            Origin = origin;
            Width = width;
            Depth = depth;
        }

        public static Footprint Of(BuildingDefinition definition, GridCoord origin, Rotation rotation)
        {
            return rotation.SwapsAxes()
                ? new Footprint(origin, definition.depth, definition.width)
                : new Footprint(origin, definition.width, definition.depth);
        }

        public bool InBounds =>
            VillageGrid.InBounds(Origin.X, Origin.Z) &&
            VillageGrid.InBounds(Origin.X + Width - 1, Origin.Z + Depth - 1);

        public bool Contains(GridCoord cell) =>
            cell.X >= Origin.X && cell.X < Origin.X + Width &&
            cell.Z >= Origin.Z && cell.Z < Origin.Z + Depth;

        public IEnumerable<GridCoord> Cells()
        {
            for (var z = Origin.Z; z < Origin.Z + Depth; z++)
            for (var x = Origin.X; x < Origin.X + Width; x++)
                yield return new GridCoord(x, z);
        }
    }

    /// <summary>
    /// Compact occupancy for the 50 x 50 buildable map. Two flat arrays rather than per-cell
    /// objects. Map origin: cell (0,0) spans world (0,0) to (cellSize,cellSize).
    /// </summary>
    public sealed class VillageGrid
    {
        public const int Size = 50;
        public const int CellCount = Size * Size;
        public const int NoOccupant = 0;

        private readonly int[] _occupants = new int[CellCount];
        private readonly bool[] _blocked = new bool[CellCount];

        public static bool InBounds(int x, int z) => x >= 0 && x < Size && z >= 0 && z < Size;
        public static bool InBounds(GridCoord cell) => InBounds(cell.X, cell.Z);

        private static int Index(GridCoord cell)
        {
            if (!InBounds(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), $"{cell} is outside the {Size}x{Size} map.");
            return cell.Z * Size + cell.X;
        }

        public int OccupantAt(GridCoord cell) => _occupants[Index(cell)];
        public bool IsBlocked(GridCoord cell) => _blocked[Index(cell)];
        public void SetBlocked(GridCoord cell, bool blocked) => _blocked[Index(cell)] = blocked;

        internal void Fill(Footprint footprint, int instanceId)
        {
            foreach (var cell in footprint.Cells())
                _occupants[Index(cell)] = instanceId;
        }

        internal void Clear(Footprint footprint)
        {
            foreach (var cell in footprint.Cells())
                _occupants[Index(cell)] = NoOccupant;
        }

        internal void ClearAll() => Array.Clear(_occupants, 0, _occupants.Length);
    }
}
