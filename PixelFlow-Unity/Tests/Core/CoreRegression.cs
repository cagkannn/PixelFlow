using System;
using System.Collections.Generic;
using PixelFlow.Core;

namespace PixelFlow.Tests
{
    // A console host can call Run after reading the exported reference JSON.
    // These checks do not need Unity or an engine licence.
    public static class CoreRegression
    {
        private static int checks;
        public static int Run(LevelDefinition[] levels)
        {
            checks = 0;
            Require(levels != null && levels.Length == 6, "Six reference levels are exported.");
            foreach (LevelDefinition definition in levels) SolveReference(definition);
            DepletionFreesCapacity(false); DepletionFreesCapacity(true);
            QueueAndPowerRules(); LastShotWins(); EnergyLoss(); RayOcclusion(); Determinism(levels[2]);
            ReturnedArrival(); GeometryContinuity(); DataValidation(levels[0]); RemainingFleetSpeed();
            Console.WriteLine("PASS: " + checks + " C# core checks, all six reference levels solved.");
            return checks;
        }

        private static void SolveReference(LevelDefinition definition)
        {
            GameState game = GameEngine.Create(definition);
            int expectedShips = game.Queue.Count, shots = 0, departures = 0, reorder = 0;
            var spent = new HashSet<string>();
            Dictionary<string, int> counts = GameEngine.RemainingByColor(game);
            foreach (KeyValuePair<string, int> pair in counts) {
                int energy = 0; foreach (Ship ship in game.Queue) if (ship.Color == pair.Key) energy += ship.Ammo;
                Require(energy == pair.Value, "Reference " + definition.id + " has exact " + pair.Key + " energy.");
            }
            for (int tick = 0; tick < 20000 && !game.HasEnded; tick++) {
                if (game.Status == GameStatus.Blocked) { GameEngine.RestoreWaiting(game); reorder++; }
                HashSet<string> exposed = ExposedColors(game);
                while (game.Active.Count < game.Rules.maxActive) {
                    List<Ship> available = GameEngine.AvailableShips(game);
                    Ship propeller = definition.id == "5" && game.OverflyUsed == 0 ? available.Find(ship => ship.Color == "cyan") : null;
                    Ship chosen = propeller ?? available.Find(ship => exposed.Contains(ship.Color));
                    if (chosen == null && game.Active.Count == 0) chosen = available.Find(ship => game.Queue.Contains(ship));
                    if (chosen == null) break;
                    Require(GameEngine.Launch(game, chosen.Id, propeller != null).Success, "A legal reference choice succeeds.");
                }
                foreach (GameEvent e in GameEngine.Advance(game, .1)) {
                    if (e.Type == GameEventType.Shot) shots++;
                    if (e.Type == GameEventType.Depleted) {
                        Require(e.Ammo == 0, "A depleted UFO has no ammunition.");
                        Require(!game.Active.Exists(ship => ship.Id == e.ShipId), "An empty UFO has already disappeared.");
                        Require(spent.Add(e.ShipId), "A UFO depletes exactly once.");
                    }
                    if (e.Type == GameEventType.Departed) departures++;
                }
                Require(game.Active.Count <= 5 && game.Active.TrueForAll(ship => ship.Ammo > 0), "Only five positive-energy UFOs may remain on the board.");
            }
            Require(game.Status == GameStatus.Won, "Reference " + definition.id + " must complete, got " + game.Status + ": " + game.LossReason);
            Require(game.Cleared == game.Total && shots == game.Total, "Every reference square is hit exactly once.");
            Require(spent.Count == expectedShips && departures == 0, "Every allocated UFO really spends its shots.");
            Require(game.Queue.Count == 0 && game.Active.Count == 0 && Array.TrueForAll(game.Waiting, ship => ship == null), "No unused UFO remains at a reference win.");
            Require(reorder == 0, "A reference solution does not need hidden rearrangement.");
            if (definition.id == "5") Require(game.OverflyUsed == 1, "The fifth reference uses its cyan propeller tutorial.");
            Console.WriteLine("PASS reference " + definition.id + ": " + game.Total + " cells, " + spent.Count + " UFOs, " + game.Elapsed.ToString("F2") + " simulation seconds.");
        }

