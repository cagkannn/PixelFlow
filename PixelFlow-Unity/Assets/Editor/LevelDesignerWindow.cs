using System;
using System.Collections.Generic;
using System.IO;
using PixelFlow.Core;
using UnityEditor;
using UnityEngine;

namespace PixelFlow.Editor
{
    /// <summary>A paintable level draft; the supplied reference levels are never overwritten.</summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private const string ReferencePath = "Assets/Resources/Levels/reference-levels.json";
        private const string CustomPath = "Assets/Resources/Levels/custom-levels.json";
        private const string SceneMenu = "PixelFlow/Oyun sahnesini hazırla";
        private static readonly string[] ColorLabels = { "Mor", "Sarı", "Turkuaz", "Pembe", "Mavi", "Yeşil", "Beyaz", "Siyah", "Lavanta" };

        [SerializeField] private LevelDefinition draft;
        [SerializeField] private string brush = "purple";
        [SerializeField] private bool modified;
        [SerializeField] private float cellSize = 22;
        private readonly List<LevelDefinition> library = new List<LevelDefinition>();
        private readonly List<string> libraryLabels = new List<string>();
        private int selectedLibrary = -1;
        private Vector2 gridScroll, fleetScroll, settingsScroll;
        private bool painting, eraseStroke;
        private int paintControl;
        private Vector2Int previousCell;
        private string message = "";
        private MessageType messageType = MessageType.Info;

