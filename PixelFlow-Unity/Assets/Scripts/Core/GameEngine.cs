using System;
using System.Collections.Generic;

namespace PixelFlow.Core
{
    // Pure C#: no rendering, Unity timing, or frame-rate-dependent gameplay.
    public static class GameEngine
    {
        public const double OrbitPadding = 2;
        public const double FixedStep = 1.0 / 60.0;
        private const double ShotInterval = 0.075;
        private const double EntrySeparation = 0.15;
        private const double Epsilon = 1e-10;

        public static GameState Create(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException("level");
            if (level.width < 1 || level.width > 32 || level.height < 1 || level.height > 32)
                throw new ArgumentException("Resim 1 ile 32 kare arasında olmalı.");
            if (level.cells == null || level.cells.Length != level.width * level.height)
                throw new ArgumentException("Resimdeki kare sayısı genişlik ve yüksekliğe uymuyor.");
            Rules rules = (level.rules ?? new Rules()).Copy();
            ValidateRules(rules);
            var game = new GameState {
                LevelId = level.id ?? "custom", Title = level.title ?? "Yeni bölüm", Provenance = level.provenance,
                Width = level.width, Height = level.height, Rules = rules,
                Grid = new string[level.height, level.width], Waiting = new Ship[rules.waitingSlots]
            };
            var counts = new Dictionary<string, int>();
            for (int row = 0; row < game.Height; row++)
                for (int col = 0; col < game.Width; col++) {
                    string color = level.cells[row * game.Width + col];
                    if (String.IsNullOrEmpty(color) || color == ".") continue;
                    if (!GameColors.IsKnown(color)) throw new ArgumentException("Resimde bilinmeyen bir renk var: " + color);
                    game.Grid[row, col] = color;
                    game.Total++;
                    if (!counts.ContainsKey(color)) counts[color] = 0;
                    counts[color]++;
                }
            if (game.Total == 0) throw new ArgumentException("Oynamak için resmine en az bir kare ekle.");

            ShipDefinition[] fleet = level.fleet;
            if (fleet == null) fleet = AutoFleet(counts);
            if (fleet.Length < 1 || fleet.Length > 40) throw new ArgumentException("Araç listesinde 1 ile 40 arasında araç olmalı.");
            for (int index = 0; index < fleet.Length; index++) {
                ShipDefinition source = fleet[index];
                if (source == null || !GameColors.IsKnown(source.color)) throw new ArgumentException("Araç rengi bilinmiyor.");
                if (source.ammo < 1 || source.ammo > 999) throw new ArgumentException("Atış hakkı 1 ile 999 arasında olmalı.");
                if (source.lane < -1 || source.lane >= Math.Max(1, rules.queueColumns)) throw new ArgumentException("Araç sütunu geçersiz.");
                int lane = rules.queueColumns == 0 ? 0 : (source.lane >= 0 ? source.lane : index % rules.queueColumns);
                game.Queue.Add(new Ship { Id = "ship-" + (index + 1), Color = source.color, Ammo = source.ammo, InitialAmmo = source.ammo, Lane = lane });
            }
            return game;
        }

        public static void ValidateRules(Rules rules)
        {
            if (rules.maxActive < 1 || rules.maxActive > 5) throw new ArgumentException("Yoldaki araç sınırı 1 ile 5 arasında olmalı.");
            if (rules.waitingSlots < 1 || rules.waitingSlots > 5) throw new ArgumentException("Bekleme yeri sayısı 1 ile 5 arasında olmalı.");
            if (Double.IsNaN(rules.tourDuration) || Double.IsInfinity(rules.tourDuration) || rules.tourDuration < 3 || rules.tourDuration > 12)
                throw new ArgumentException("Tur süresi 3 ile 12 saniye arasında olmalı.");
            if (rules.queueColumns < 0 || rules.queueColumns > 3) throw new ArgumentException("Araç sütunu sayısı 0 ile 3 arasında olmalı.");
        }