        private static void DepletionFreesCapacity(bool overfly)
        {
            var fleet = new List<ShipDefinition> { ShipDef("purple", 1) };
            for (int i = 0; i < 4; i++) fleet.Add(ShipDef("blue", 999));
            fleet.Add(ShipDef("yellow", 1));
            GameState game = GameEngine.Create(Level(2, 1, new[] { "purple", "yellow" }, fleet.ToArray()));
            Ship first = game.Queue[0]; string sixth = game.Queue[5].Id;
            Require(GameEngine.Launch(game, first.Id, overfly).Success, "First launch succeeds.");
            for (int i = 0; i < 4; i++) Require(GameEngine.Launch(game, game.Queue[0].Id).Success, "Rapid launches are accepted without click delay.");
            Require(!GameEngine.Launch(game, sixth).Success, "A sixth active UFO cannot launch.");
            List<GameEvent> events = GameEngine.Advance(game, 1);
            int shot = events.FindIndex(e => e.Type == GameEventType.Shot && e.ShipId == first.Id);
            Require(shot >= 0 && shot + 1 < events.Count, "A last projectile event is preserved.");
            GameEvent depleted = events[shot + 1];
            Require(depleted.Type == GameEventType.Depleted && depleted.ShipId == first.Id && depleted.Ammo == 0, "Last shot is followed immediately by depletion.");
            Require(depleted.Progress == events[shot].Progress && depleted.Progress < 1 && depleted.Overfly == overfly, "Depletion keeps its real firing position and mode.");
            Require(!game.Active.Contains(first) && game.Active.Count == 4, "The empty UFO is removed in the same step.");
            Require(GameEngine.RemainingShipCount(game) == 5 && GameEngine.EndgameSpeed(game) == 2, "The last-five phase excludes the disappeared UFO.");
            Require(GameEngine.Launch(game, sixth).Success && game.Active.Count == 5, "The released slot can be reused immediately.");
            Require(!GameEngine.Advance(game, 1).Exists(e => e.ShipId == first.Id), "No later ghost shot or departure is emitted.");
        }

        private static void QueueAndPowerRules()
        {
            var level = Level(1, 1, new[] { "purple" }, new[] { ShipDef("blue", 10), ShipDef("yellow", 10), ShipDef("purple", 1) });
            level.rules.queueColumns = 2;
            GameState game = GameEngine.Create(level);
            Require(GameEngine.AvailableShips(game).Count == 2, "Only the front UFO in each lane is available.");
            Require(!GameEngine.Launch(game, "ship-3").Success && game.Moves == 0, "A hidden same-lane UFO is rejected without mutation.");
            Require(!GameEngine.Launch(game, "missing", true).Success && game.OverflyUsed == 0, "A failed power launch does not spend the power.");
            Require(GameEngine.Launch(game, "ship-1", true).Success && game.OverflyUsed == 1, "One airborne power is available.");
            Require(!GameEngine.Launch(game, "ship-2", true).Success && game.Queue.Count == 2, "A second airborne power is rejected without removing its UFO.");
            Require(GameEngine.Launch(game, "ship-3").Success, "Launching the lane front exposes its next UFO.");
        }

        private static void LastShotWins()
        {
            GameState game = GameEngine.Create(Level(1, 1, new[] { "purple" }, new[] { ShipDef("purple", 1) }));
            GameEngine.Launch(game, "ship-1"); List<GameEvent> events = GameEngine.Advance(game, 1);
            Require(events.Count == 3 && events[0].Type == GameEventType.Shot && events[1].Type == GameEventType.Depleted && events[2].Type == GameEventType.Won, "The final shot retires its UFO before the completion event.");
            Require(game.Status == GameStatus.Won && game.Active.Count == 0, "Win takes precedence over exhausted energy.");
        }