        [MenuItem("PixelFlow/Bölüm tasarla", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<LevelDesignerWindow>();
            window.titleContent = new GUIContent("Bölüm tasarla");
            window.minSize = new Vector2(850, 610);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Bölüm tasarla");
            minSize = new Vector2(850, 610);
            if (draft == null) NewDraft();
            NormalizeDraft();
            ReloadLibrary();
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            if (painting) GUIUtility.hotControl = 0;
        }

        private void OnGUI()
        {
            if (draft == null) NewDraft();
            NormalizeDraft();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Kendi bölümünü kur", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Bir renk seçip kareleri boya. Sağ tık siler. Alttaki araçları hazırla, kaydet ve sahnede dene. Hazır bölümlerin aslı değişmez.", MessageType.Info);
            DrawToolbar();
            if (!String.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(position.width * .57f, 430, 700)));
            DrawPicture();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            DrawSettings();
            DrawCounts();
            DrawFleet();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            DrawSaveBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Yeni bölüm", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                // Keep an unsaved draft available in Unity's undo history.
                Record("Yeni bölüm aç"); NewDraft(); modified = true; selectedLibrary = -1;
            }
            if (GUILayout.Button("Dosya aç", EditorStyles.toolbarButton, GUILayout.Width(70))) ImportDraft();
            if (GUILayout.Button("Dosyaya kaydet", EditorStyles.toolbarButton, GUILayout.Width(100))) ExportDraft();
            GUILayout.FlexibleSpace();
            if (library.Count > 0)
            {
                string[] labels = libraryLabels.ToArray();
                int selected = EditorGUILayout.Popup(selectedLibrary, labels, EditorStyles.toolbarPopup, GUILayout.Width(260));
                if (selected >= 0 && selected != selectedLibrary)
                {
                    Record("Bölüm örneği aç");
                    draft = Copy(library[selected]);
                    bool reference = !IsCustomId(draft.id);
                    if (reference)
                    {
                        draft.id = NewId();
                        draft.title += " · Taslağım";
                        draft.provenance = "Hazır bölümden düzenlenen kişisel taslak. " + (draft.provenance ?? "");
                    }
                    selectedLibrary = selected; modified = reference; message = "";
                    NormalizeDraft(); GUI.FocusControl(null);
                }
            }
            if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(55))) ReloadLibrary();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPicture()
        {
            EditorGUI.BeginChangeCheck();
            string title = EditorGUILayout.TextField("Bölüm adı", draft.title ?? "");
            if (EditorGUI.EndChangeCheck()) { Record("Bölüm adını değiştir"); draft.title = title; Changed(); }
            EditorGUILayout.BeginHorizontal();
            int width = Mathf.Clamp(EditorGUILayout.IntField("Genişlik", draft.width), 1, 32);
            int height = Mathf.Clamp(EditorGUILayout.IntField("Yükseklik", draft.height), 1, 32);
            EditorGUILayout.EndHorizontal();
            if (width != draft.width || height != draft.height) Resize(width, height);
            cellSize = EditorGUILayout.Slider("Kare boyutu", cellSize, 12, 28);
            DrawPalette();
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));
            float gridWidth = draft.width * cellSize, gridHeight = draft.height * cellSize;
            Rect area = GUILayoutUtility.GetRect(gridWidth + 12, gridHeight + 12, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            Rect grid = new Rect(area.x + 6, area.y + 6, gridWidth, gridHeight);
            EditorGUI.DrawRect(new Rect(grid.x - 2, grid.y - 2, grid.width + 4, grid.height + 4), new Color(.17f, .15f, .18f));
            for (int row = 0; row < draft.height; row++)
                for (int col = 0; col < draft.width; col++)
                {
                    string name = draft.cells[row * draft.width + col];
                    Rect cell = new Rect(grid.x + col * cellSize, grid.y + row * cellSize, cellSize - 1, cellSize - 1);
                    Color color = String.IsNullOrEmpty(name) ? new Color(.39f, .36f, .39f) : ColorFor(name);
                    EditorGUI.DrawRect(cell, color);
                    if (!String.IsNullOrEmpty(name) && cellSize >= 16)
                    {
                        EditorGUI.DrawRect(new Rect(cell.x + 1, cell.y + 1, cell.width - 2, 2), Color.Lerp(color, Color.white, .38f));
                        EditorGUI.DrawRect(new Rect(cell.x + 1, cell.yMax - 3, cell.width - 2, 2), Color.Lerp(color, Color.black, .28f));
                    }
                }
            HandlePaint(grid);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(modified ? "Kaydedilmemiş değişiklikler var." : "Taslak hazır.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Çizimi temizle", GUILayout.Width(110)))
            {
                Record("Çizimi temizle"); draft.cells = new string[draft.width * draft.height]; Changed();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPalette()
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < GameColors.Names.Length; i++)
            {
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = ColorFor(GameColors.Names[i]);
                bool selected = brush == GameColors.Names[i];
                if (GUILayout.Button(new GUIContent(selected ? "●" : "", ColorLabels[i]), GUILayout.Width(28), GUILayout.Height(27))) brush = GameColors.Names[i];
                GUI.backgroundColor = old;
            }
            if (GUILayout.Toggle(String.IsNullOrEmpty(brush), "Silgi", "Button", GUILayout.Width(55), GUILayout.Height(27))) brush = "";
            EditorGUILayout.EndHorizontal();
        }

        private void HandlePaint(Rect grid)
        {
            Event current = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            bool inside = grid.Contains(current.mousePosition);
            if (current.type == EventType.MouseDown && inside && (current.button == 0 || current.button == 1))
            {
                Record("Kareleri boya"); painting = true; eraseStroke = current.button == 1;
                paintControl = id; GUIUtility.hotControl = id;
                previousCell = CellAt(grid, current.mousePosition);
                PaintCell(previousCell.x, previousCell.y, eraseStroke ? "" : brush); current.Use();
            }
            else if (current.type == EventType.MouseDrag && painting && GUIUtility.hotControl == paintControl)
            {
                if (inside)
                {
                    Vector2Int cell = CellAt(grid, current.mousePosition);
                    PaintLine(previousCell, cell, eraseStroke ? "" : brush); previousCell = cell;
                }
                current.Use();
            }
            else if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && painting)
            {
                painting = false; GUIUtility.hotControl = 0;
                if (current.type == EventType.MouseUp) current.Use();
            }
        }

        private Vector2Int CellAt(Rect rect, Vector2 point)
        {
            return new Vector2Int(Mathf.Clamp((int)((point.x - rect.x) / cellSize), 0, draft.width - 1), Mathf.Clamp((int)((point.y - rect.y) / cellSize), 0, draft.height - 1));
        }

        private void PaintLine(Vector2Int a, Vector2Int b, string color)
        {
            int dx = Math.Abs(b.x - a.x), dy = Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                PaintCell(a.x, a.y, color);
                if (a == b) break;
                int doubled = error * 2;
                if (doubled > -dy) { error -= dy; a.x += sx; }
                if (doubled < dx) { error += dx; a.y += sy; }
            }
        }

        private void PaintCell(int col, int row, string color)
        {
            int index = row * draft.width + col;
            if ((draft.cells[index] ?? "") == color) return;
            draft.cells[index] = color; Changed(); Repaint();
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Oyun kuralları", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int active = EditorGUILayout.IntSlider("Yoldaki UFO", draft.rules.maxActive, 1, 5);
            int waiting = EditorGUILayout.IntSlider("Bekleme yeri", draft.rules.waitingSlots, 1, 5);
            int lanes = EditorGUILayout.IntSlider("Kalkış sütunu", draft.rules.queueColumns, 0, 3);
            float seconds = EditorGUILayout.Slider("Bir turun süresi", (float)draft.rules.tourDuration, 3, 12);
            bool strict = EditorGUILayout.Toggle("Pedler dolunca kaybet", draft.rules.strictDock);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Bölüm kuralını değiştir");
                draft.rules.maxActive = active; draft.rules.waitingSlots = waiting; draft.rules.queueColumns = lanes;
                draft.rules.tourDuration = seconds; draft.rules.strictDock = strict;
                foreach (ShipDefinition ship in draft.fleet) if (ship != null) ship.lane = Mathf.Clamp(ship.lane, 0, Math.Max(1, lanes) - 1);
                Changed();
            }
            if (draft.rules.queueColumns == 0) EditorGUILayout.LabelField("0 sütun: sıradaki bütün UFO’lar seçilebilir.", EditorStyles.miniLabel);
        }

        private Dictionary<string, int> Counts()
        {
            var counts = new Dictionary<string, int>();
            foreach (string color in draft.cells)
                if (!String.IsNullOrEmpty(color)) { if (!counts.ContainsKey(color)) counts[color] = 0; counts[color]++; }
            return counts;
        }

        private void DrawCounts()
        {
            Dictionary<string, int> counts = Counts();
            var energy = new Dictionary<string, int>();
            foreach (ShipDefinition ship in draft.fleet)
                if (ship != null && !String.IsNullOrEmpty(ship.color)) { if (!energy.ContainsKey(ship.color)) energy[ship.color] = 0; energy[ship.color] += ship.ammo; }
            int total = 0; bool balanced = true;
            foreach (string color in GameColors.Names)
            {
                int needed = counts.ContainsKey(color) ? counts[color] : 0;
                int available = energy.ContainsKey(color) ? energy[color] : 0;
                if (needed == 0 && available == 0) continue;
                total += needed; balanced &= needed == available;
                EditorGUILayout.BeginHorizontal();
                Rect swatch = GUILayoutUtility.GetRect(12, 17, GUILayout.Width(12)); EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 3, 11, 11), ColorFor(color));
                EditorGUILayout.LabelField(GameColors.TurkishName(color) + ": " + needed + " kare / " + available + " atış", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Toplam: " + total + " kare", EditorStyles.miniBoldLabel);
            if (total > 0 && !balanced) EditorGUILayout.HelpBox("Kare sayısı ile araçların atış hakkı farklı. Bu düzeni bilerek deneyebilir veya aşağıdaki düğmeyle eşitleyebilirsin.", MessageType.Warning);
        }

        private void DrawFleet()
        {
            EditorGUILayout.Space(5); EditorGUILayout.LabelField("UFO sırası", EditorStyles.boldLabel);
            if (GUILayout.Button("Karelere yetecek UFO’ları hazırla")) GenerateFleet();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Renk", GUILayout.MinWidth(70)); GUILayout.Label("Atış", GUILayout.Width(48)); GUILayout.Label("Sütun", GUILayout.Width(45)); GUILayout.Label("Sıra", GUILayout.Width(68));
            EditorGUILayout.EndHorizontal();
            fleetScroll = EditorGUILayout.BeginScrollView(fleetScroll, GUILayout.MinHeight(130), GUILayout.MaxHeight(Mathf.Max(170, position.height - 430)));
            for (int i = 0; i < draft.fleet.Length; i++)
            {
                ShipDefinition ship = draft.fleet[i];
                if (ship == null) ship = draft.fleet[i] = new ShipDefinition { color = "purple", ammo = 20, lane = 0 };
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                int chosen = EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(GameColors.Names, ship.color)), ColorLabels, GUILayout.MinWidth(70));
                int ammo = Mathf.Clamp(EditorGUILayout.IntField(ship.ammo, GUILayout.Width(48)), 1, 999);
                int lane = Mathf.Clamp(EditorGUILayout.IntField(ship.lane + 1, GUILayout.Width(45)) - 1, 0, Math.Max(1, draft.rules.queueColumns) - 1);
                if (EditorGUI.EndChangeCheck()) { Record("UFO’yu değiştir"); ship.color = GameColors.Names[chosen]; ship.ammo = ammo; ship.lane = lane; Changed(); }
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(22))) { Record("UFO’yu öne al"); SwapShips(i, i - 1); }
                using (new EditorGUI.DisabledScope(i == draft.fleet.Length - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(22))) { Record("UFO’yu arkaya al"); SwapShips(i, i + 1); }
                bool remove = GUILayout.Button("×", GUILayout.Width(22));
                EditorGUILayout.EndHorizontal();
                if (remove) { Record("UFO’yu kaldır"); ArrayUtility.RemoveAt(ref draft.fleet, i); Changed(); break; }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(draft.fleet.Length >= 40))
                if (GUILayout.Button("+ UFO ekle"))
                {
                    Record("UFO ekle");
                    ArrayUtility.Add(ref draft.fleet, new ShipDefinition { color = String.IsNullOrEmpty(brush) ? "purple" : brush, ammo = 20, lane = draft.fleet.Length % Math.Max(1, draft.rules.queueColumns) }); Changed();
                }
            EditorGUILayout.LabelField(draft.fleet.Length + " / 40 UFO", EditorStyles.miniLabel);
        }

        private void SwapShips(int first, int second)
        {
            ShipDefinition ship = draft.fleet[first]; draft.fleet[first] = draft.fleet[second]; draft.fleet[second] = ship; Changed();
        }

        private void GenerateFleet()
        {
            Dictionary<string, int> counts = Counts();
            if (counts.Count == 0) { SetMessage("Önce çizime en az bir kare ekle.", MessageType.Warning); return; }
            Record("Karelere göre UFO hazırla");
            var batches = new List<List<ShipDefinition>>(); int maximum = 0;
            foreach (string color in GameColors.Names)
            {
                if (!counts.ContainsKey(color)) continue;
                int count = counts[color], pieces = Mathf.CeilToInt(count / 50f);
                var ships = new List<ShipDefinition>();
                for (int i = 0; i < pieces; i++) ships.Add(new ShipDefinition { color = color, ammo = count / pieces + (i < count % pieces ? 1 : 0) });
                batches.Add(ships); maximum = Math.Max(maximum, pieces);
            }
            var fleet = new List<ShipDefinition>();
            for (int row = 0; row < maximum; row++) foreach (List<ShipDefinition> batch in batches) if (row < batch.Count) fleet.Add(batch[row]);
            for (int i = 0; i < fleet.Count; i++) fleet[i].lane = i % Math.Max(1, draft.rules.queueColumns);
            draft.fleet = fleet.ToArray(); Changed(); SetMessage("Her renk için toplam atış hakkı kare sayısına eşitlendi. Sırayı değiştirip farklı kararlar tasarlayabilirsin.", MessageType.Info);
        }

        private void DrawSaveBar()
        {
            EditorGUILayout.Space(4); EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Bölümü kaydet", GUILayout.Width(140), GUILayout.Height(30))) SaveDraft();
            if (GUILayout.Button("Kaydet ve deneme sahnesini aç", GUILayout.Width(230), GUILayout.Height(30)))
            {
                if (SaveDraft())
                {
                    EditorPrefs.SetString("PixelFlow.EditorTestLevelId", draft.id);
                    if (!EditorApplication.ExecuteMenuItem(SceneMenu)) SetMessage("Bölüm kaydedildi. PixelFlow menüsünden oyun sahnesini açıp Play düğmesine bas.", MessageType.Info);
                }
            }
            EditorGUILayout.EndHorizontal(); EditorGUILayout.Space(5);
        }

        private void NewDraft()
        {
            draft = new LevelDefinition { id = NewId(), title = "Yeni bölüm", width = 16, height = 16, cells = new string[256], rules = new Rules(), fleet = new ShipDefinition[0], provenance = "Unity bölüm tasarım alanında hazırlanmış kişisel bölüm." };
            modified = false; selectedLibrary = -1; message = "";
        }

        private void NormalizeDraft()
        {
            draft.width = Mathf.Clamp(draft.width, 1, 32); draft.height = Mathf.Clamp(draft.height, 1, 32);
            if (draft.cells == null || draft.cells.Length != draft.width * draft.height) Array.Resize(ref draft.cells, draft.width * draft.height);
            for (int i = 0; i < draft.cells.Length; i++) if (draft.cells[i] == ".") draft.cells[i] = "";
            if (draft.rules == null) draft.rules = new Rules();
            if (draft.fleet == null) draft.fleet = new ShipDefinition[0];
            if (String.IsNullOrEmpty(draft.id)) draft.id = NewId();
        }

        private void Resize(int width, int height)
        {
            Record("Resim boyutunu değiştir");
            string[] resized = new string[width * height];
            int offsetX = (width - draft.width) / 2, offsetY = (height - draft.height) / 2;
            for (int row = 0; row < draft.height; row++) for (int col = 0; col < draft.width; col++)
            {
                int x = col + offsetX, y = row + offsetY;
                if (x >= 0 && x < width && y >= 0 && y < height) resized[y * width + x] = draft.cells[row * draft.width + col];
            }
            draft.width = width; draft.height = height; draft.cells = resized; Changed();
        }

        private void ReloadLibrary()
        {
            library.Clear(); libraryLabels.Clear();
            AppendLibrary(ReferencePath, "Örnek: "); AppendLibrary(CustomPath, "Benim: ");
            selectedLibrary = -1; Repaint();
        }

        private void AppendLibrary(string path, string prefix)
        {
            if (!File.Exists(path)) return;
            try
            {
                LevelCollection collection = JsonUtility.FromJson<LevelCollection>(File.ReadAllText(path));
                if (collection == null || collection.levels == null) return;
                foreach (LevelDefinition definition in collection.levels)
                {
                    if (definition == null) continue;
                    library.Add(definition); libraryLabels.Add(prefix + definition.title);
                }
            }
            catch (Exception error) { SetMessage("Bölüm listesi okunamadı: " + error.Message, MessageType.Warning); }
        }

        private bool SaveDraft()
        {
            try
            {
                ValidateDraft();
                if (!IsCustomId(draft.id)) draft.id = NewId();
                var saved = new List<LevelDefinition>();
                if (File.Exists(CustomPath))
                {
                    LevelCollection existing = JsonUtility.FromJson<LevelCollection>(File.ReadAllText(CustomPath));
                    if (existing == null || existing.levels == null) throw new InvalidDataException("Kendi bölüm dosyan okunamadı; var olan dosyanın üzerine yazılmadı.");
                    foreach (LevelDefinition definition in existing.levels) if (definition != null) saved.Add(definition);
                }
                int index = saved.FindIndex(level => level.id == draft.id);
                LevelDefinition copy = Copy(draft);
                if (index < 0) saved.Add(copy); else saved[index] = copy;
                Directory.CreateDirectory(Path.GetDirectoryName(CustomPath));
                string json = JsonUtility.ToJson(new LevelCollection { levels = saved.ToArray() }, true);
                string backupDirectory = Path.Combine("Library", "PixelFlowLevelBackups");
                Directory.CreateDirectory(backupDirectory);
                string temporary = Path.Combine(backupDirectory, "custom-levels.json.tmp");
                File.WriteAllText(temporary, json);
                if (File.Exists(CustomPath)) File.Copy(CustomPath, Path.Combine(backupDirectory, "custom-levels.json.bak"), true);
                File.Copy(temporary, CustomPath, true); File.Delete(temporary);
                modified = false; AssetDatabase.ImportAsset(CustomPath, ImportAssetOptions.ForceUpdate); ReloadLibrary();
                SetMessage("Bölüm kaydedildi. Sahneyi yeniden çalıştırınca kendi bölümlerin de sıraya eklenir.", MessageType.Info); return true;
            }
            catch (Exception error) { SetMessage(error.Message, MessageType.Error); return false; }
        }

        private void ValidateDraft()
        {
            if (String.IsNullOrWhiteSpace(draft.title)) throw new InvalidDataException("Bölümüne bir ad ver.");
            if (draft.fleet == null || draft.fleet.Length == 0) throw new InvalidDataException("Önce UFO ekle veya karelere yetecek UFO’ları hazırla düğmesine bas.");
            GameEngine.Create(draft);
        }

        private void ExportDraft()
        {
            try
            {
                ValidateDraft();
                string file = EditorUtility.SaveFilePanel("Bölüm dosyasını sakla", "", "PixelFlow-Bolum.json", "json");
                if (String.IsNullOrEmpty(file)) return;
                File.WriteAllText(file, JsonUtility.ToJson(Copy(draft), true));
                SetMessage("Bölüm dosyası saklandı. Başka bir projede Dosya aç ile yeniden alabilirsin.", MessageType.Info);
            }
            catch (Exception error) { SetMessage(error.Message, MessageType.Error); }
        }

        private void ImportDraft()
        {
            string file = EditorUtility.OpenFilePanel("Bölüm dosyasını aç", "", "json");
            if (String.IsNullOrEmpty(file)) return;
            try
            {
                string json = File.ReadAllText(file);
                LevelDefinition imported = JsonUtility.FromJson<LevelDefinition>(json);
                if (imported == null || imported.cells == null)
                {
                    LevelCollection collection = JsonUtility.FromJson<LevelCollection>(json);
                    if (collection == null || collection.levels == null || collection.levels.Length == 0) throw new InvalidDataException("Bu dosyada PixelFlow bölüm resmi bulunamadı.");
                    imported = collection.levels[0];
                    SetMessage("Dosyadaki ilk bölüm açıldı. Diğer bölümler dosyada korunuyor.", MessageType.Info);
                }
                if (imported.width < 1 || imported.width > 32 || imported.height < 1 || imported.height > 32 || imported.cells.Length != imported.width * imported.height)
                    throw new InvalidDataException("Bölüm resmi 1–32 kare boyutunda olmalı; kare sayısı resme uymalı.");
                foreach (string color in imported.cells) if (!String.IsNullOrEmpty(color) && color != "." && !GameColors.IsKnown(color)) throw new InvalidDataException("Resimde bilinmeyen bir renk var.");
                Record("Bölüm dosyasını aç"); draft = Copy(imported); draft.id = NewId(); NormalizeDraft(); modified = true; selectedLibrary = -1; GUI.FocusControl(null);
            }
            catch (Exception error) { SetMessage("Dosya açılamadı: " + error.Message, MessageType.Error); }
        }

        private void Record(string action) { Undo.RecordObject(this, action); }
        private void Changed() { modified = true; EditorUtility.SetDirty(this); }
        private void SetMessage(string text, MessageType type) { message = text; messageType = type; Repaint(); }
        private static string NewId() { return "custom-" + Guid.NewGuid().ToString("N"); }
        private static bool IsCustomId(string id) { return !String.IsNullOrEmpty(id) && id.StartsWith("custom-", StringComparison.Ordinal); }
        private static LevelDefinition Copy(LevelDefinition source) { return JsonUtility.FromJson<LevelDefinition>(JsonUtility.ToJson(source)); }
        private static Color ColorFor(string name) { Color color; return ColorUtility.TryParseHtmlString(GameColors.HexFor(name), out color) ? color : Color.magenta; }
    }
}
