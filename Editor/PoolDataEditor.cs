using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// Custom Inspector for <see cref="PoolData"/>: each <see cref="PoolGroup"/> is a box with an
    /// editable header and its entries laid out in a horizontal, wrapping card grid - each card shows
    /// a real 3D preview of its prefab, its name, and an initialize-count stepper - capped at roughly
    /// two visible rows with a scroll + fade hint when a group holds more entries than that. A "+"
    /// grows the current group; a "+" below every group adds a whole new one. "Compile" hands off to
    /// <see cref="PoolCompiler"/>.
    /// </summary>
    [CustomEditor(typeof(PoolData))]
    internal sealed class PoolDataEditor : UnityEditor.Editor
    {
        private const float CardSize = 72f;
        private const float CardInnerPadding = 4f;
        private const float CardGap = 10f;
        private const float FieldHeight = 18f;
        private const float FieldSpacing = 4f;
        private const float StepperButtonWidth = 16f;
        private const float DeleteButtonSize = 16f;
        private const float FadeHeight = 16f;
        private const float HeaderHeight = 28f;

        private const float CardOuterWidth = CardSize + CardInnerPadding * 2f;
        private const float CardOuterHeight = CardSize + CardInnerPadding * 2f + FieldHeight * 2f + FieldSpacing * 2f;
        private const float CardWidth = CardOuterWidth + CardGap; // column width used for wrap math
        private const float RowHeight = CardOuterHeight + CardGap;

        private readonly Dictionary<PoolGroup, Vector2> _scrollPositions = new();
        private float _cachedViewWidth = 400f;
        private GUIStyle _headerStyle;
        private GUIStyle _deleteButtonStyle;
        private GUIStyle _hintLabelStyle;

        private GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            fixedHeight = HeaderHeight
        };

        private GUIStyle DeleteButtonStyle => _deleteButtonStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 9,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };

        private GUIStyle HintLabelStyle => _hintLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };

        private static Color CardBackgroundColor =>
            EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.65f, 0.65f, 0.65f);

        private static Color PreviewBackgroundColor =>
            EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.5f, 0.5f, 0.5f);

        private static Color StepperBackgroundColor =>
            EditorGUIUtility.isProSkin ? new Color(0.26f, 0.26f, 0.26f) : new Color(0.72f, 0.72f, 0.72f);

        private static readonly Color FocusHighlightColor = new(0.24f, 0.49f, 0.9f);

        public override void OnInspectorGUI()
        {
            var poolData = (PoolData)target;

            PoolGroup groupPendingRemoval = null;

            for (var i = 0; i < poolData.Groups.Count; i++)
            {
                var group = poolData.Groups[i];
                DrawGroup(poolData, group, () => groupPendingRemoval = group);
                EditorGUILayout.Space(6);
            }

            if (groupPendingRemoval != null)
            {
                Undo.RecordObject(poolData, "Remove Pool Group");
                poolData.Groups.Remove(groupPendingRemoval);
                _scrollPositions.Remove(groupPendingRemoval);
                EditorUtility.SetDirty(poolData);
            }

            if (GUILayout.Button("+ Add Group", GUILayout.Height(28)))
            {
                Undo.RecordObject(poolData, "Add Pool Group");
                poolData.Groups.Add(new PoolGroup());
                EditorUtility.SetDirty(poolData);
            }

            EditorGUILayout.Space(14);

            var buttonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.75f, 0.4f);
            if (GUILayout.Button("Compile", GUILayout.Height(34)))
            {
                PoolCompiler.Compile(poolData);
            }
            GUI.backgroundColor = buttonColor;

            if (GUI.changed)
            {
                EditorUtility.SetDirty(poolData);
            }
        }

        private void DrawGroup(PoolData poolData, PoolGroup group, System.Action requestRemoveGroup)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newHeader = EditorGUILayout.TextField(group.Header, HeaderStyle, GUILayout.Height(HeaderHeight));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(poolData, "Rename Pool Group");
                group.Header = newHeader;
            }

            if (GUILayout.Button("✕", GUILayout.Width(HeaderHeight), GUILayout.Height(HeaderHeight)))
            {
                requestRemoveGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            DrawEntryGrid(poolData, group);

            EditorGUILayout.EndVertical();
        }

        private void DrawEntryGrid(PoolData poolData, PoolGroup group)
        {
            // Cache the view width, updating it only on the Layout event. Reading
            // EditorGUIUtility.currentViewWidth fresh on every event type (Layout, Repaint, MouseDown -
            // each a separate OnInspectorGUI invocation) can return a value that differs by a pixel or two
            // between passes (e.g. the Inspector's own scrollbar toggling based on the height we report).
            // That is enough to flip perRow across an integer boundary between passes, changing how many
            // BeginHorizontal/EndHorizontal wrap resets run - which desyncs Unity's automatic control-ID
            // allocation and makes two unrelated TextFields share the same "currently being edited" buffer.
            // Locking the width to what Layout saw keeps the wrap structure - and therefore every control ID
            // derived from it - identical across every pass in this GUI cycle.
            if (Event.current.type == EventType.Layout)
            {
                _cachedViewWidth = EditorGUIUtility.currentViewWidth;
            }

            var viewWidth = _cachedViewWidth - 40f;
            var perRow = Mathf.Max(1, Mathf.FloorToInt(viewWidth / CardWidth));

            var cardCount = group.Entries.Count + 1; // +1 for the "add entry" card
            var rowCount = Mathf.CeilToInt(cardCount / (float)perRow);
            var contentHeight = rowCount * RowHeight;
            var visibleHeight = Mathf.Min(contentHeight, RowHeight * 1.5f); // ~2 rows, 2nd half-cut

            _scrollPositions.TryGetValue(group, out var scroll);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(visibleHeight));

            PoolEntry entryPendingRemoval = null;
            var column = 0;

            EditorGUILayout.BeginHorizontal();
            foreach (var entry in group.Entries)
            {
                if (column >= perRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    column = 0;
                }

                DrawEntryCard(poolData, entry, () => entryPendingRemoval = entry);
                column++;
            }

            if (column >= perRow)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            if (GUILayout.Button("+", GUILayout.Width(CardOuterWidth), GUILayout.Height(CardOuterHeight)))
            {
                Undo.RecordObject(poolData, "Add Pool Entry");
                group.Entries.Add(new PoolEntry());
                EditorUtility.SetDirty(poolData);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
            var scrollRect = GUILayoutUtility.GetLastRect();

            if (contentHeight > visibleHeight)
            {
                DrawBottomFade(scrollRect);
            }

            if (entryPendingRemoval != null)
            {
                Undo.RecordObject(poolData, "Remove Pool Entry");
                group.Entries.Remove(entryPendingRemoval);
                EditorUtility.SetDirty(poolData);
            }

            _scrollPositions[group] = scroll;
        }

        private void DrawEntryCard(PoolData poolData, PoolEntry entry, System.Action requestRemoveEntry)
        {
            // Deliberately avoids GUILayout.BeginArea: nesting an auto-layout area inside the
            // enclosing BeginHorizontal() flow desyncs that flow's measurement of later siblings
            // once more than one card is present. Instead, reserve exactly one rect for the whole
            // card in the outer flow and position every inner element with plain Rect math.
            var cardRect = GUILayoutUtility.GetRect(
                CardOuterWidth, CardOuterHeight, GUILayout.Width(CardOuterWidth), GUILayout.Height(CardOuterHeight));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(cardRect, CardBackgroundColor);
            }

            var innerX = cardRect.x + CardInnerPadding;
            var innerY = cardRect.y + CardInnerPadding;

            // The delete badge's rect overlaps the top-right corner of the preview below it. Consume
            // its click here, BEFORE DrawPrefabPreview runs, so a click in that corner is claimed by
            // delete rather than falling through to the preview's "open object picker" handler - an
            // invisible GUI.Button still fully participates in event handling even with no visual style,
            // it just doesn't draw anything. Its actual appearance is drawn last, on top of everything.
            var deleteRect = new Rect(cardRect.xMax - DeleteButtonSize - 2f, cardRect.y + 2f, DeleteButtonSize, DeleteButtonSize);
            var deleteClicked = GUI.Button(deleteRect, GUIContent.none, GUIStyle.none);

            var previewRect = new Rect(innerX, innerY, CardSize, CardSize);
            DrawPrefabPreview(previewRect, poolData, entry);

            var nameRect = new Rect(innerX, previewRect.yMax + FieldSpacing, CardSize, FieldHeight);
            var nameControlName = GetControlName(entry, "Name");
            GUI.SetNextControlName(nameControlName);
            EditorGUI.BeginChangeCheck();
            var newName = EditorGUI.TextField(nameRect, entry.TypeName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(poolData, "Rename Pool Entry");
                entry.TypeName = newName;
                EditorUtility.SetDirty(poolData);
            }
            DrawFocusHighlightIfActive(nameRect, nameControlName);

            var countRect = new Rect(innerX, nameRect.yMax + FieldSpacing, CardSize, FieldHeight);
            DrawIntStepper(countRect, poolData, entry);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(deleteRect, "✕", DeleteButtonStyle);
            }

            if (deleteClicked)
            {
                requestRemoveEntry();
            }
        }

        private void DrawPrefabPreview(Rect rect, PoolData poolData, PoolEntry entry)
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, PreviewBackgroundColor);

                if (entry.Prefab != null)
                {
                    var preview = AssetPreview.GetAssetPreview(entry.Prefab);
                    if (preview == null)
                    {
                        if (AssetPreview.IsLoadingAssetPreview(entry.Prefab.GetInstanceID()))
                        {
                            Repaint();
                        }
                        preview = AssetPreview.GetMiniThumbnail(entry.Prefab);
                    }

                    if (preview != null)
                    {
                        GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                    }
                }
                else
                {
                    EditorGUI.LabelField(rect, "Drop\nPrefab", HintLabelStyle);
                }
            }

            HandlePreviewDragAndDrop(rect, poolData, entry);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.ShowObjectPicker<GameObject>(entry.Prefab, false, string.Empty, controlId);
                Event.current.Use();
            }

            if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == controlId)
            {
                var picked = EditorGUIUtility.GetObjectPickerObject() as GameObject;
                if (picked != entry.Prefab)
                {
                    Undo.RecordObject(poolData, "Change Pool Entry Prefab");
                    entry.Prefab = picked;
                    EditorUtility.SetDirty(poolData);
                    GUI.changed = true;
                }
            }
        }

        private static void HandlePreviewDragAndDrop(Rect rect, PoolData poolData, PoolEntry entry)
        {
            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                var dragged = DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] as GameObject : null;
                DragAndDrop.visualMode = dragged != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                var dragged = DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] as GameObject : null;
                if (dragged != null)
                {
                    Undo.RecordObject(poolData, "Change Pool Entry Prefab");
                    entry.Prefab = dragged;
                    EditorUtility.SetDirty(poolData);
                    GUI.changed = true;
                }
                DragAndDrop.AcceptDrag();
                evt.Use();
            }
        }

        private void DrawIntStepper(Rect rect, PoolData poolData, PoolEntry entry)
        {
            // Exact half-height split so both buttons are pixel-identical; a texture-based style
            // (e.g. EditorStyles.miniButton) 9-slices its background for normal button sizes and
            // visibly distorts at a height this small, which is what made the two arrows look
            // mismatched. Flat colors + a hand-drawn triangle avoid that entirely.
            var fieldRect = new Rect(rect.x, rect.y, rect.width - StepperButtonWidth, rect.height);
            var upRect = new Rect(rect.xMax - StepperButtonWidth, rect.y, StepperButtonWidth, rect.height / 2f);
            var downRect = new Rect(rect.xMax - StepperButtonWidth, rect.yMax - rect.height / 2f, StepperButtonWidth, rect.height / 2f);

            var countControlName = GetControlName(entry, "Count");
            GUI.SetNextControlName(countControlName);
            EditorGUI.BeginChangeCheck();
            var typed = EditorGUI.IntField(fieldRect, entry.InitializeCount);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyInitializeCount(poolData, entry, typed);
            }
            DrawFocusHighlightIfActive(fieldRect, countControlName);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(upRect, StepperBackgroundColor);
                EditorGUI.DrawRect(downRect, StepperBackgroundColor);
            }

            if (GUI.Button(upRect, GUIContent.none, GUIStyle.none))
            {
                ApplyInitializeCount(poolData, entry, entry.InitializeCount + 1);
            }
            DrawStepperTriangle(upRect, true);

            if (GUI.Button(downRect, GUIContent.none, GUIStyle.none))
            {
                ApplyInitializeCount(poolData, entry, entry.InitializeCount - 1);
            }
            DrawStepperTriangle(downRect, false);
        }

        private static string GetControlName(PoolEntry entry, string field) =>
            $"PoolEntry_{entry.GetHashCode()}_{field}";

        private static void DrawFocusHighlightIfActive(Rect rect, string controlName)
        {
            if (Event.current.type != EventType.Repaint || GUI.GetNameOfFocusedControl() != controlName)
            {
                return;
            }

            const float thickness = 2f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), FocusHighlightColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), FocusHighlightColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), FocusHighlightColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), FocusHighlightColor);
        }

        private static void ApplyInitializeCount(PoolData poolData, PoolEntry entry, int value)
        {
            Undo.RecordObject(poolData, "Change Pool Entry Initialize Count");
            entry.InitializeCount = Mathf.Max(0, value);
            EditorUtility.SetDirty(poolData);
            GUI.changed = true;
        }

        private static void DrawStepperTriangle(Rect rect, bool pointingUp)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float inset = 3.5f;
            var top = new Vector3(rect.center.x, pointingUp ? rect.y + inset : rect.yMax - inset, 0f);
            var left = new Vector3(rect.x + inset, pointingUp ? rect.yMax - inset : rect.y + inset, 0f);
            var right = new Vector3(rect.xMax - inset, pointingUp ? rect.yMax - inset : rect.y + inset, 0f);

            var previousColor = Handles.color;
            Handles.BeginGUI();
            Handles.color = EditorStyles.label.normal.textColor;
            Handles.DrawAAConvexPolygon(top, right, left);
            Handles.EndGUI();
            Handles.color = previousColor;
        }

        private static void DrawBottomFade(Rect scrollRect)
        {
            const int steps = 8;
            var baseColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.78f, 0.78f, 0.78f);
            var stepHeight = FadeHeight / steps;

            for (var i = 0; i < steps; i++)
            {
                var t = (i + 1) / (float)steps;
                var stripRect = new Rect(scrollRect.x, scrollRect.yMax - FadeHeight + i * stepHeight, scrollRect.width, stepHeight);
                var color = baseColor;
                color.a = t * 0.85f;
                EditorGUI.DrawRect(stripRect, color);
            }
        }
    }
}