        private static ShipDefinition[] AutoFleet(Dictionary<string, int> counts)
        {
            var colors = new List<string>(counts.Keys);
            var eligible = colors.FindAll(color => counts[color] >= 24);
            eligible.Sort((a, b) => counts[b].CompareTo(counts[a]));
            var split = new HashSet<string>();
            for (int i = 0; i < Math.Min(eligible.Count, Math.Max(0, 6 - colors.Count)); i++) split.Add(eligible[i]);
            var first = new List<ShipDefinition>(); var second = new List<ShipDefinition>();
            foreach (string color in colors) {
                int pieces = split.Contains(color) ? 2 : 1;
                for (int part = 0; part < pieces; part++) {
                    var item = new ShipDefinition { color = color, ammo = counts[color] / pieces + (part < counts[color] % pieces ? 1 : 0) };
                    (part == 0 ? first : second).Add(item);
                }
            }
            first.AddRange(second); return first.ToArray();
        }

        public static List<Ship> AvailableShips(GameState game)
        {
            var result = new List<Ship>();
            foreach (Ship ship in game.Waiting) if (ship != null) result.Add(ship);
            var lanes = new HashSet<int>();
            foreach (Ship ship in game.Queue)
                if (game.Rules.queueColumns == 0 || lanes.Add(ship.Lane)) result.Add(ship);
            return result;
        }

        public static LaunchResult Launch(GameState game, string shipId, bool overfly = false)
        {
            if (overfly && game.OverflyUsed >= 1) return Failure("Havadan atış hakkın bu bölümde kullanıldı.");
            if (game.Status != GameStatus.Playing)
                return Failure(game.Status == GameStatus.Won ? "Bölüm tamamlandı." : game.Status == GameStatus.Lost ? game.LossReason : "Önce bekleme alanını düzenle.");
            if (game.Active.Count >= game.Rules.maxActive) return Failure("Yol dolu. Bir UFO ayrılınca yer açılacak.");
            Ship ship = null;
            for (int slot = 0; slot < game.Waiting.Length; slot++)
                if (game.Waiting[slot] != null && game.Waiting[slot].Id == shipId) { ship = game.Waiting[slot]; game.Waiting[slot] = null; break; }
            if (ship == null) {
                int index = game.Queue.FindIndex(item => item.Id == shipId);
                if (index < 0) return Failure("Bu UFO şu anda yola çıkamaz.");
                ship = game.Queue[index];
                if (game.Rules.queueColumns > 0)
                    for (int i = 0; i < index; i++) if (game.Queue[i].Lane == ship.Lane) return Failure("Önce bu sütunun önündeki UFO’yu gönder.");
                game.Queue.RemoveAt(index);
            }
            double delay = 0.2;
            foreach (Ship active in game.Active)
                if (active.EntryDelay > 0 || active.Progress < 0.06)
                    delay = Math.Max(delay, active.EntryDelay + EntrySeparation - active.Progress * game.Rules.tourDuration);
            ship.EntryDelay = delay; ship.EntryDelayTotal = delay; ship.BoardingDuration = 0.2;
            ship.Progress = 0; ship.ShotClock = 0; ship.Overfly = overfly;
            if (overfly) game.OverflyUsed++;
            game.Active.Add(ship); game.Moves++;
            return new LaunchResult { Success = true, Ship = ship };
        }

        private static LaunchResult Failure(string reason) { return new LaunchResult { Success = false, Reason = reason }; }

        public static OrbitGeometry GetOrbitGeometry(GameState game)
        {
            double perimeter = 2 * (game.Width + game.Height) + 2 * Math.PI * OrbitPadding;
            double gap = Math.Min(2.7, Math.PI * OrbitPadding / 2 * .86);
            return new OrbitGeometry { Left = -OrbitPadding, Right = game.Width + OrbitPadding, Top = -OrbitPadding, Bottom = game.Height + OrbitPadding,
                Radius = OrbitPadding, Perimeter = perimeter, Gap = gap, TravelLength = perimeter - gap };
        }