        private static void EnergyLoss()
        {
            GameState game = GameEngine.Create(Level(2, 1, new[] { "purple", "purple" }, new[] { ShipDef("purple", 1) }));
            GameEngine.Launch(game, "ship-1"); List<GameEvent> events = GameEngine.Advance(game, 1);
            Require(game.Status == GameStatus.Lost && events.Exists(e => e.Type == GameEventType.Lost), "A designed insufficient fleet fails when its last matching energy runs out.");
        }

        private static void RayOcclusion()
        {
            GameState game = GameEngine.Create(Level(1, 2, new[] { "yellow", "purple" }, new[] { ShipDef("purple", 1), ShipDef("yellow", 1) }));
            double length = GameEngine.GetOrbitGeometry(game).TravelLength;
            Require(!GameEngine.FindTarget(game, "yellow", .5 / length).HasValue, "A shot cannot pass through another colour.");
            GridTarget? target = GameEngine.FindTarget(game, "purple", .5 / length);
            Require(target.HasValue && target.Value.Row == 1 && target.Value.Col == 0, "Bottom shooting reaches the first occupied cell.");
            double topDistance = game.Width + game.Height + Math.PI * GameEngine.OrbitPadding + .5;
            target = GameEngine.FindTarget(game, "yellow", topDistance / length);
            Require(target.HasValue && target.Value.Row == 0, "The opposite straight can fire at the exposed top cell.");
        }

        private static void Determinism(LevelDefinition definition)
        {
            GameState a = GameEngine.Create(definition), b = GameEngine.Create(definition);
            for (int i = 0; i < 3; i++) { GameEngine.Launch(a, a.Queue[0].Id); GameEngine.Launch(b, b.Queue[0].Id); }
            for (int i = 0; i < 700; i++) GameEngine.Advance(a, .01);
            for (int i = 0; i < 70; i++) GameEngine.Advance(b, .1);
            Require(a.Cleared == b.Cleared && a.Active.Count == b.Active.Count && a.Queue.Count == b.Queue.Count, "Frame sizes preserve the same outcome.");
            for (int row = 0; row < a.Height; row++) for (int col = 0; col < a.Width; col++) Require(a.Grid[row, col] == b.Grid[row, col], "Frame sizes preserve every grid cell.");
            Require(Math.Abs(a.Elapsed - b.Elapsed) < 1e-9, "The fixed simulation clock is frame-rate independent.");
        }

        private static void ReturnedArrival()
        {
            GameState game = GameEngine.Create(Level(1, 1, new[] { "purple" }, new[] { ShipDef("yellow", 3), ShipDef("purple", 1) }));
            GameEngine.Launch(game, "ship-1", true);
            List<GameEvent> events = GameEngine.Advance(game, 3.3);
            GameEvent arrived = events.Find(e => e.Type == GameEventType.Returned);
            Require(arrived != null && arrived.Overfly && arrived.Progress >= 1 - 1e-9 && arrived.Ammo == 3 && arrived.Slot == 0, "Arrival remembers final position, mode and unspent energy.");
            Require(game.Waiting[0] != null && !game.Waiting[0].Overfly && game.Waiting[0].Progress == 0, "An airborne survivor returns as a ground UFO.");
            Require(GameEngine.RestoreWaiting(game) == 1 && game.Queue[0].Id == "ship-1", "Queue restoration preserves the returning UFO and all energy.");
        }

        private static void GeometryContinuity()
        {
            GameState game = GameEngine.Create(Level(3, 4, new string[] { "purple", null, null, null, null, null, null, null, null, null, null, null }, new[] { ShipDef("purple", 1) }));
            OrbitPoint first = GameEngine.OrbitPosition(game, 0), last = GameEngine.OrbitPosition(game, 1);
            Require(first.Side == 0 && first.TangentX == 1 && first.TangentY == 0, "The feed travels right along the bottom straight.");
            Require(Math.Abs(first.X - last.X) > .1 || Math.Abs(first.Y - last.Y) > .1, "Unloading does not wrap through the loading mouth.");
            OrbitPoint previous = first;
            for (int i = 1; i <= 2000; i++) {
                OrbitPoint point = GameEngine.OrbitPosition(game, i / 2000.0);
                double distance = Math.Sqrt((point.X - previous.X) * (point.X - previous.X) + (point.Y - previous.Y) * (point.Y - previous.Y));
                Require(distance < .02, "Rounded road positions remain continuous.");
                Require(Math.Abs(point.TangentX * point.TangentX + point.TangentY * point.TangentY - 1) < 1e-9, "Road tangent is a unit vector.");
                if (point.Side == -1) Require(!point.CanFire, "Rounded corners do not shoot through edge cells.");
                previous = point;
            }
        }

