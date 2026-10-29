using System;
using System.Collections.Generic;
using PixelFlow.Core;
using PixelFlow.Presentation;
using PixelFlow.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace PixelFlow.Runtime
{
    /// <summary>Playable Unity view of the portable, deterministic game rules.</summary>
    public sealed class GameController : MonoBehaviour
    {
        public string StartupLevelId = "";
        public int StartingLevel;
        public GameState State { get; private set; }
        public LevelDefinition[] Levels { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsPowerArmed { get; private set; }
        public int Speed { get; private set; }
        public event Action Changed;

        const float ViewTilt = 42f;
        const float ActorScale = 1.5f;
        const float GearScale = 1.5f;
        const float HoverHeight = .65f;
        static readonly float GroundCos = Mathf.Cos(ViewTilt * Mathf.Deg2Rad);
        Transform generated, world, actorParent, effectParent;
        Camera gameCamera;
        PixelHud hud;
        BinVisual bin;
        GameObject[,] blocks;
        TextMesh carrierCounter;
        readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        readonly List<Flight> flights = new List<Flight>();
        readonly List<Ball> balls = new List<Ball>();
        readonly List<Shard> shards = new List<Shard>();
        readonly List<Transform> arrows = new List<Transform>();
        readonly List<Renderer> runwayLights = new List<Renderer>();
        readonly Dictionary<string, Material> paints = new Dictionary<string, Material>();
        readonly List<Vector3> path = new List<Vector3>();
        readonly List<float> distances = new List<float>();
        Vector3[] waitPositions;
        Vector3 boardJoin, boardEnd;
        float pathLength, arrowTime, lampClock;
        bool pendingWin, resultShown, runtimeReady, rushLatched;
        int lastWidth, lastHeight;

        sealed class Actor
        {
            public UfoVisual Visual;
            public Transform Gear;
            public ShipPointer Pointer;
            public Ship Ship;
            public Vector3 LaunchOrigin;
            public int LaunchSlot;
            public float WeaponAge = 1;
            public Vector3 WeaponDirection;
        }
        sealed class Flight
        {
            public GameEvent Event;
            public Vector3 From, To;
            public float Age;
            public const float Duration = .46f;
        }
        sealed class Ball
        {
            public Transform Root;
            public Vector3 From, To;
            public float Age, Duration;
            public int Row, Col;
            public Color Color;
        }
        sealed class Shard
        {
            public Transform Root;
            public Vector3 Velocity;
            public float Age;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapIfNeeded()
        {
            if (FindObjectOfType<GameController>() == null)
                new GameObject("PixelFlow").AddComponent<GameController>();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            LoadLevels();
            hud = PixelHud.Create(transform);
            hud.OnPause = TogglePause;
            hud.OnRestart = RestartLevel;
            hud.OnPrevLevel = delegate { StartLevel(CurrentLevelIndex - 1); };
            hud.OnNextLevel = delegate { StartLevel(CurrentLevelIndex + 1); };
            hud.OnPower = TogglePower;
            int first = Mathf.Clamp(StartingLevel, 0, Levels.Length - 1);
            if (!String.IsNullOrEmpty(StartupLevelId))
                for (int i = 0; i < Levels.Length; i++) if (Levels[i].id == StartupLevelId) first = i;
            runtimeReady = true;
            StartLevel(first);
        }

        public void BuildEditorPreview(int levelIndex = 0)
        {
            LoadLevels();
            if (Levels.Length == 0) return;
            int selected = Mathf.Clamp(levelIndex, 0, Levels.Length - 1);
            if (!String.IsNullOrEmpty(StartupLevelId))
                for (int i = 0; i < Levels.Length; i++) if (Levels[i].id == StartupLevelId) selected = i;
            CurrentLevelIndex = selected;
            State = GameEngine.Create(Levels[selected]);
            BuildWorld();
            SyncActors(0);
        }

        void LoadLevels()
        {
            var all = new List<LevelDefinition>();
            foreach (string resource in new[] { "Levels/reference-levels", "Levels/custom-levels" })
            {
                TextAsset text = Resources.Load<TextAsset>(resource);
                if (text == null) continue;
                LevelCollection data = JsonUtility.FromJson<LevelCollection>(text.text);
                if (data != null && data.levels != null) all.AddRange(data.levels);
            }
            Levels = all.ToArray();
            if (Levels.Length == 0)
                throw new InvalidOperationException("Bölüm dosyası bulunamadı: Assets/Resources/Levels/reference-levels.json");
        }

        public void StartLevel(int index)
        {
            if (Levels == null || Levels.Length == 0) LoadLevels();
            CurrentLevelIndex = Mathf.Clamp(index, 0, Levels.Length - 1);
            State = GameEngine.Create(Levels[CurrentLevelIndex]);
            IsPaused = false; IsPowerArmed = false; Speed = 1;
            pendingWin = false; resultShown = false; rushLatched = false;
            BuildWorld();
            if (hud != null) hud.HideOverlay();
            SyncActors(0); Notify();
        }

        public void RestartLevel() { StartLevel(CurrentLevelIndex); }
        public void TogglePause()
        {
            if (State == null || State.HasEnded) return;
            IsPaused = !IsPaused; Notify();
        }
        public void TogglePower()
        {
            if (State == null || State.HasEnded || State.OverflyUsed > 0 || IsPaused) return;
            IsPowerArmed = !IsPowerArmed; Notify();
        }

        public bool Launch(string id)
        {
            if (!runtimeReady || State == null || State.HasEnded || IsPaused) return false;
            if (BoardOccupancy() >= State.Rules.maxActive) { Message("Yolda " + State.Rules.maxActive + " UFO var."); return false; }
            Actor actor;
            if (!actors.TryGetValue(id, out actor)) return false;
            Vector3 origin = actor.Visual.Root.position;
            int slot = Mathf.Max(0, FreeCarrierCount() - 1);
            LaunchResult result = GameEngine.Launch(State, id, IsPowerArmed);
            if (!result.Success) { Message(result.Reason); return false; }
            actor.LaunchOrigin = origin;
            actor.LaunchSlot = slot;
            actor.Ship = result.Ship;
            if (IsPowerArmed) IsPowerArmed = false;
            SyncActors(0); Notify();
            return true;
        }

        void Update()
        {
            if (!runtimeReady || State == null) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight) FitCamera();
            ReadSelection();
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) RestartLevel();
            float dt = IsPaused ? 0 : Mathf.Min(Time.deltaTime, .08f);
            UpdateSpeed();
            if (dt > 0 && !State.HasEnded) ProcessEvents(GameEngine.Advance(State, dt * Speed));
            float visualStep = dt * Speed;
            UpdateFlights(visualStep);
            SyncActors(visualStep);
            UpdateBalls(visualStep);
            UpdateShards(visualStep);
            AnimateRoad(visualStep);
            UpdateSpeed();
            if (pendingWin && !resultShown && balls.Count == 0 && flights.Count == 0)
            {
                resultShown = true;
                if (hud != null) hud.ShowWin(delegate { StartLevel((CurrentLevelIndex + 1) % Levels.Length); }, RestartLevel);
                Notify();
            }
        }

        void ReadSelection()
        {
            if (IsPaused || State.HasEnded || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && (EventSystem.current.IsPointerOverGameObject() ||
                (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))) return;
            RaycastHit hit;
            if (gameCamera != null && Physics.Raycast(gameCamera.ScreenPointToRay(Input.mousePosition), out hit, 300f))
            {
                ShipPointer pointer = hit.collider.GetComponentInParent<ShipPointer>();
                if (pointer != null && pointer.CanSelect) Launch(pointer.ShipId);
            }
        }

        void BuildWorld()
        {
            Transform previous = transform.Find("PixelFlow_Generated");
            if (previous != null) { previous.gameObject.SetActive(false); Remove(previous.gameObject); }
            actors.Clear(); flights.Clear(); balls.Clear(); shards.Clear(); arrows.Clear(); runwayLights.Clear(); path.Clear(); distances.Clear();
            generated = new GameObject("PixelFlow_Generated").transform; generated.SetParent(transform, false);
            world = Child("World", generated); actorParent = Child("UFOs", generated); effectParent = Child("Effects", generated);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#fff4e5"); RenderSettings.ambientEquatorColor = Hex("#b8a4b4"); RenderSettings.ambientGroundColor = Hex("#746875");
            RenderSettings.ambientIntensity = 1.05f;
            var cameraObject = new GameObject("Main Camera"); cameraObject.transform.SetParent(generated, false);
            gameCamera = cameraObject.AddComponent<Camera>(); gameCamera.tag = "MainCamera";
            gameCamera.clearFlags = CameraClearFlags.SolidColor; gameCamera.backgroundColor = Hex("#807476");
            gameCamera.orthographic = true; gameCamera.nearClipPlane = .1f; gameCamera.farClipPlane = 220;
            cameraObject.AddComponent<AudioListener>();
            Vector3 target = new Vector3(0, 0, -12f / GroundCos);
            gameCamera.transform.position = target + new Vector3(0, 65f * GroundCos, -65f * Mathf.Sin(ViewTilt * Mathf.Deg2Rad));
            gameCamera.transform.LookAt(target);
            FitCamera();
            var key = Child("Key light", generated).gameObject.AddComponent<Light>(); key.type = LightType.Directional;
            key.color = Hex("#fff3df"); key.intensity = 1.2f; key.shadows = LightShadows.Soft; key.shadowStrength = .40f;
            key.transform.rotation = Quaternion.Euler(57, -32, 0); key.shadowBias = .04f;
            var fill = Child("Soft fill", generated).gameObject.AddComponent<Light>(); fill.type = LightType.Directional; fill.color = Hex("#dce9ff"); fill.intensity = .35f; fill.transform.rotation = Quaternion.Euler(38, 150, 0);
            Cube("Ground", world, new Vector3(0, -.15f, 0), new Vector3(90, .25f, 150), Material("#807476"));
            float boundary = Point(0, State.Height + 7f, -.025f).z;
            Cube("Queue ground", world, new Vector3(0, -.06f, boundary - 25f), new Vector3(85, .1f, 50), Material("#a39397"));
            blocks = new GameObject[State.Height, State.Width];
            for (int row = 0; row < State.Height; row++) for (int col = 0; col < State.Width; col++)
            {
                string color = State.Grid[row, col]; if (String.IsNullOrEmpty(color)) continue;
                GameObject block = ModelFactory.CreateBlock(world, ColorFor(color), 1);
                block.name = "Block " + row + "," + col;
                block.transform.position = TilePoint(row, col, .43f); blocks[row, col] = block;
            }
            BuildRoad();
            Vector3 binPosition = Point(-1.9f, State.Height + .6f, .08f);
            bin = ModelFactory.CreateBox(world, 4.2f, 2.5f, 2.1f);
            bin.Root.position = binPosition;
            carrierCounter = Text("Free landing gear", world, "5/5", .28f, Hex("#fff7dc")); carrierCounter.transform.position = binPosition + new Vector3(1.5f, .15f, -2.5f);
            waitPositions = new Vector3[State.Waiting.Length];
            for (int i = 0; i < waitPositions.Length; i++)
            {
                Vector3 p = Point(State.Width * .5f + (i - (waitPositions.Length - 1) * .5f) * 3.2f, State.Height + 7.0f, .08f); waitPositions[i] = p;
                GameObject pad = ModelFactory.CreateBlock(world, Hex("#ffb34e"), 2.4f); pad.name = "Waiting pad " + (i + 1);
                pad.transform.position = p + Vector3.up * .25f; pad.transform.localScale = new Vector3(1, .24f, 1.65f);
            }
            arrowTime = lampClock = 0; AnimateRoad(0);
        }

        void FitCamera()
        {
            if (gameCamera == null || State == null) return;
            lastWidth = Screen.width; lastHeight = Screen.height;
            float screenAspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height), aspect = PixelHud.PortraitAspect;
            if (screenAspect > aspect) { float w = aspect / screenAspect; gameCamera.rect = new Rect((1 - w) * .5f, 0, w, 1); }
            else { float h = screenAspect / aspect; gameCamera.rect = new Rect(0, (1 - h) * .5f, 1, h); }
            float viewWidth = Mathf.Max(29f, State.Width + 9.5f);
            gameCamera.orthographicSize = viewWidth / (2f * aspect);
        }

        Vector3 Point(float x, float y, float elevation = 0) { return new Vector3(x - State.Width * .5f, elevation, (State.Height * .5f - y) / GroundCos); }
        Vector3 TilePoint(int row, int col, float elevation = .52f) { return Point(col + .5f, row + .5f + Mathf.Sin(ViewTilt * Mathf.Deg2Rad) * .43f, elevation); }
        Vector3 Orbit(double progress)
        {
            OrbitPoint p = GameEngine.OrbitPosition(State, progress);
            if (!p.CanFire && p.X < 0 && p.Y >= State.Height) return Point(-2, State.Height, .22f);
            return Point((float)p.X, (float)p.Y, .22f);
        }
        Vector3 FlightCenter(double progress) { float a = (float)progress * Mathf.PI * 2f; return Point(State.Width * .5f + Mathf.Cos(a) * .3f, State.Height * .5f + Mathf.Sin(a) * .2f, 5.2f); }

        void BuildRoad()
        {
            for (int i = 0; i <= 640; i++) { Vector3 p = Orbit(i / 640.0); path.Add(p); distances.Add(i == 0 ? 0 : distances[i - 1] + Vector3.Distance(path[i - 1], p)); }
            pathLength = distances[distances.Count - 1]; boardJoin = path[0]; boardEnd = path[path.Count - 1];
            Ribbon("Raised golden track", path, 2.05f, .05f, .18f, Material("#d79b4d"));
            Ribbon("Dark road", path, 1.55f, .18f, .23f, Material("#554a41"));
            var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (int i = 0; i < path.Count; i++) { Vector3 tangent = path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]; Vector3 n = Vector3.Cross(Vector3.up, tangent.normalized); inner.Add(path[i] + n * .94f); outer.Add(path[i] - n * .94f); }
            Ribbon("Inner bright rim", inner, .11f, .25f, .32f, Material("#ffd181")); Ribbon("Outer bright rim", outer, .11f, .25f, .32f, Material("#ffd181"));
            int count = Mathf.FloorToInt(pathLength / 2.8f);
            for (int i = 0; i < count; i++)
            {
                Transform arrow = Child("Road direction", world);
                for (int side = -1; side <= 1; side += 2) { GameObject wing = Cube("Arrow wing", arrow, new Vector3(side * .11f, 0, -.10f), new Vector3(.09f, .035f, .35f), Material("#a59679")); wing.transform.localRotation = Quaternion.Euler(0, -side * 43f, 0); }
                arrows.Add(arrow);
            }
            for (int i = -1; i <= 1; i++) Cube("Runway stripe", world, boardEnd + new Vector3(i * .35f, .08f, .43f), new Vector3(.16f, .035f, .86f), Material("#fff8e5"));
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere); lamp.name = "Runway light"; lamp.transform.SetParent(world, false); lamp.transform.position = boardEnd + new Vector3(side * .87f, .14f, .45f); lamp.transform.localScale = Vector3.one * .19f; Remove(lamp.GetComponent<Collider>());
                Renderer paint = lamp.GetComponent<Renderer>(); paint.sharedMaterial = Material("#ffdc53"); runwayLights.Add(paint);
            }
        }

        void Ribbon(string name, IList<Vector3> line, float width, float bottom, float top, Material material)
        {
            var vertices = new Vector3[line.Count * 4]; var triangles = new List<int>();
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 tangent = line[Mathf.Min(line.Count - 1, i + 1)] - line[Mathf.Max(0, i - 1)], n = Vector3.Cross(Vector3.up, tangent.normalized) * width * .5f;
                Vector3 p = line[i]; p.y = 0;
                vertices[i * 4] = p - n + Vector3.up * top; vertices[i * 4 + 1] = p + n + Vector3.up * top; vertices[i * 4 + 2] = p - n + Vector3.up * bottom; vertices[i * 4 + 3] = p + n + Vector3.up * bottom;
                if (i == 0) continue; int a = (i - 1) * 4, b = i * 4;
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, a + 2, a + 3, b + 2, a + 3, b + 3, b + 2, a, a + 2, b, a + 2, b + 2, b, a + 1, b + 1, a + 3, a + 3, b + 1, b + 3 });
            }
            Mesh mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GameObject go = new GameObject(name); go.transform.SetParent(world, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void AnimateRoad(float dt)
        {
            arrowTime += dt * 1.15f; lampClock += dt;
            for (int i = 0; i < arrows.Count; i++)
            {
                float d = (i * 2.8f + arrowTime) % pathLength; arrows[i].gameObject.SetActive(d > .6f && d < pathLength - 1.2f);
                int index = distances.BinarySearch(d); if (index < 0) index = ~index; index = Mathf.Clamp(index, 1, path.Count - 1);
                float t = Mathf.InverseLerp(distances[index - 1], distances[index], d);
                Vector3 p = Vector3.Lerp(path[index - 1], path[index], t); p.y = .28f; arrows[i].position = p;
                Vector3 tangent = path[index] - path[index - 1]; if (tangent.sqrMagnitude > .00001f) arrows[i].rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
            bool on = Mathf.FloorToInt(lampClock * 2f) % 2 == 0;
            foreach (Renderer lamp in runwayLights) lamp.sharedMaterial = Material(on ? "#ffdc53" : "#8d7141");
        }

        Actor EnsureActor(Ship ship)
        {
            Actor actor; if (actors.TryGetValue(ship.Id, out actor)) { actor.Ship = ship; return actor; }
            UfoVisual visual = ModelFactory.CreateUfo(actorParent, ColorFor(ship.Color)); visual.Root.name = ship.Id; visual.Root.localScale = Vector3.one * ActorScale;
            ShipPointer pointer = visual.Root.gameObject.AddComponent<ShipPointer>(); pointer.ShipId = ship.Id;
            SphereCollider collider = visual.Root.gameObject.AddComponent<SphereCollider>(); collider.center = new Vector3(0, .45f, 0); collider.radius = 1.28f;
            Transform gear = ModelFactory.CreateGear(actorParent); gear.name = ship.Id + " landing gear"; gear.localScale = Vector3.one * GearScale; gear.gameObject.SetActive(false);
            actor = new Actor { Visual = visual, Gear = gear, Pointer = pointer, Ship = ship }; actors.Add(ship.Id, actor); return actor;
        }

        void SyncActors(float dt)
        {
            var alive = new HashSet<string>(); var available = new HashSet<string>();
            foreach (Ship ship in GameEngine.AvailableShips(State)) available.Add(ship.Id);
            var laneRows = new Dictionary<int, int>(); int columns = Mathf.Max(1, State.Rules.queueColumns);
            foreach (Ship ship in State.Queue)
            {
                int lane = State.Rules.queueColumns > 0 ? ship.Lane : State.Queue.IndexOf(ship) % 3;
                int row; laneRows.TryGetValue(lane, out row); laneRows[lane] = row + 1;
                Actor actor = EnsureActor(ship); alive.Add(ship.Id); bool visible = row < 4;
                actor.Visual.Root.gameObject.SetActive(visible); actor.Gear.gameObject.SetActive(false);
                actor.Visual.Root.position = Point(State.Width * .5f + (lane - ((State.Rules.queueColumns > 0 ? columns : 3) - 1) * .5f) * 3.4f, State.Height + 13f + row * 6.4f, HoverHeight);
                actor.Pointer.CanSelect = visible && row < 3 && available.Contains(ship.Id) && !IsPaused && !InFlight(ship.Id) && BoardOccupancy() < State.Rules.maxActive;
            }
            for (int i = 0; i < State.Waiting.Length; i++)
            {
                Ship ship = State.Waiting[i]; if (ship == null) continue; Actor actor = EnsureActor(ship); alive.Add(ship.Id);
                actor.Visual.Root.gameObject.SetActive(true); actor.Gear.gameObject.SetActive(false); actor.Visual.Root.position = waitPositions[i] + Vector3.up * .45f;
                actor.Pointer.CanSelect = !IsPaused && !InFlight(ship.Id) && BoardOccupancy() < State.Rules.maxActive;
            }
            foreach (Ship ship in State.Active)
            {
                Actor actor = EnsureActor(ship); alive.Add(ship.Id); actor.Visual.Root.gameObject.SetActive(true); actor.Pointer.CanSelect = false;
                if (ship.EntryDelay > 0)
                {
                    float duration = (float)Math.Max(.01, ship.BoardingDuration), progress = Mathf.Clamp01(1 - (float)ship.EntryDelay / duration);
                    if (ship.EntryDelay > duration)
                    {
                        actor.Visual.Root.position = actor.LaunchOrigin + Vector3.up * 1.8f; actor.Gear.gameObject.SetActive(false);
                    }
                    else if (ship.Overfly)
                    {
                        actor.Gear.gameObject.SetActive(false); actor.Visual.Root.position = Vector3.Lerp(actor.LaunchOrigin, FlightCenter(0), Smooth(progress)) + Vector3.up * Mathf.Sin(progress * Mathf.PI) * 1.7f;
                    }
                    else
                    {
                        Vector3 stored = bin.GetStoredPosition(actor.LaunchSlot), carrier = Vector3.Lerp(stored, boardJoin, Smooth(progress)) + Vector3.up * Mathf.Sin(progress * Mathf.PI) * .38f;
                        actor.Gear.gameObject.SetActive(true); actor.Gear.position = carrier; actor.Gear.rotation = Quaternion.Euler(0, 90, Mathf.Lerp(-22, 0, Mathf.Clamp01(progress / .7f)));
                        float flight = Mathf.Clamp01(progress / .76f); actor.Visual.Root.position = Vector3.Lerp(actor.LaunchOrigin, carrier + Vector3.up * ModelFactory.GearMountHeight * GearScale, Smooth(flight)) + Vector3.up * Mathf.Sin(flight * Mathf.PI) * 2.2f;
                    }
                }
                else
                {
                    Vector3 p = ship.Overfly ? FlightCenter(ship.Progress) : Orbit(ship.Progress);
                    actor.Visual.Root.position = ship.Overfly ? p : p + Vector3.up * ModelFactory.GearMountHeight * GearScale;
                    actor.Gear.gameObject.SetActive(!ship.Overfly); actor.Gear.position = p;
                    OrbitPoint road = GameEngine.OrbitPosition(State, ship.Progress);
                    Vector3 direction = new Vector3((float)road.TangentX, 0, -(float)road.TangentY / GroundCos); if (direction.sqrMagnitude > .0001f) actor.Gear.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 90, 0);
                }
            }
            foreach (Flight flight in flights)
            {
                Actor actor; if (!actors.TryGetValue(flight.Event.ShipId, out actor)) continue; alive.Add(flight.Event.ShipId);
                actor.Visual.Root.gameObject.SetActive(true); actor.Pointer.CanSelect = false; actor.Gear.gameObject.SetActive(false);
                float t = Mathf.Clamp01(flight.Age / Flight.Duration); actor.Visual.Root.position = Vector3.Lerp(flight.From, flight.To, Smooth(t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.7f;
                if (flight.Event.Type == GameEventType.Departed) actor.Visual.Root.localScale = Vector3.one * ActorScale * Mathf.Lerp(1, .12f, t);
            }
            foreach (KeyValuePair<string, Actor> pair in actors)
            {
                Actor actor = pair.Value;
                if (!alive.Contains(pair.Key)) { actor.Visual.Root.gameObject.SetActive(false); actor.Gear.gameObject.SetActive(false); actor.Pointer.CanSelect = false; }
                actor.Visual.SetAmmo(actor.Ship.Ammo); actor.Visual.FaceCamera(gameCamera); actor.WeaponAge += dt;
                actor.Visual.SetWeapon(Mathf.Clamp01(1 - actor.WeaponAge / .16f), actor.WeaponDirection);
            }
            if (bin != null) bin.SetStock(FreeCarrierCount());
            if (carrierCounter != null) { carrierCounter.text = FreeCarrierCount() + "/" + State.Rules.maxActive; carrierCounter.transform.rotation = gameCamera.transform.rotation; }
        }

        void ProcessEvents(List<GameEvent> events)
        {
            foreach (GameEvent e in events)
            {
                Actor actor; actors.TryGetValue(e.ShipId ?? "", out actor);
                if (e.Type == GameEventType.Shot)
                {
                    Vector3 origin = e.Overfly ? FlightCenter(e.Progress) : Orbit(e.Progress) + Vector3.up * ModelFactory.GearMountHeight * GearScale;
                    origin += Vector3.up * .78f; Vector3 target = TilePoint(e.Row, e.Col);
                    if (actor != null) { actor.WeaponAge = 0; actor.WeaponDirection = target - origin; }
                    SpawnBall(origin, target, e.Row, e.Col, ColorFor(e.Color));
                }
                else if (e.Type == GameEventType.Depleted)
                {
                    if (actor != null) { actor.Visual.Root.gameObject.SetActive(false); actor.Gear.gameObject.SetActive(false); actor.Pointer.CanSelect = false; }
                }
                else if (e.Type == GameEventType.Returned || e.Type == GameEventType.Departed)
                {
                    if (actor == null) continue;
                    Vector3 from = actor.Visual.Root.position, to;
                    if (e.Type == GameEventType.Returned && e.Slot >= 0) to = waitPositions[e.Slot] + Vector3.up * .45f;
                    else if (e.Type == GameEventType.Returned) to = Point(State.Width * .5f, State.Height + 13f, HoverHeight);
                    else to = from + Vector3.up * 8f;
                    flights.Add(new Flight { Event = e, From = from, To = to }); actor.Pointer.CanSelect = false;
                }
                else if (e.Type == GameEventType.Won) pendingWin = true;
                else if (e.Type == GameEventType.Lost && !resultShown)
                {
                    resultShown = true; if (hud != null) hud.ShowLoss(e.Reason, RestartLevel);
                }
                else if (e.Type == GameEventType.Blocked) { GameEngine.RestoreWaiting(State); Message("Bekleyen UFO’lar tekrar sıraya alındı."); }
            }
            UpdateSpeed(); Notify();
        }

        void SpawnBall(Vector3 from, Vector3 to, int row, int col, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Shot"; go.transform.SetParent(effectParent, false); go.transform.position = from; go.transform.localScale = Vector3.one * .32f; Remove(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = ColorMaterial(color, .2f);
            TrailRenderer trail = go.AddComponent<TrailRenderer>(); trail.time = .13f; trail.startWidth = .22f; trail.endWidth = 0; trail.Clear(); trail.material = ColorMaterial(Color.Lerp(color, Color.white, .55f), .6f); trail.startColor = Color.white; trail.endColor = new Color(color.r, color.g, color.b, 0);
            balls.Add(new Ball { Root = go.transform, From = from, To = to, Row = row, Col = col, Color = color, Duration = Mathf.Clamp(Vector3.Distance(from, to) / 85f, .1f, .24f) });
        }

        void UpdateBalls(float dt)
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball ball = balls[i]; ball.Age += dt; float t = Mathf.Clamp01(ball.Age / ball.Duration); ball.Root.position = Vector3.Lerp(ball.From, ball.To, t);
                if (t < 1) continue;
                if (blocks[ball.Row, ball.Col] != null) { Remove(blocks[ball.Row, ball.Col]); blocks[ball.Row, ball.Col] = null; }
                for (int j = 0; j < 4; j++)
                {
                    GameObject chip = Cube("Block chip", effectParent, ball.To, Vector3.one * .16f, ColorMaterial(ball.Color, .25f));
                    shards.Add(new Shard { Root = chip.transform, Velocity = new Vector3(Mathf.Cos(j * Mathf.PI * .5f) * 2.3f, 3f, Mathf.Sin(j * Mathf.PI * .5f) * 2.3f) });
                }
                Remove(ball.Root.gameObject); balls.RemoveAt(i);
            }
        }
        void UpdateShards(float dt)
        {
            for (int i = shards.Count - 1; i >= 0; i--) { Shard s = shards[i]; s.Age += dt; if (s.Age > .33f) { Remove(s.Root.gameObject); shards.RemoveAt(i); continue; } s.Velocity += Vector3.down * (8f * dt); s.Root.position += s.Velocity * dt; s.Root.Rotate(new Vector3(100, 80, 45) * dt); s.Root.localScale = Vector3.one * .16f * (1 - s.Age / .33f); }
        }
        void UpdateFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight f = flights[i]; f.Age += dt; if (f.Age < Flight.Duration) continue;
                Actor actor; if (actors.TryGetValue(f.Event.ShipId, out actor)) { actor.Visual.Root.localScale = Vector3.one * ActorScale; if (f.Event.Type == GameEventType.Departed) actor.Visual.Root.gameObject.SetActive(false); }
                flights.RemoveAt(i); Notify();
            }
        }

        int BoardOccupancy()
        {
            var ids = new HashSet<string>(); foreach (Ship s in State.Active) ids.Add(s.Id);
            foreach (Flight flight in flights) if (flight.Event.Type == GameEventType.Returned || flight.Event.Source == "active") ids.Add(flight.Event.ShipId);
            return ids.Count;
        }
        int FreeCarrierCount() { return Mathf.Max(0, State.Rules.maxActive - BoardOccupancy()); }
        bool InFlight(string id) { return flights.Exists(f => f.Event.ShipId == id); }
        void UpdateSpeed()
        {
            var exits = new List<GameEvent>(); foreach (Flight f in flights) exits.Add(f.Event);
            if (GameEngine.EndgameSpeed(State, exits) == 2) rushLatched = true;
            int next = rushLatched ? 2 : 1; if (Speed != next) { Speed = next; Notify(); }
        }
        void Notify()
        {
            if (State != null && hud != null) hud.SetState(CurrentLevelIndex + 1, Levels.Length, IsPaused, State.OverflyUsed == 0 ? 1 : 0, IsPowerArmed, CurrentLevelIndex > 0, CurrentLevelIndex < Levels.Length - 1, Speed);
            if (Changed != null) Changed();
        }
        void Message(string text) { if (hud != null) hud.ShowMessage(text); }
        void OnApplicationFocus(bool focus) { if (!focus && runtimeReady && State != null && !State.HasEnded) { IsPaused = true; Notify(); } }
        static float Smooth(float x) { return x * x * (3f - 2f * x); }
        static Transform Child(string name, Transform parent) { Transform t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static void Remove(Object value) { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        static Color Hex(string hex) { Color color; ColorUtility.TryParseHtmlString(hex, out color); return color; }
        public static Color ColorFor(string color) { return Hex(GameColors.HexFor(color)); }
        Material Material(string hex)
        {
            Material material; if (paints.TryGetValue(hex, out material) && material != null) return material;
            material = ColorMaterial(Hex(hex), .15f); paints[hex] = material; return material;
        }
        static Material ColorMaterial(Color color, float gloss)
        {
            Shader shader = Shader.Find("Standard"); if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = new Material(shader) { color = color }; if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", gloss); return m;
        }
        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent, false); cube.transform.localPosition = position; cube.transform.localScale = scale; cube.GetComponent<Renderer>().sharedMaterial = material; Remove(cube.GetComponent<Collider>()); return cube;
        }
        static TextMesh Text(string name, Transform parent, string text, float size, Color color)
        {
            TextMesh label = Child(name, parent).gameObject.AddComponent<TextMesh>(); label.text = text; label.characterSize = size; label.fontSize = 80; label.color = color; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            try { label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (ArgumentException) { label.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            if (label.font != null) label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            return label;
        }
    }
}