        public static OrbitPoint OrbitPosition(GameState game, double progress)
        {
            if (Double.IsNaN(progress)) throw new ArgumentException("Yol konumu geçersiz.");
            OrbitGeometry geometry = GetOrbitGeometry(game);
            double radius = geometry.Radius, quarter = Math.PI * radius / 2;
            double distance = Math.Max(0, Math.Min(1, progress)) * geometry.TravelLength;
            if (distance < game.Width) return Straight(distance, game.Height + radius, 0, Math.PI / 2, 1, 0);
            distance -= game.Width;
            if (distance < quarter) return Arc(game.Width, game.Height, Math.PI / 2 - distance / radius, radius);
            distance -= quarter;
            if (distance < game.Height) return Straight(game.Width + radius, game.Height - distance, 1, 0, 0, -1);
            distance -= game.Height;
            if (distance < quarter) return Arc(game.Width, 0, -distance / radius, radius);
            distance -= quarter;
            if (distance < game.Width) return Straight(game.Width - distance, -radius, 2, -Math.PI / 2, -1, 0);
            distance -= game.Width;
            if (distance < quarter) return Arc(0, 0, -Math.PI / 2 - distance / radius, radius);
            distance -= quarter;
            if (distance < game.Height) return Straight(-radius, distance, 3, Math.PI, 0, 1);
            distance -= game.Height;
            return Arc(0, game.Height, Math.PI - distance / radius, radius);
        }

        private static OrbitPoint Straight(double x, double y, int side, double angle, double tx, double ty)
        { return new OrbitPoint { X = x, Y = y, Side = side, Angle = angle, TangentX = tx, TangentY = ty, CanFire = true }; }
        private static OrbitPoint Arc(double x, double y, double angle, double radius)
        { return new OrbitPoint { X = x + radius * Math.Cos(angle), Y = y + radius * Math.Sin(angle), Side = -1, Angle = angle, TangentX = Math.Sin(angle), TangentY = -Math.Cos(angle), CanFire = false }; }

        public static GridTarget? FindTarget(GameState game, string color, double progress)
        {
            OrbitPoint point = OrbitPosition(game, progress);
            if (!point.CanFire) return null;
            if (point.Side == 0 || point.Side == 2) {
                int col = (int)Math.Floor(point.X); if (col < 0 || col >= game.Width) return null;
                for (int k = 0; k < game.Height; k++) {
                    int row = point.Side == 0 ? game.Height - 1 - k : k;
                    if (!String.IsNullOrEmpty(game.Grid[row, col])) return game.Grid[row, col] == color ? new GridTarget(row, col) : (GridTarget?)null;
                }
            } else {
                int row = (int)Math.Floor(point.Y); if (row < 0 || row >= game.Height) return null;
                for (int k = 0; k < game.Width; k++) {
                    int col = point.Side == 1 ? game.Width - 1 - k : k;
                    if (!String.IsNullOrEmpty(game.Grid[row, col])) return game.Grid[row, col] == color ? new GridTarget(row, col) : (GridTarget?)null;
                }
            }
            return null;
        }

        public static GridTarget? FindOverflyTarget(GameState game, string color)
        {
            double centerRow = (game.Height - 1) / 2.0, centerCol = (game.Width - 1) / 2.0, best = Double.PositiveInfinity;
            GridTarget? target = null;
            for (int row = 0; row < game.Height; row++) for (int col = 0; col < game.Width; col++) {
                if (game.Grid[row, col] != color) continue;
                double distance = (row - centerRow) * (row - centerRow) + (col - centerCol) * (col - centerCol);
                if (distance < best) { best = distance; target = new GridTarget(row, col); }
            }
            return target;
        }

