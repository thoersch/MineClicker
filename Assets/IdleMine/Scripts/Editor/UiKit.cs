using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Shared building blocks for the editor installers: nodes, labels, buttons and Inspector wiring in the
    /// project's style (Lilita One, rounded 9-sliced panels, PressScale on every button).
    /// Call LoadAssets() before building.
    /// </summary>
    internal static class UiKit
    {
        const string ArtRoot = "Assets/IdleMine/Art/";

        /// <summary>Set by "Install All" so each installer skips its own dialog.</summary>
        internal static bool Quiet;

        internal static Font LabelFont;
        internal static Sprite Rounded, Circle, Ring;

        internal static void LoadAssets()
        {
            LabelFont = AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "Fonts/LilitaOne-Regular.ttf");
            Rounded = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Sprites/Rounded.png");
            Circle = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Sprites/Circle.png");
            Ring = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Sprites/Ring.png");
        }


        internal struct Layout
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, Pos, Size;
        }

        internal static Layout TopRow(float y, float height)
        {
            return new Layout { AnchorMin = new Vector2(0, 1), AnchorMax = new Vector2(1, 1), Pivot = new Vector2(0.5f, 1), Pos = new Vector2(0, y), Size = new Vector2(0, height) };
        }

        internal static Layout Bottom(float y, Vector2 size)
        {
            return new Layout { AnchorMin = new Vector2(0.5f, 0), AnchorMax = new Vector2(0.5f, 0), Pivot = new Vector2(0.5f, 0), Pos = new Vector2(0, y), Size = size };
        }

        internal static Layout Centered(Vector2 pos, Vector2 size, float anchorY)
        {
            return new Layout { AnchorMin = new Vector2(0.5f, anchorY), AnchorMax = new Vector2(0.5f, anchorY), Pivot = new Vector2(0.5f, 0.5f), Pos = pos, Size = size };
        }

        internal static Layout Fill()
        {
            return new Layout { AnchorMin = Vector2.zero, AnchorMax = Vector2.one, Pivot = new Vector2(0.5f, 0.5f) };
        }

        internal static RectTransform Node(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            Undo.RegisterCreatedObjectUndo(go, "Install monetization");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, aMin, aMax, pivot, pos, size);
            return rt;
        }

        internal static RectTransform Node(string name, Transform parent, Layout l)
        {
            return Node(name, parent, l.AnchorMin, l.AnchorMax, l.Pivot, l.Pos, l.Size);
        }

        internal static RectTransform Stretch(string name, Transform parent)
        {
            return Node(name, parent, Fill());
        }

        internal static void Place(Transform t, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static RectTransform Card(Transform parent, Vector2 size)
        {
            var card = Node("Card", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Img(card, Rounded, Palette.Panel, true);
            return card;
        }

        internal static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return img;
        }

        internal static Text Label(string name, Transform parent, string text, int size, Color color, Layout l, bool shadow)
        {
            var rt = Node(name, parent, l);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = LabelFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.text = text;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.55f);
                s.effectDistance = new Vector2(0, -3);
            }
            return t;
        }

        internal static Button MakeButton(GameObject go, Image target)
        {
            var b = go.AddComponent<Button>();
            b.targetGraphic = target;
            var c = b.colors;
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            c.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            c.fadeDuration = 0.06f;
            b.colors = c;
            go.AddComponent<PressScale>();
            return b;
        }

        internal static Button PlainButton(string name, Transform parent, string text, Color bg, Color fg, Layout? l, int fontSize)
        {
            var rt = l.HasValue ? Node(name, parent, l.Value) : Node(name, parent, Fill());
            var img = Img(rt, Rounded, bg, true);
            var b = MakeButton(rt.gameObject, img);
            Label("Label", rt, text, fontSize, fg, Fill(), false);
            return b;
        }

        internal static AdButtonView AdButton(string name, Transform parent, string text, Color bg, Layout? l, int fontSize)
        {
            var b = PlainButton(name, parent, text, bg, Palette.Panel, l, fontSize);
            var tag = Node("Ad Tag", b.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-22, -8), new Vector2(78, 46));
            Img(tag, Rounded, Palette.Panel, true).raycastTarget = false;
            Label("Label", tag, "AD", 28, Palette.Gold, Fill(), false);

            var view = b.gameObject.AddComponent<AdButtonView>();
            Set(view, "button", b);
            Set(view, "label", b.transform.Find("Label").GetComponent<Text>());
            Set(view, "adTag", tag.gameObject);
            return view;
        }

        internal static void Wire(Button b, UnityAction action)
        {
            UnityEventTools.AddPersistentListener(b.onClick, action);
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[IdleMine] " + target.GetType().Name + " has no field '" + field + "'."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
