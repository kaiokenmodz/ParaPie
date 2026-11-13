using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParaPie
{
    /// <summary>
    /// The radial menu, built from uGUI at runtime on the game's UI canvas.
    /// Look: cozy sticker-like pills in the Together Card category colours, a cream hub with the
    /// Para's portrait, soft shadows and bouncy easing like the rest of the Paralives UI.
    /// </summary>
    internal class PieView
    {
        private const float PillHeight = 50f;
        private const float HubSize = 150f;

        private readonly TMP_FontAsset _font;
        private readonly GameObject _root;
        private readonly RectTransform _rootRT;
        private readonly CanvasGroup _group;
        private readonly Image _catcher;
        private readonly PieHit _catcherHit;
        private readonly RectTransform _pie;
        private readonly RectTransform _glow, _ring;
        private readonly RectTransform _hub;
        private readonly Image _portrait, _star;
        private readonly RectTransform _portraitRT;
        private readonly TextMeshProUGUI _initial;
        private readonly RectTransform _nameTag, _infoTag;
        private readonly TextMeshProUGUI _nameText, _infoText;
        private readonly RectTransform _chipRow;

        private readonly List<OptionView> _options = new List<OptionView>();
        private readonly List<ChipView> _chips = new List<ChipView>();

        private PieContent _content;
        private List<Slice> _bound = new List<Slice>();
        private bool _visible;
        private bool _closing;
        private float _openTime, _optionsTime, _closeTime;
        private int _hovered = -1;
        private int _chosen = -1;
        private string _shownKey;
        private float _rx, _ry;

        public bool IsAlive { get { return _root != null; } }
        public bool IsOpen { get { return _visible && !_closing; } }

        private class OptionView
        {
            public RectTransform RT;
            public CanvasGroup Group;
            public Image Shadow, Ghost, Body, Icon;
            public RectTransform GhostRT, BadgeRT, VariantRT;
            public TextMeshProUGUI Label, Badge;
            public Image VariantArrow;
            public PieHit Hit, VariantHit;
            public Vector2 Target;
            public float Angle;
            public float Hover;
            public float Tilt;
            public Color Background;
            public bool Greyed;
        }

        private class ChipView
        {
            public RectTransform RT;
            public Image Outline, Portrait, Star;
            public TextMeshProUGUI Initial;
            public PieHit Hit;
            public Chip Data;
            public float Hover;
        }

        public static Canvas CanvasFor(Component ui)
        {
            Canvas parentCanvas = null;
            var w = ui as UIWindow;
            if (w != null) { try { parentCanvas = w.PlayerOwner.UIManager.Canvas; } catch { } }
            if (parentCanvas == null) parentCanvas = ui.GetComponentInParent<Canvas>();
            return parentCanvas != null ? parentCanvas.rootCanvas : null;
        }

        public PieView(Canvas parentCanvas, TMP_FontAsset font)
        {
            _font = font;
            _root = new GameObject("ParaPie");
            _root.layer = parentCanvas.gameObject.layer;
            _rootRT = _root.AddComponent<RectTransform>();
            _rootRT.SetParent(parentCanvas.transform, false);
            Stretch(_rootRT);
            _root.transform.SetAsLastSibling();

            var canvas = _root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = parentCanvas.sortingOrder + 100;
            _root.AddComponent<GraphicRaycaster>();
            _group = _root.AddComponent<CanvasGroup>();

            // Invisible full-screen click catcher: clicking away from the pie closes it (Sims style).
            _catcher = Img("Click catcher", _rootRT, null, new Color(0, 0, 0, 0));
            Stretch(_catcher.rectTransform);
            _catcherHit = _catcher.gameObject.AddComponent<PieHit>();
            _catcherHit.Clicked = b =>
            {
                if (_content != null && _content.OnOutsideClick != null && IsOpen) _content.OnOutsideClick();
            };

            _pie = Rect("Pie", _rootRT, Vector2.zero);
            _pie.anchorMin = _pie.anchorMax = new Vector2(0.5f, 0.5f);

            _glow = Img("Glow", _pie, Sprites.Glow, new Color(Palette.Cream.r, Palette.Cream.g, Palette.Cream.b, 0.75f)).rectTransform;
            _ring = Img("Track", _pie, Sprites.DashedRing, new Color(1f, 1f, 1f, 0.85f)).rectTransform;

            _hub = Rect("Hub", _pie, new Vector2(HubSize, HubSize));
            var hubShadow = Img("Shadow", _hub, Sprites.SoftShadow, Fade(Palette.Shadow, 0.30f)).rectTransform;
            hubShadow.sizeDelta = new Vector2(HubSize + 46, HubSize + 46);
            hubShadow.anchoredPosition = new Vector2(0, -8);
            Img("Outline", _hub, Sprites.Circle, Color.white).rectTransform.sizeDelta = new Vector2(HubSize, HubSize);
            Img("Plate", _hub, Sprites.Circle, Palette.Cream).rectTransform.sizeDelta = new Vector2(HubSize - 10, HubSize - 10);
            var maskImg = Img("Mask", _hub, Sprites.Circle, Color.white);
            maskImg.rectTransform.sizeDelta = new Vector2(HubSize - 22, HubSize - 22);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _portrait = Img("Portrait", maskImg.rectTransform, null, Color.white);
            _portraitRT = _portrait.rectTransform;
            _initial = Text("Initial", maskImg.rectTransform, 64, Palette.Cocoa, FontStyles.Bold);
            _initial.rectTransform.sizeDelta = new Vector2(HubSize, HubSize);
            _star = Img("Storyteller", _hub, Sprites.Star, Palette.Honey);
            _star.rectTransform.sizeDelta = new Vector2(84, 84);

            _nameTag = Rect("NameTag", _hub, new Vector2(140, 38));
            _nameTag.anchoredPosition = new Vector2(0, -HubSize / 2f + 4f);
            Pill("Shadow", _nameTag, Sprites.SoftShadow, Fade(Palette.Shadow, 0.22f), -8, -12, 8, 4, 30f);
            Pill("Outline", _nameTag, Sprites.Circle, Color.white, -3, -3, 3, 3, 22f);
            Pill("Plate", _nameTag, Sprites.Circle, Palette.Paper, 0, 0, 0, 0, 19f);
            _nameText = Text("Name", _nameTag, 21, Palette.Cocoa, FontStyles.Bold);
            Stretch(_nameText.rectTransform);

            _infoTag = Rect("Info", _pie, new Vector2(200, 30));
            Pill("Plate", _infoTag, Sprites.Circle, Fade(Palette.Paper, 0.92f), 0, 0, 0, 0, 15f);
            _infoText = Text("Text", _infoTag, 16, Palette.SoftCocoa, FontStyles.Normal);
            Stretch(_infoText.rectTransform);

            _chipRow = Rect("Chips", _pie, Vector2.zero);

            _root.SetActive(false);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>Next Tick with content opens the pie from scratch (with the intro animation).</summary>
        public void ForceReopen()
        {
            _visible = false;
            _closing = false;
            _shownKey = null;
        }

        public void Tick(PieContent content)
        {
            float now = Time.unscaledTime;
            bool want = content != null && content.Slices.Count > 0;
            if (want)
            {
                if (!_visible || _closing) Open(now, content);
                Bind(content, now);
                UpdateHover();
                Animate(now);
            }
            else if (_visible)
            {
                if (!_closing)
                {
                    _closing = true;
                    _closeTime = now;
                    SetHovered(-1);
                }
                if (AnimateOut(now))
                {
                    _visible = false;
                    _closing = false;
                    _chosen = -1;
                    _shownKey = null;
                    _root.SetActive(false);
                }
            }
        }

        private void Open(float now, PieContent content)
        {
            _visible = true;
            _closing = false;
            _chosen = -1;
            _hovered = -1;
            _openTime = now;
            _optionsTime = now;
            _shownKey = null;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();

            float scale = Plugin.MenuScale.Value * 1.15f;
            _pie.localScale = new Vector3(scale, scale, 1f);
            Vector2 pos = new Vector2(0f, 40f);
            if (content.OpenAtMouse || Plugin.FollowMouse.Value)
            {
                Vector2 local;
                var cam = _root.GetComponent<Canvas>().worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRT, Input.mousePosition, cam, out local))
                    pos = local;
            }
            _desiredPos = pos;
            _snapPosition = true;
        }

        private Vector2 _desiredPos;
        private bool _snapPosition;

        /// <summary>Keeps every slice, the info line and the chips on screen (the pie can open at the mouse).</summary>
        private void KeepOnScreen()
        {
            float minX = -HubSize / 2f, maxX = HubSize / 2f, minY = -HubSize / 2f, maxY = HubSize / 2f;
            for (int i = 0; i < _bound.Count; i++)
            {
                var v = _options[i];
                float w = v.RT.sizeDelta.x, px = v.RT.pivot.x;
                minX = Mathf.Min(minX, v.Target.x - px * w - 14f);
                maxX = Mathf.Max(maxX, v.Target.x + (1f - px) * w + 14f);
                minY = Mathf.Min(minY, v.Target.y - 34f);
                maxY = Mathf.Max(maxY, v.Target.y + 40f);
            }
            if (_infoTag.gameObject.activeSelf)
            {
                minY = Mathf.Min(minY, _infoTag.anchoredPosition.y - 20f);
                minX = Mathf.Min(minX, -_infoTag.sizeDelta.x / 2f);
                maxX = Mathf.Max(maxX, _infoTag.sizeDelta.x / 2f);
            }
            if (_content != null && _content.Chips.Count > 1) minY = Mathf.Min(minY, _chipRow.anchoredPosition.y - 36f);

            float s = _pie.localScale.x;
            var half = _rootRT.rect.size / 2f;
            const float margin = 18f;
            Vector2 target;
            target.x = ClampRange(_desiredPos.x, -half.x + margin - minX * s, half.x - margin - maxX * s);
            target.y = ClampRange(_desiredPos.y, -half.y + margin - minY * s, half.y - margin - maxY * s);
            if (_snapPosition) { _pie.anchoredPosition = target; _snapPosition = false; }
            else _pie.anchoredPosition = Vector2.Lerp(_pie.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        private static float ClampRange(float v, float lo, float hi)
        {
            return lo > hi ? (lo + hi) / 2f : Mathf.Clamp(v, lo, hi);
        }

        /// <summary>Playtest hook: a real pointer click on the "outside" area, through the EventSystem.</summary>
        public void SimulateOutsideClick(PointerEventData.InputButton button)
        {
            if (_root == null || !_root.activeSelf || EventSystem.current == null) return;
            var ped = new PointerEventData(EventSystem.current) { button = button, position = Input.mousePosition };
            ExecuteEvents.Execute(_catcher.gameObject, ped, ExecuteEvents.pointerClickHandler);
        }

        // ------------------------------------------------------------------ binding

        private void Bind(PieContent content, float now)
        {
            _content = content;
            if (content.Key != _shownKey)
            {
                SetHovered(-1);
                if (_shownKey != null) _optionsTime = now;
                _shownKey = content.Key;
            }
            _bound = content.Slices;
            _catcher.raycastTarget = content.OnOutsideClick != null;

            bool story = content.HubIsStoryteller;
            _star.gameObject.SetActive(story);
            _portrait.gameObject.SetActive(!story && content.HubPortrait != null);
            _initial.gameObject.SetActive(!story && content.HubPortrait == null);
            if (!story && content.HubPortrait != null)
            {
                _portrait.sprite = content.HubPortrait;
                Envelope(_portraitRT, content.HubPortrait, HubSize - 22);
            }
            _initial.text = Initial(content.HubName);
            _nameText.text = content.HubName;
            _nameTag.gameObject.SetActive(!string.IsNullOrEmpty(content.HubName));
            _nameTag.sizeDelta = new Vector2(Mathf.Max(110f, _nameText.GetPreferredValues(content.HubName ?? "").x + 40f), 38f);

            int n = _bound.Count;
            while (_options.Count < n) _options.Add(CreateOption(_options.Count));
            for (int i = 0; i < _options.Count; i++) _options[i].RT.gameObject.SetActive(i < n);
            Layout(n);
            for (int i = 0; i < n; i++) BindOption(_options[i], _bound[i]);

            BindChips(content.Chips);
            KeepOnScreen();
        }

        private void Layout(int n)
        {
            _ry = 165f + 15f * Mathf.Max(0, n - 6);
            _rx = _ry * 1.32f + 20f;
            _glow.sizeDelta = new Vector2(_rx * 2f + 360f, _ry * 2f + 330f);
            _ring.sizeDelta = new Vector2(_rx * 1.62f, _ry * 1.78f);
            for (int i = 0; i < n; i++)
            {
                float deg;
                if (n == 1) deg = 90f;
                else if (n == 2) deg = i == 0 ? 180f : 0f;
                else deg = 90f - i * 360f / n;
                float rad = deg * Mathf.Deg2Rad;
                var o = _options[i];
                o.Angle = rad;
                o.Target = new Vector2(Mathf.Cos(rad) * _rx * 0.86f, Mathf.Sin(rad) * _ry);
                // Pills grow outward from the ring: right-side ones extend right, left-side ones left.
                o.RT.pivot = new Vector2(0.5f - 0.42f * Mathf.Cos(rad), 0.5f);
            }
            _infoTag.anchoredPosition = new Vector2(0f, -_ry - 58f);
            _chipRow.anchoredPosition = new Vector2(0f, -_ry - 116f);
        }

        private void BindOption(OptionView v, Slice s)
        {
            string label = s.IsBack ? "‹  " + s.Label : (s.HasChildren ? s.Label + "  ›" : s.Label);
            v.Label.text = label;
            v.Greyed = s.Greyed;
            v.Label.color = s.Greyed ? Fade(s.TextColor, 0.55f) : s.TextColor;
            v.Background = s.Background;
            v.Icon.gameObject.SetActive(s.Icon != null);
            if (s.Icon != null) { v.Icon.sprite = s.Icon; v.Icon.color = s.Greyed ? Fade(Color.white, 0.5f) : Color.white; }

            float textW = v.Label.GetPreferredValues(label).x;
            float left = s.Icon != null ? 50f : 24f;
            float right = s.HasVariants ? 40f : 24f;
            float w = Mathf.Clamp(textW + left + right, s.IsBack ? 110f : 128f, 380f);
            v.RT.sizeDelta = new Vector2(w, PillHeight);
            v.Label.rectTransform.offsetMin = new Vector2(left, 0f);
            v.Label.rectTransform.offsetMax = new Vector2(-right, 0f);

            bool badge = !string.IsNullOrEmpty(s.Badge);
            v.BadgeRT.gameObject.SetActive(badge);
            if (badge)
            {
                v.Badge.text = s.Badge;
                v.BadgeRT.sizeDelta = new Vector2(Mathf.Max(40f, v.Badge.GetPreferredValues(s.Badge).x + 18f), 26f);
            }
            v.VariantRT.gameObject.SetActive(s.HasVariants);
            v.GhostRT.gameObject.SetActive(s.HasVariants || s.HasChildren);
            v.Ghost.color = Color.Lerp(s.Background, Palette.Shadow, 0.12f);
            v.VariantArrow.color = s.TextColor;

            float tilt = 0f;
            if (Plugin.PlayfulTilt.Value && !s.IsBack)
            {
                uint h = (uint)((s.Seed + 1UL) * 2654435761UL >> 7);
                tilt = ((h % 1000) / 1000f - 0.5f) * 5f;
            }
            v.Tilt = tilt;
        }

        private void BindChips(List<Chip> chips)
        {
            int n = chips.Count > 1 ? chips.Count : 0;
            while (_chips.Count < n) _chips.Add(CreateChip());
            const float size = 54f, gap = 14f;
            float total = n * size + (n - 1) * gap;
            for (int i = 0; i < _chips.Count; i++)
            {
                var c = _chips[i];
                bool on = i < n;
                c.RT.gameObject.SetActive(on);
                if (!on) continue;
                var p = chips[i];
                c.Data = p;
                c.RT.anchoredPosition = new Vector2(-total / 2f + size / 2f + i * (size + gap), 0f);
                c.Star.gameObject.SetActive(p.IsStoryteller);
                c.Portrait.gameObject.SetActive(!p.IsStoryteller && p.Portrait != null);
                c.Initial.gameObject.SetActive(!p.IsStoryteller && p.Portrait == null);
                c.Initial.text = Initial(p.Name);
                if (!p.IsStoryteller && p.Portrait != null)
                {
                    c.Portrait.sprite = p.Portrait;
                    Envelope(c.Portrait.rectTransform, p.Portrait, size - 10f);
                }
                c.Outline.color = p.Active ? Color.Lerp(p.Accent, Palette.Shadow, 0.15f) : Color.white;
                c.Hover = Mathf.Lerp(c.Hover, c.Hit.Hovered ? 1f : 0f, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                float s = (p.Active ? 1.12f : 0.92f) + 0.06f * c.Hover;
                c.RT.localScale = new Vector3(s, s, 1f);
            }
        }

        // ------------------------------------------------------------------ interaction

        private void SetHovered(int index)
        {
            if (index == _hovered) return;
            if (_hovered >= 0 && _hovered < _bound.Count && _bound[_hovered].OnHover != null) _bound[_hovered].OnHover(false);
            _hovered = index;
            if (_hovered >= 0 && _bound[_hovered].OnHover != null) _bound[_hovered].OnHover(true);
        }

        private void UpdateHover()
        {
            int hovered = -1;
            for (int i = 0; i < _bound.Count; i++)
                if (_options[i].Hit.Hovered || _options[i].VariantHit.Hovered) { hovered = i; break; }

            if (hovered != _hovered) SetHovered(hovered);
            else if (hovered >= 0 && _bound[hovered].OnHoverStay != null) _bound[hovered].OnHoverStay();

            string info = _content.Hint;
            if (hovered >= 0)
            {
                var s = _bound[hovered];
                info = !string.IsNullOrEmpty(s.Info) ? s.Info : s.Label;
            }
            _infoTag.gameObject.SetActive(!string.IsNullOrEmpty(info));
            _infoText.text = info ?? "";
            _infoTag.sizeDelta = new Vector2(Mathf.Min(620f, _infoText.GetPreferredValues(info ?? "").x + 30f), 30f);
        }

        private void OnOptionClicked(int index, PointerEventData.InputButton button)
        {
            if (!IsOpen || index >= _bound.Count) return;
            var s = _bound[index];
            if (button == PointerEventData.InputButton.Right)
            {
                if (s.OnRightClick != null) s.OnRightClick();
                return;
            }
            if (button != PointerEventData.InputButton.Left || s.Greyed || s.OnClick == null) return;
            if (!s.HasChildren && !s.IsBack) _chosen = index;
            s.OnClick();
        }

        private void OnVariantClicked(int index)
        {
            if (!IsOpen || index >= _bound.Count) return;
            if (_bound[index].OnRightClick != null) _bound[index].OnRightClick();
        }

        // ------------------------------------------------------------------ animation

        private void Animate(float now)
        {
            float dt = Time.unscaledDeltaTime;
            float tOpen = now - _openTime;
            _group.alpha = Mathf.Clamp01(tOpen / 0.16f);
            _group.blocksRaycasts = true;

            float hubE = EaseOutBack(Mathf.Clamp01(tOpen / 0.34f));
            float breathe = 1f + 0.012f * Mathf.Sin(now * 2.2f);
            float hs = Mathf.LerpUnclamped(0.4f, 1f, hubE) * breathe;
            _hub.localScale = new Vector3(hs, hs, 1f);
            float ringE = EaseOutCubic(Mathf.Clamp01(tOpen / 0.4f));
            _ring.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, ringE), Mathf.Lerp(0.6f, 1f, ringE), 1f);
            _ring.localEulerAngles = new Vector3(0, 0, now * 3f);
            _glow.localScale = new Vector3(Mathf.Lerp(0.7f, 1f, ringE), Mathf.Lerp(0.7f, 1f, ringE), 1f);

            float tOpt = now - _optionsTime;
            for (int i = 0; i < _bound.Count; i++)
            {
                var v = _options[i];
                float p = Mathf.Clamp01((tOpt - 0.05f - i * 0.03f) / 0.32f);
                float e = EaseOutBack(p);
                float target = (i == _hovered && !v.Greyed) ? 1f : 0f;
                v.Hover = Mathf.Lerp(v.Hover, target, 1f - Mathf.Exp(-20f * dt));
                Vector2 outward = new Vector2(Mathf.Cos(v.Angle), Mathf.Sin(v.Angle));
                v.RT.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, v.Target, Mathf.LerpUnclamped(0.25f, 1f, e)) + outward * 6f * v.Hover;
                float s = Mathf.LerpUnclamped(0.3f, 1f, e) * (1f + 0.07f * v.Hover);
                v.RT.localScale = new Vector3(s, s, 1f);
                v.RT.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(v.Tilt, 0f, v.Hover));
                v.Group.alpha = Mathf.Clamp01(p * 2.5f);
                Color bg = v.Greyed ? Color.Lerp(v.Background, new Color(0.85f, 0.83f, 0.8f), 0.6f) : v.Background;
                v.Body.color = Color.Lerp(bg, Color.white, 0.14f * v.Hover);
                v.Shadow.color = Fade(Palette.Shadow, (v.Greyed ? 0.1f : 0.20f) + 0.14f * v.Hover);
                v.Shadow.rectTransform.anchoredPosition = new Vector2(0f, -4f - 4f * v.Hover);
            }
        }

        private bool AnimateOut(float now)
        {
            float t = Mathf.Clamp01((now - _closeTime) / 0.22f);
            float e = EaseOutCubic(t);
            _group.alpha = 1f - e;
            _group.blocksRaycasts = false;
            float hs = Mathf.Lerp(1f, 0.85f, e);
            _hub.localScale = new Vector3(hs, hs, 1f);
            for (int i = 0; i < _bound.Count && i < _options.Count; i++)
            {
                var v = _options[i];
                if (i == _chosen)
                {
                    float s = 1.07f + 0.12f * Mathf.Sin(t * Mathf.PI);
                    v.RT.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    v.RT.anchoredPosition = Vector2.Lerp(v.Target, v.Target * 0.6f, e);
                    float s = Mathf.Lerp(1f, 0.6f, e);
                    v.RT.localScale = new Vector3(s, s, 1f);
                }
            }
            return t >= 1f;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }

        private static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }

        // ------------------------------------------------------------------ construction

        private OptionView CreateOption(int index)
        {
            var v = new OptionView();
            v.RT = Rect("Option" + index, _pie, new Vector2(160, PillHeight));
            v.RT.SetSiblingIndex(_hub.GetSiblingIndex()); // above the track, below the hub and its name tag
            v.Group = v.RT.gameObject.AddComponent<CanvasGroup>();

            v.Shadow = Pill("Shadow", v.RT, Sprites.SoftShadow, Fade(Palette.Shadow, 0.2f), -12, -12, 12, 8, PillHeight / 2f + 12f);

            // A second pill peeking out behind: "there is more here" (variants or a submenu).
            v.GhostRT = Rect("Stack", v.RT, Vector2.zero);
            Stretch(v.GhostRT);
            v.GhostRT.anchoredPosition = new Vector2(7f, -6f);
            v.GhostRT.localEulerAngles = new Vector3(0, 0, -4f);
            Pill("Outline", v.GhostRT, Sprites.Circle, Color.white, -3, -3, 3, 3, PillHeight / 2f + 3f);
            v.Ghost = Pill("Body", v.GhostRT, Sprites.Circle, Color.white, 0, 0, 0, 0, PillHeight / 2f);

            Pill("Outline", v.RT, Sprites.Circle, Color.white, -3, -3, 3, 3, PillHeight / 2f + 3f);
            v.Body = Pill("Body", v.RT, Sprites.Circle, Color.white, 0, 0, 0, 0, PillHeight / 2f);
            v.Body.raycastTarget = true;
            v.Hit = v.Body.gameObject.AddComponent<PieHit>();
            int idx = index;
            v.Hit.Clicked = b => OnOptionClicked(idx, b);

            v.Icon = Img("Icon", v.RT, null, Color.white);
            v.Icon.preserveAspect = true;
            var irt = v.Icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(30f, 30f);
            irt.anchoredPosition = new Vector2(28f, 0f);

            v.Label = Text("Label", v.RT, 21, Palette.Cocoa, FontStyles.Bold);
            Stretch(v.Label.rectTransform);

            v.BadgeRT = Rect("Chance", v.RT, new Vector2(44, 26));
            v.BadgeRT.anchorMin = v.BadgeRT.anchorMax = new Vector2(1f, 1f);
            v.BadgeRT.anchoredPosition = new Vector2(-14f, 2f);
            v.BadgeRT.localEulerAngles = new Vector3(0, 0, 6f);
            Pill("Shadow", v.BadgeRT, Sprites.SoftShadow, Fade(Palette.Shadow, 0.22f), -6, -8, 6, 3, 20f);
            Pill("Plate", v.BadgeRT, Sprites.Circle, Palette.Paper, 0, 0, 0, 0, 13f);
            v.Badge = Text("Text", v.BadgeRT, 15, Palette.Cocoa, FontStyles.Bold);
            Stretch(v.Badge.rectTransform);

            v.VariantRT = Rect("Swap", v.RT, new Vector2(32, 32));
            v.VariantRT.anchorMin = v.VariantRT.anchorMax = new Vector2(1f, 0.5f);
            v.VariantRT.anchoredPosition = new Vector2(-22f, 0f);
            var vb = Img("Plate", v.VariantRT, Sprites.Circle, new Color(1f, 1f, 1f, 0.75f));
            vb.rectTransform.sizeDelta = new Vector2(30, 30);
            vb.raycastTarget = true;
            v.VariantArrow = Img("Arrow", v.VariantRT, Sprites.Cycle, Palette.Cocoa);
            v.VariantArrow.rectTransform.sizeDelta = new Vector2(26, 26);
            v.VariantHit = vb.gameObject.AddComponent<PieHit>();
            v.VariantHit.Clicked = b => OnVariantClicked(idx);
            return v;
        }

        private ChipView CreateChip()
        {
            var c = new ChipView();
            c.RT = Rect("Chip", _chipRow, new Vector2(54, 54));
            var sh = Img("Shadow", c.RT, Sprites.SoftShadow, Fade(Palette.Shadow, 0.25f)).rectTransform;
            sh.sizeDelta = new Vector2(70, 70);
            sh.anchoredPosition = new Vector2(0, -4);
            c.Outline = Img("Outline", c.RT, Sprites.Circle, Color.white);
            c.Outline.rectTransform.sizeDelta = new Vector2(54, 54);
            c.Outline.raycastTarget = true;
            c.Hit = c.Outline.gameObject.AddComponent<PieHit>();
            var chip = c;
            c.Hit.Clicked = b =>
            {
                if (b == PointerEventData.InputButton.Left && chip.Data != null && chip.Data.OnClick != null) chip.Data.OnClick();
            };
            Img("Plate", c.RT, Sprites.Circle, Palette.Cream).rectTransform.sizeDelta = new Vector2(46, 46);
            var mask = Img("Mask", c.RT, Sprites.Circle, Color.white);
            mask.rectTransform.sizeDelta = new Vector2(44, 44);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            c.Portrait = Img("Portrait", mask.rectTransform, null, Color.white);
            c.Star = Img("Star", c.RT, Sprites.Star, Palette.Honey);
            c.Star.rectTransform.sizeDelta = new Vector2(32, 32);
            c.Initial = Text("Initial", c.RT, 24, Palette.Cocoa, FontStyles.Bold);
            c.Initial.rectTransform.sizeDelta = new Vector2(54, 54);
            return c;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            return rt;
        }

        private Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent, new Vector2(100, 100));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A 9-sliced pill stretched over its parent with the given edge offsets and corner radius.</summary>
        private Image Pill(string name, Transform parent, Sprite sprite, Color color, float l, float b, float r, float t, float radius)
        {
            var img = Img(name, parent, sprite, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 64f / Mathf.Max(1f, radius);
            var rt = img.rectTransform;
            Stretch(rt);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(r, t);
            return img;
        }

        private TextMeshProUGUI Text(string name, Transform parent, float size, Color color, FontStyles style)
        {
            var rt = Rect(name, parent, new Vector2(200, 40));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
#pragma warning disable 618
            t.enableWordWrapping = false;
#pragma warning restore 618
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Color Fade(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }

        private static string Initial(string name)
        {
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        /// <summary>Sizes a portrait so it fully covers a circle of the given diameter.</summary>
        private static void Envelope(RectTransform rt, Sprite sprite, float diameter)
        {
            float aspect = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 1f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = aspect >= 1f ? new Vector2(diameter * aspect, diameter) : new Vector2(diameter, diameter / aspect);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
