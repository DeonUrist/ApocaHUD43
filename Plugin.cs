using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ApocaHUD
{
    /// Keeps the game's HUD on the screen edges at any aspect ratio.
    ///
    /// Why it hangs: the HUD canvas ("Canvas", CanvasScaler ScaleWithScreenSize 1920x1080, ScreenMatchMode Expand) has every
    /// element anchored to the screen CENTRE with offsets laid out for 16:9 (weapon card at y -489, survival card at -479...).
    /// Expand keeps the full 1920 width on a 4:3 screen, so the canvas becomes 1920x1440 and the bottom row ends up 240 units
    /// above the real bottom edge (on 21:9 the canvas is 2520 wide and the side groups drift inwards the same way).
    ///
    /// Fix: re-anchor each HUD element to the edge/corner it was designed next to, keeping exactly the offset it has from that
    /// edge in the 1920x1080 layout. At 16:9 nothing moves; at any other aspect the bottom row sits on the bottom edge, the
    /// top row on the top edge and corner groups in their corners. Elements in the middle band (crosshair, item names, use
    /// prompts, full-screen overlays) keep their centre anchor.
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.denis.apocalypter.apocahud";
        public const string NAME = "ApocaHUD";
        public const string VERSION = "1.0.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        private static GameObject _runnerGo;

        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show this mod in the Apocasetter Mods menu");
            Enabled = Config.Bind("General", "Enabled", true, "Keep the HUD at the screen edges on any aspect ratio (4:3, 16:10, 21:9...). Off = the game's own layout, made for 16:9.");
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRunner();
            Log.LogInfo(NAME + " " + VERSION + " loaded.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureRunner(); }

        internal static void EnsureRunner()
        {
            if (_runnerGo != null) return;
            _runnerGo = new GameObject("ApocaHUD.Runner");
            _runnerGo.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
            _runnerGo.AddComponent<Runner>();
        }
    }

    internal class Runner : MonoBehaviour
    {
        private float _next;
        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            try { Hud.Tick(Plugin.Enabled.Value); }
            catch (Exception e) { Plugin.Log.LogWarning("HUD layout pass failed: " + e); _next = Time.unscaledTime + 5f; }
        }
    }

    internal static class Hud
    {
        /// Edge bands, as fractions of the 1920x1080 design frame. An element whose pivot lies within the bottom/top 20 % is
        /// anchored to that edge; within such a band, one in the left/right third goes to that corner. The middle band keeps
        /// its centre anchor (use prompts at y -305 / -140, item names, crosshair).
        private const float EdgeY = 0.2f, EdgeX = 1f / 3f;

        private class Orig { public Vector2 AMin, AMax, Pos, Size; }

        private static Canvas _canvas;
        private static bool _applied;
        private static readonly Dictionary<RectTransform, Orig> _orig = new Dictionary<RectTransform, Orig>();
        /// Containers laid out as a 1920x1080 frame (Design_UI, SurvivalCanvas): stretched over the canvas, children
        /// re-anchored inside them. Value = the frame size.
        private static readonly Dictionary<RectTransform, Vector2> _frames = new Dictionary<RectTransform, Vector2>();

        internal static void Tick(bool enabled)
        {
            if (_canvas == null)
            {
                _applied = false; _orig.Clear(); _frames.Clear();
                _canvas = FindHudCanvas();
                if (_canvas == null) return;
            }
            if (enabled) Apply();
            else if (_applied) Restore();
        }

        /// The game's HUD canvas: root object "Canvas" with a ScaleWithScreenSize scaler (the menus are *_Canvas).
        private static Canvas FindHudCanvas()
        {
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isRootCanvas || c.name != "Canvas" || !c.gameObject.scene.IsValid()) continue;
                var s = c.GetComponent<CanvasScaler>();
                if (s == null || s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (c.transform.Find("Design_UI") == null && c.transform.Find("WeaponSlots_UI") == null) continue;
                return c;
            }
            return null;
        }

        /// Idempotent: runs every 0.5 s so objects added later (Apocapocket's card clones) are anchored too. Anchors are read
        /// in the 1920x1080 design frame, so an element that is already edge-anchored keeps the same anchor.
        private static void Apply()
        {
            var root = _canvas.transform as RectTransform;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            Vector2 refRes = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            int changed = 0;
            changed += Layout(root, refRes);
            // Frames found in this pass (and earlier ones).
            var frames = new List<KeyValuePair<RectTransform, Vector2>>(_frames);
            foreach (var kv in frames) if (kv.Key != null) changed += Layout(kv.Key, kv.Value);
            Cover(root, refRes);
            if (!_applied || changed > 0)
                Plugin.Log.LogInfo((_applied ? "Re-anchored " : "HUD anchored to the screen edges: ") + changed + " element(s) (screen " + Screen.width + "x" + Screen.height + ", canvas " + root.rect.width.ToString("0") + "x" + root.rect.height.ToString("0") + ")");
            _applied = true;
        }

        /// Re-anchor the direct children of `parent`, a rect laid out for `frame` (design size).
        private static int Layout(RectTransform parent, Vector2 frame)
        {
            int n = 0;
            if (parent.GetComponent<LayoutGroup>() != null) return 0;   // a layout group positions its children itself
            for (int i = 0; i < parent.childCount; i++)
            {
                var rt = parent.GetChild(i) as RectTransform;
                if (rt == null || _frames.ContainsKey(rt)) continue;
                if (rt.anchorMin != rt.anchorMax) continue;              // already stretches with its parent

                Vector2 a = rt.anchorMin;
                // A 1920x1080 container without a graphic of its own (Design_UI, SurvivalCanvas): stretch it over the
                // parent so its children can sit on the real edges, then lay out its children.
                if (IsFrame(rt, frame))
                {
                    Remember(rt);
                    Vector2 framePos = rt.anchoredPosition, size = rt.sizeDelta;
                    Vector2 off = framePos + Vector2.Scale(a - new Vector2(0.5f, 0.5f), frame);   // offset from the parent centre
                    var lg = rt.GetComponent<LayoutGroup>();
                    if (lg != null)
                    {
                        // A layout group places its rows from its own rect (SurvivalCanvas: LowerLeft, spacing -897, so the
                        // rows only land right in a 1080-high rect). Keep the 1920x1080 frame as it is and pin it to the
                        // corner its content is aligned to.
                        int al = (int)lg.childAlignment;
                        var ca = new Vector2((al % 3) * 0.5f, 1f - (al / 3) * 0.5f);
                        Vector3 was = rt.localPosition;
                        rt.anchorMin = ca; rt.anchorMax = ca;
                        rt.anchoredPosition = off + Vector2.Scale(new Vector2(0.5f, 0.5f) - ca, frame);
                        Apocapocket.Moved(rt, rt.localPosition - was);
                        n++;
                        continue;
                    }
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = off;
                    _frames[rt] = size;
                    n++;
                    continue;
                }

                // Pivot position in the design frame, from its bottom-left corner.
                Vector2 p = Vector2.Scale(a, frame) + rt.anchoredPosition;
                float fy = p.y / frame.y, fx = p.x / frame.x;
                float ay = fy < EdgeY ? 0f : fy > 1f - EdgeY ? 1f : 0.5f;
                float ax = ay == 0.5f ? 0.5f : fx < EdgeX ? 0f : fx > 1f - EdgeX ? 1f : 0.5f;
                // Middle band: centred elements stay centred, and anything the game already anchored elsewhere (the
                // top-left AdjustUI panel) is left alone.
                if (ay == 0.5f && a != new Vector2(0.5f, 0.5f)) continue;
                var na = new Vector2(ax, ay);
                if (na == a) continue;

                Remember(rt);
                Vector3 before = rt.localPosition;
                rt.anchorMin = na; rt.anchorMax = na;
                rt.anchoredPosition = p - Vector2.Scale(na, frame);
                Apocapocket.Moved(rt, rt.localPosition - before);
                n++;
            }
            return n;
        }

        /// Full-screen pictures on the HUD canvas (LoadingScreen: a 1920x1080 RawImage, centred) are sized to COVER the canvas,
        /// aspect kept, overflow cropped, so no game world shows above/below (4:3) or at the sides (21:9). Their children
        /// (the "Loading..." text) keep their size and offsets. Re-checked every pass: follows resolution changes.
        private static void Cover(RectTransform root, Vector2 refRes)
        {
            Vector2 canvas = root.rect.size;
            if (canvas.x <= 0f || canvas.y <= 0f) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var rt = root.GetChild(i) as RectTransform;
                if (rt == null || _frames.ContainsKey(rt)) continue;
                Orig o;
                Vector2 baseSize = _orig.TryGetValue(rt, out o) ? o.Size : rt.sizeDelta;
                if (Mathf.Abs(baseSize.x - refRes.x) > 2f || Mathf.Abs(baseSize.y - refRes.y) > 2f) continue;
                if (rt.anchorMin != new Vector2(0.5f, 0.5f) || rt.anchorMax != rt.anchorMin) continue;
                if (!(rt.GetComponent<Graphic>() is RawImage) && !(rt.GetComponent<Graphic>() is Image)) continue;
                float s = Mathf.Max(canvas.x / baseSize.x, canvas.y / baseSize.y);
                var want = baseSize * s;
                if ((rt.sizeDelta - want).sqrMagnitude < 0.25f) continue;
                Remember(rt);
                rt.sizeDelta = want;
                Plugin.Log.LogInfo(rt.name + " sized to cover the screen: " + want.x.ToString("0") + "x" + want.y.ToString("0"));
            }
        }

        private static bool IsFrame(RectTransform rt, Vector2 frame)
        {
            if (Mathf.Abs(rt.sizeDelta.x - frame.x) > 2f || Mathf.Abs(rt.sizeDelta.y - frame.y) > 2f) return false;
            if (rt.anchorMin != new Vector2(0.5f, 0.5f)) return false;
            if (rt.GetComponent<Graphic>() != null) return false;   // a full-screen picture (loading screen) keeps its size
            return rt.childCount > 0;
        }

        private static void Remember(RectTransform rt)
        {
            if (_orig.ContainsKey(rt)) return;
            _orig[rt] = new Orig { AMin = rt.anchorMin, AMax = rt.anchorMax, Pos = rt.anchoredPosition, Size = rt.sizeDelta };
        }

        private static void Restore()
        {
            int n = 0;
            foreach (var kv in _orig)
            {
                var rt = kv.Key; if (rt == null) continue;
                Vector3 before = rt.localPosition;
                rt.anchorMin = kv.Value.AMin; rt.anchorMax = kv.Value.AMax; rt.sizeDelta = kv.Value.Size; rt.anchoredPosition = kv.Value.Pos;
                Apocapocket.Moved(rt, rt.localPosition - before);
                n++;
            }
            _orig.Clear(); _frames.Clear();
            _applied = false;
            Plugin.Log.LogInfo("HUD back to the game's layout (" + n + " element(s))");
        }
    }

    /// Apocapocket moves the hints left of the weapon card aside while slots 4-6 are shown by writing a remembered
    /// localPosition every frame (ExtraSlots._shifted: T, Orig, Delta). When we re-anchor one of those after it was
    /// remembered, its remembered position moves by the same amount, or Apocapocket would put it back mid-screen.
    internal static class Apocapocket
    {
        private static bool _looked;
        private static FieldInfo _list, _t, _orig;

        internal static void Moved(Transform t, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-6f) return;
            if (!_looked)
            {
                _looked = true;
                try
                {
                    Type es = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        if (asm.GetName().Name == "Apocapocket") { es = asm.GetType("Apocapocket.ExtraSlots"); break; }
                    if (es == null) return;
                    _list = es.GetField("_shifted", BindingFlags.NonPublic | BindingFlags.Static);
                    var sh = es.GetNestedType("Shifted", BindingFlags.NonPublic);
                    if (sh != null) { _t = sh.GetField("T"); _orig = sh.GetField("Orig"); }
                    if (_list == null || _t == null || _orig == null) { _list = null; Plugin.Log.LogInfo("Apocapocket found, but not its shifted-UI list (version changed?)"); }
                }
                catch (Exception e) { _list = null; Plugin.Log.LogWarning("Apocapocket lookup failed: " + e.Message); }
            }
            if (_list == null) return;
            var list = _list.GetValue(null) as IList;
            if (list == null) return;
            foreach (var entry in list)
            {
                if (entry == null || !ReferenceEquals(_t.GetValue(entry), t)) continue;
                _orig.SetValue(entry, (Vector3)_orig.GetValue(entry) + delta);
            }
        }
    }
}
