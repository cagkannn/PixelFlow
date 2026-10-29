using System;
using System.Collections.Generic;

namespace PixelFlow.Core
{
    [Serializable]
    public sealed class Rules
    {
        public int maxActive = 5;
        public int waitingSlots = 5;
        public double tourDuration = 5;
        public bool strictDock;
        public int queueColumns = 2;

        public Rules Copy()
        {
            return new Rules { maxActive = maxActive, waitingSlots = waitingSlots,
                tourDuration = tourDuration, strictDock = strictDock, queueColumns = queueColumns };
        }
    }

    [Serializable]
    public sealed class ShipDefinition
    {
        public string color;
        public int ammo;
        public int lane = -1;
    }

    // Flat row-major cells are supported by Unity's JsonUtility. Empty strings
    // represent empty cells; the runtime copies them into its own mutable grid.
    [Serializable]
    public sealed class LevelDefinition
    {
        public string id;
        public string title;
        public int width;
        public int height;
        public string[] cells;
        public Rules rules = new Rules();
        public ShipDefinition[] fleet;
        public string provenance;
    }

    [Serializable]
    public sealed class LevelCollection
    {
        public LevelDefinition[] levels;
    }

    public enum GameStatus { Playing, Blocked, Won, Lost }
    public enum GameEventType { Shot, Returned, Depleted, Departed, Blocked, Won, Lost }

    public sealed class Ship
    {
        public string Id;
        public string Color;
        public int Ammo;
        public int InitialAmmo;
        public int Lane;
        public double Progress;
        public double ShotClock;
        public double EntryDelay;
        public double EntryDelayTotal;
        public double BoardingDuration;
        public bool Overfly;
    }

    public sealed class GameState
    {
        public string LevelId;
        public string Title;
        public string Provenance;
        public int Width;
        public int Height;
        public Rules Rules;
        public string[,] Grid;
        public List<Ship> Queue = new List<Ship>();
        public List<Ship> Active = new List<Ship>();
        public Ship[] Waiting;
        public GameStatus Status = GameStatus.Playing;
        public string LossReason;
        public int Total;
        public int Cleared;
        public int Moves;
        public double Elapsed;
        public int OverflyUsed;
        internal double Accumulator;
        public bool HasEnded { get { return Status == GameStatus.Won || Status == GameStatus.Lost; } }
    }

    public sealed class LaunchResult
    {
        public bool Success;
        public string Reason;
        public Ship Ship;
        public bool Ok { get { return Success; } }
    }

    public sealed class GameEvent
    {
        public GameEventType Type;
        public string ShipId;
        public string Color;
        public int Ammo;
        public double Progress;
        public int Row = -1;
        public int Col = -1;
        public bool Overfly;
        public string Source;
        public int Slot = -1;
        public string Reason;
        public string LevelId;
    }

    public struct GridTarget
    {
        public int Row;
        public int Col;
        public GridTarget(int row, int col) { Row = row; Col = col; }
    }

    public struct OrbitPoint
    {
        public double X, Y, Angle, TangentX, TangentY;
        public int Side;
        public bool CanFire;
    }

    public struct OrbitGeometry
    {
        public double Left, Right, Top, Bottom, Radius, Perimeter, Gap, TravelLength;
    }

    public static class GameColors
    {
        public static readonly string[] Names = { "purple", "yellow", "cyan", "pink", "blue", "green", "white", "black", "lavender" };
        public static readonly string[] Hex = { "#b600ee", "#ffdb00", "#00ddeb", "#f40091", "#2374f5", "#36e616", "#fff9f0", "#30282c", "#d393f6" };
        public static bool IsKnown(string value) { return Array.IndexOf(Names, value) >= 0; }
        public static string HexFor(string value)
        {
            int index = Array.IndexOf(Names, value);
            return index < 0 ? Hex[0] : Hex[index];
        }
        public static string TurkishName(string value)
        {
            string[] names = { "Mor", "Sarı", "Turkuaz", "Pembe", "Mavi", "Yeşil", "Beyaz", "Siyah", "Lavanta" };
            int index = Array.IndexOf(Names, value);
            return index < 0 ? value : names[index];
        }
    }
}