        public static List<GameEvent> Advance(GameState game, double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0 || seconds > 60)
                throw new ArgumentException("Geçen süre 0 ile 60 saniye arasında olmalı.");
            var events = new List<GameEvent>();
            if (game.HasEnded) return events;
            CheckEnergy(game, events); if (game.HasEnded) return events;
            double alignment = game.Rules.tourDuration / GetOrbitGeometry(game).TravelLength;
            double interval = Math.Min(ShotInterval, Math.Max(FixedStep, Math.Floor(alignment / FixedStep) * FixedStep));
            game.Accumulator += seconds;
            while (game.Accumulator + Epsilon >= FixedStep && !game.HasEnded) {
                game.Accumulator -= FixedStep; game.Elapsed += FixedStep;
                foreach (Ship ship in game.Active.ToArray()) {
                    double movement = FixedStep;
                    if (ship.EntryDelay > 0) {
                        double boarding = Math.Min(ship.EntryDelay, movement);
                        ship.EntryDelay = Math.Max(0, ship.EntryDelay - boarding);
                        if (ship.EntryDelay < Epsilon) ship.EntryDelay = 0;
                        movement -= boarding;
                        if (movement < Epsilon) continue;
                    }
                    ship.Progress = Math.Min(1, ship.Progress + movement / game.Rules.tourDuration); ship.ShotClock += movement;
                    if (ship.Ammo > 0 && ship.ShotClock + Epsilon >= interval && ship.Progress < 1) {
                        ship.ShotClock -= interval;
                        GridTarget? target = ship.Overfly ? FindOverflyTarget(game, ship.Color) : FindTarget(game, ship.Color, ship.Progress);
                        if (target.HasValue) {
                            GridTarget hit = target.Value; game.Grid[hit.Row, hit.Col] = null; game.Cleared++; ship.Ammo--;
                            events.Add(new GameEvent { Type = GameEventType.Shot, ShipId = ship.Id, Color = ship.Color, Ammo = ship.Ammo, Progress = ship.Progress, Row = hit.Row, Col = hit.Col, Overfly = ship.Overfly });
                        }
                    }
                    if (ship.Ammo <= 0 || ship.Progress >= 1 - Epsilon) {
                        game.Active.Remove(ship);
                        GameEvent arrival = ShipEvent(ship.Ammo <= 0 ? GameEventType.Depleted : GameEventType.Returned, ship);
                        if (ship.Ammo <= 0) events.Add(arrival);
                        else {
                            ship.Progress = 0; ship.ShotClock = 0; ship.EntryDelay = 0; ship.EntryDelayTotal = 0; ship.BoardingDuration = 0; ship.Overfly = false;
                            int slot = Array.IndexOf(game.Waiting, null);
                            if (slot >= 0) game.Waiting[slot] = ship;
                            else {
                                game.Queue.Insert(0, ship);
                                if (game.Rules.strictDock) MarkLoss(game, "Bekleme alanı doldu.", events);
                                else { if (!game.HasEnded) game.Status = GameStatus.Blocked; events.Add(new GameEvent { Type = GameEventType.Blocked, ShipId = ship.Id }); }
                            }
                            arrival.Slot = slot;
                            if (game.Status != GameStatus.Lost) events.Add(arrival);
                        }
                    }
                    MarkWin(game, events); CheckEnergy(game, events);
                    if (game.HasEnded) break;
                }
            }
            return events;
        }

        private static GameEvent ShipEvent(GameEventType type, Ship ship)
        { return new GameEvent { Type = type, ShipId = ship.Id, Color = ship.Color, Ammo = ship.Ammo, Progress = ship.Progress, Overfly = ship.Overfly }; }