        private static void DataValidation(LevelDefinition original)
        {
            GameState game = GameEngine.Create(original);
            string originalFirstColor = original.cells[0]; int energy = original.fleet[0].ammo;
            game.Grid[0, 0] = "black"; game.Queue[0].Ammo--;
            Require(original.cells[0] == originalFirstColor && original.fleet[0].ammo == energy, "Playing does not mutate authored cells or fleet.");
            bool invalid = false;
            try { GameEngine.Create(Level(1, 1, new[] { "unknown" }, new[] { ShipDef("purple", 1) })); } catch (ArgumentException) { invalid = true; }
            Require(invalid, "Unknown colours are rejected.");
            invalid = false; try { GameEngine.Advance(game, Double.NaN); } catch (ArgumentException) { invalid = true; }
            Require(invalid, "Invalid simulation times are rejected.");
        }

        private static void RemainingFleetSpeed()
        {
            var fleet = new ShipDefinition[6]; for (int i = 0; i < 6; i++) fleet[i] = ShipDef("purple", 1);
            GameState game = GameEngine.Create(Level(6, 1, new[] { "purple", "purple", "purple", "purple", "purple", "purple" }, fleet));
            for (int i = 0; i < 5; i++) GameEngine.Launch(game, game.Queue[0].Id);
            Require(GameEngine.RemainingShipCount(game) == 6 && GameEngine.EndgameSpeed(game) == 1, "Five active UFOs do not accelerate a six-UFO fleet.");
            Ship gone = game.Active[0]; game.Active.RemoveAt(0); gone.Ammo = 0;
            var flights = new List<GameEvent> { new GameEvent { Type = GameEventType.Depleted, ShipId = gone.Id }, new GameEvent { Type = GameEventType.Returned, ShipId = game.Queue[0].Id } };
            Require(GameEngine.RemainingShipCount(game, flights) == 5 && GameEngine.EndgameSpeed(game, flights) == 2, "Depleted and duplicate returning UFOs do not inflate the final-five count.");
        }

        private static HashSet<string> ExposedColors(GameState game)
        {
            var colors = new HashSet<string>();
            for (int col = 0; col < game.Width; col++) {
                for (int row = 0; row < game.Height; row++) if (!String.IsNullOrEmpty(game.Grid[row, col])) { colors.Add(game.Grid[row, col]); break; }
                for (int row = game.Height - 1; row >= 0; row--) if (!String.IsNullOrEmpty(game.Grid[row, col])) { colors.Add(game.Grid[row, col]); break; }
            }
            for (int row = 0; row < game.Height; row++) {
                for (int col = 0; col < game.Width; col++) if (!String.IsNullOrEmpty(game.Grid[row, col])) { colors.Add(game.Grid[row, col]); break; }
                for (int col = game.Width - 1; col >= 0; col--) if (!String.IsNullOrEmpty(game.Grid[row, col])) { colors.Add(game.Grid[row, col]); break; }
            }
            return colors;
        }

        private static ShipDefinition ShipDef(string color, int ammo) { return new ShipDefinition { color = color, ammo = ammo, lane = -1 }; }
        private static LevelDefinition Level(int width, int height, string[] cells, ShipDefinition[] fleet)
        { return new LevelDefinition { id = "test", title = "Core fixture", width = width, height = height, cells = cells, fleet = fleet, rules = new Rules { maxActive = 5, waitingSlots = 5, tourDuration = 3, queueColumns = 0 } }; }
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("FAILED: " + message); }
    }
}