        private static void MarkWin(GameState game, List<GameEvent> events)
        {
            if (game.Cleared != game.Total || game.HasEnded) return;
            foreach (Ship ship in game.Active) { GameEvent e = ShipEvent(ship.Ammo <= 0 ? GameEventType.Depleted : GameEventType.Departed, ship); e.Source = "active"; e.Reason = "level-complete"; events.Add(e); }
            for (int slot = 0; slot < game.Waiting.Length; slot++) if (game.Waiting[slot] != null) { GameEvent e = ShipEvent(GameEventType.Departed, game.Waiting[slot]); e.Source = "waiting"; e.Slot = slot; e.Reason = "level-complete"; events.Add(e); }
            for (int slot = 0; slot < game.Queue.Count; slot++) { GameEvent e = ShipEvent(GameEventType.Departed, game.Queue[slot]); e.Source = "queue"; e.Slot = slot; e.Reason = "level-complete"; events.Add(e); }
            game.Active.Clear(); Array.Clear(game.Waiting, 0, game.Waiting.Length); game.Queue.Clear(); game.Status = GameStatus.Won;
            events.Add(new GameEvent { Type = GameEventType.Won, LevelId = game.LevelId });
        }

        private static void MarkLoss(GameState game, string reason, List<GameEvent> events)
        { if (game.HasEnded) return; game.Status = GameStatus.Lost; game.LossReason = reason; events.Add(new GameEvent { Type = GameEventType.Lost, Reason = reason }); }

        public static Dictionary<string, int> RemainingByColor(GameState game)
        {
            var result = new Dictionary<string, int>();
            for (int row = 0; row < game.Height; row++) for (int col = 0; col < game.Width; col++) {
                string color = game.Grid[row, col]; if (String.IsNullOrEmpty(color)) continue;
                if (!result.ContainsKey(color)) result[color] = 0; result[color]++;
            }
            return result;
        }

        private static void CheckEnergy(GameState game, List<GameEvent> events)
        {
            if (game.HasEnded) return;
            var energy = new Dictionary<string, int>();
            foreach (Ship ship in AllShips(game)) {
                if (!energy.ContainsKey(ship.Color)) energy[ship.Color] = 0; energy[ship.Color] += ship.Ammo;
            }
            foreach (string color in RemainingByColor(game).Keys)
                if (!energy.ContainsKey(color) || energy[color] <= 0) { MarkLoss(game, GameColors.TurkishName(color) + " enerji tükendi.", events); break; }
        }

        public static IEnumerable<Ship> AllShips(GameState game)
        {
            foreach (Ship ship in game.Queue) yield return ship;
            foreach (Ship ship in game.Waiting) if (ship != null) yield return ship;
            foreach (Ship ship in game.Active) yield return ship;
        }

        public static int RemainingShipCount(GameState game, IEnumerable<GameEvent> departures = null)
        {
            var ids = new HashSet<string>();
            foreach (Ship ship in AllShips(game)) if (ship.Ammo > 0) ids.Add(ship.Id);
            if (departures != null) foreach (GameEvent flight in departures) if (flight.Type == GameEventType.Departed) ids.Add(flight.ShipId);
            return ids.Count;
        }

        public static int EndgameSpeed(GameState game, IEnumerable<GameEvent> departures = null)
        {
            var flights = departures == null ? new List<GameEvent>() : new List<GameEvent>(departures);
            bool hasDeparture = flights.Exists(e => e.Type == GameEventType.Departed);
            if (game.Status != GameStatus.Playing && game.Status != GameStatus.Blocked && !(game.Status == GameStatus.Won && hasDeparture)) return 1;
            int count = RemainingShipCount(game, flights); return count > 0 && count <= 5 ? 2 : 1;
        }

        public static int RestoreWaiting(GameState game)
        {
            if (game.HasEnded) return 0;
            var ships = new List<Ship>(); foreach (Ship ship in game.Waiting) if (ship != null) ships.Add(ship);
            game.Queue.InsertRange(0, ships); Array.Clear(game.Waiting, 0, game.Waiting.Length); game.Status = GameStatus.Playing;
            return ships.Count;
        }
    }
}
