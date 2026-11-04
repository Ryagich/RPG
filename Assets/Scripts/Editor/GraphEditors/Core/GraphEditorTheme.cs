using System;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Shared, domain-neutral visual language for graph editor windows.
    /// Owns only IMGUI styles, palette and created presentation assets.
    /// </summary>
    internal sealed class GraphEditorTheme : IDisposable
    {
        private readonly Func<bool> useLightTheme;
        private readonly GraphEditorStyleTextOverrides styleTextOverrides = new();

        private GUIStyle lightWindowStyle;
        private GUIStyle lightHelpBoxStyle;
        private GUIStyle lightButtonStyle;
        private GUIStyle lightMiniButtonStyle;
        private GUIStyle lightPopupStyle;
        private GUIStyle lightTextFieldStyle;
        private GUIStyle lightLabelStyle;
        private GUIStyle lightFoldoutStyle;
        private GUIStyle lightBoldLabelStyle;
        private GUIStyle lightMiniBoldLabelStyle;
        private GUIStyle lightMiniLabelStyle;
        private GUIStyle lightWordWrappedMiniLabelStyle;
        private GUIStyle lightCenteredMiniLabelStyle;
        private GUIStyle lightPreviewLabelStyle;
        private Texture2D lightWindowTexture;
        private Texture2D lightHelpBoxTexture;
        private Texture2D lightButtonTexture;
        private Texture2D lightButtonHoverTexture;
        private Texture2D lightButtonActiveTexture;
        private Texture2D lightTextFieldTexture;
        private GUISkin lightSkin;

        public GraphEditorTheme(Func<bool> useLightTheme)
        {
            this.useLightTheme = useLightTheme ?? throw new ArgumentNullException(nameof(useLightTheme));
        }

        private bool IsLightTheme => useLightTheme();

        public GUIStyle NodeWindowStyle => IsLightTheme ? lightWindowStyle ??= CreateNodeWindowStyle() : GUI.skin.window;
        public GUIStyle HelpBoxStyle => IsLightTheme ? lightHelpBoxStyle ??= CreateHelpBoxStyle() : EditorStyles.helpBox;
        public GUIStyle ButtonStyle => IsLightTheme ? lightButtonStyle ??= CreateButtonStyle(GUI.skin.button) : GUI.skin.button;
        public GUIStyle MiniButtonStyle => IsLightTheme ? lightMiniButtonStyle ??= CreateButtonStyle(EditorStyles.miniButton) : EditorStyles.miniButton;
        public GUIStyle PopupStyle => IsLightTheme ? lightPopupStyle ??= CreatePopupStyle() : EditorStyles.popup;
        public GUIStyle TextFieldStyle => IsLightTheme ? lightTextFieldStyle ??= CreateTextInputStyle(EditorStyles.textField) : EditorStyles.textField;
        public GUIStyle LabelStyle => IsLightTheme ? lightLabelStyle ??= CreateLabelStyle(EditorStyles.label) : EditorStyles.label;
        public GUIStyle FoldoutStyle => IsLightTheme ? lightFoldoutStyle ??= CreateLabelStyle(EditorStyles.foldout) : EditorStyles.foldout;
        public GUIStyle BoldLabelStyle => IsLightTheme ? lightBoldLabelStyle ??= CreateLabelStyle(EditorStyles.boldLabel) : EditorStyles.boldLabel;
        public GUIStyle MiniBoldLabelStyle => IsLightTheme ? lightMiniBoldLabelStyle ??= CreateLabelStyle(EditorStyles.miniBoldLabel) : EditorStyles.miniBoldLabel;
        public GUIStyle MiniLabelStyle => IsLightTheme ? lightMiniLabelStyle ??= CreateLabelStyle(EditorStyles.miniLabel, MutedContentColor) : EditorStyles.miniLabel;
        public GUIStyle WordWrappedMiniLabelStyle => IsLightTheme ? lightWordWrappedMiniLabelStyle ??= CreateLabelStyle(EditorStyles.wordWrappedMiniLabel, MutedContentColor) : EditorStyles.wordWrappedMiniLabel;
        public GUIStyle CenteredMiniLabelStyle => IsLightTheme ? lightCenteredMiniLabelStyle ??= CreateLabelStyle(EditorStyles.centeredGreyMiniLabel, MutedContentColor) : EditorStyles.centeredGreyMiniLabel;
        public GUIStyle PreviewLabelStyle => IsLightTheme ? lightPreviewLabelStyle ??= CreatePreviewTextStyle() : EditorStyles.wordWrappedLabel;

        public Color PanelBackgroundColor => IsLightTheme ? new Color(0.96f, 0.95f, 0.92f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color WindowBackgroundColor => IsLightTheme ? new Color(0.97f, 0.96f, 0.94f, 1f) : new Color(0.22f, 0.22f, 0.22f, 1f);
        public Color CanvasBackgroundColor => IsLightTheme ? new Color(0.98f, 0.97f, 0.95f, 1f) : new Color(0.13f, 0.13f, 0.13f, 1f);
        public Color MinorGridColor => IsLightTheme ? new Color(0.35f, 0.40f, 0.48f, 0.18f) : new Color(0.25f, 0.25f, 0.25f, 0.35f);
        public Color MajorGridColor => IsLightTheme ? new Color(0.32f, 0.37f, 0.46f, 0.30f) : new Color(0.25f, 0.25f, 0.25f, 0.60f);
        public Color PrimaryConnectionColor => IsLightTheme ? new Color(0.30f, 0.28f, 0.24f, 0.98f) : new Color(0.96f, 0.96f, 0.96f, 0.98f);
        public Color SourceHighlightConnectionColor => IsLightTheme ? new Color(0.18f, 0.18f, 0.18f, 0.98f) : new Color(1f, 1f, 1f, 0.98f);
        public Color TargetHighlightConnectionColor => IsLightTheme ? new Color(0.86f, 0.18f, 0.18f, 0.98f) : new Color(1f, 0.28f, 0.28f, 0.98f);
        public Color DangerButtonColor => IsLightTheme ? new Color(0.88f, 0.32f, 0.32f, 1f) : new Color(1f, 0.40f, 0.40f, 1f);
        public Color LinkButtonColor => IsLightTheme ? new Color(0.84f, 0.62f, 0.18f, 1f) : new Color(1f, 0.70f, 0.20f, 1f);
        public Color StartBadgeColor => IsLightTheme ? new Color(0.22f, 0.60f, 0.26f, 1f) : new Color(0.20f, 0.70f, 0.25f, 1f);
        public Color WarningBadgeColor => IsLightTheme ? new Color(0.84f, 0.56f, 0.14f, 1f) : new Color(1f, 0.60f, 0.15f, 1f);
        public Color StartNodeTint => IsLightTheme ? new Color(0.84f, 0.95f, 0.84f, 1f) : new Color(0.82f, 1f, 0.82f, 1f);
        public Color QuestNodeTint => IsLightTheme ? new Color(0.84f, 0.90f, 0.98f, 1f) : new Color(0.80f, 0.90f, 1f, 1f);
        public Color OrphanNodeTint => IsLightTheme ? new Color(0.98f, 0.90f, 0.74f, 1f) : new Color(1f, 0.92f, 0.72f, 1f);
        public Color MissingLinkPanelTint => IsLightTheme ? new Color(1f, 0.86f, 0.86f, 1f) : new Color(1f, 0.75f, 0.75f, 1f);
        public Color OutsideGraphPanelTint => IsLightTheme ? new Color(1f, 0.93f, 0.78f, 1f) : new Color(1f, 0.90f, 0.65f, 1f);
        public Color SelectionOverlayTextColor => IsLightTheme ? new Color(0.10f, 0.16f, 0.12f, 1f) : Color.white;
        public Color DangerAccentColor => IsLightTheme ? new Color(0.82f, 0.30f, 0.30f, 1f) : new Color(0.92f, 0.34f, 0.34f, 1f);
        public Color WarningAccentColor => IsLightTheme ? new Color(0.78f, 0.52f, 0.12f, 1f) : new Color(0.95f, 0.66f, 0.22f, 1f);
        public Color HybridAccentColor => IsLightTheme ? new Color(0.20f, 0.58f, 0.64f, 1f) : new Color(0.24f, 0.72f, 0.78f, 1f);
        public Color ConditionAccentColor => IsLightTheme ? new Color(0.21f, 0.52f, 0.72f, 1f) : new Color(0.26f, 0.63f, 0.86f, 1f);
        public Color RewardAccentColor => IsLightTheme ? new Color(0.31f, 0.65f, 0.35f, 1f) : new Color(0.38f, 0.78f, 0.42f, 1f);
        public Color MoneyAccentColor => IsLightTheme ? new Color(0.74f, 0.58f, 0.18f, 1f) : new Color(0.85f, 0.68f, 0.22f, 1f);
        public Color ItemAccentColor => IsLightTheme ? new Color(0.20f, 0.66f, 0.64f, 1f) : new Color(0.24f, 0.78f, 0.76f, 1f);
        public Color NeutralAccentColor => IsLightTheme ? new Color(0.48f, 0.48f, 0.48f, 1f) : new Color(0.60f, 0.60f, 0.60f, 1f);
        public Color StrongDividerColor => IsLightTheme ? new Color(0f, 0f, 0f, 0.12f) : new Color(1f, 1f, 1f, 0.12f);
        public Color SectionDividerColor => IsLightTheme ? new Color(0f, 0f, 0f, 0.10f) : new Color(1f, 1f, 1f, 0.10f);
        public Color SoftDividerColor => IsLightTheme ? new Color(0f, 0f, 0f, 0.08f) : new Color(1f, 1f, 1f, 0.08f);
        public Color SoftestDividerColor => IsLightTheme ? new Color(0f, 0f, 0f, 0.06f) : new Color(1f, 1f, 1f, 0.06f);
        public Color SubtleDividerColor => IsLightTheme ? new Color(0.54f, 0.52f, 0.47f, 0.42f) : new Color(0.72f, 0.72f, 0.72f, 0.28f);

        public string GetThemeToggleLabel() => IsLightTheme ? "Switch to Night Theme" : "Switch to Light Theme";
        public Color GetSelectionOverlayColor(bool isHovered) => IsLightTheme
            ? new Color(0.18f, 0.65f, 0.24f, isHovered ? 0.32f : 0.18f)
            : new Color(0f, 0.75f, 0.20f, isHovered ? 0.45f : 0.25f);

        public void DrawWindowBackground(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, WindowBackgroundColor);
            }
        }

        public void ApplyGuiState()
        {
            if (!IsLightTheme)
            {
                return;
            }

            GUI.backgroundColor = ControlBackgroundColor;
            GUI.contentColor = ControlContentColor;
            GUI.skin = LightSkin;
            styleTextOverrides.ApplyForLightTheme();
        }

        public void RestoreGuiState() => styleTextOverrides.Restore();

        public void Dispose()
        {
            styleTextOverrides.Restore();
            DestroyCreatedObject(lightSkin);
            DestroyCreatedObject(lightWindowTexture);
            DestroyCreatedObject(lightHelpBoxTexture);
            DestroyCreatedObject(lightButtonTexture);
            DestroyCreatedObject(lightButtonHoverTexture);
            DestroyCreatedObject(lightButtonActiveTexture);
            DestroyCreatedObject(lightTextFieldTexture);
            lightSkin = null;
        }

        private GUISkin LightSkin => lightSkin ??= CreateLightSkin();

        private GUIStyle CreateNodeWindowStyle()
        {
            lightWindowTexture ??= CreateSolidTexture(new Color(0.95f, 0.96f, 0.98f, 1f));
            return CreateBackgroundStyle(GUI.skin.window, lightWindowTexture);
        }

        private GUIStyle CreateHelpBoxStyle()
        {
            lightHelpBoxTexture ??= CreateSolidTexture(new Color(0.96f, 0.95f, 0.92f, 1f));
            return CreateBackgroundStyle(EditorStyles.helpBox, lightHelpBoxTexture);
        }

        private GUIStyle CreateButtonStyle(GUIStyle sourceStyle)
        {
            lightButtonTexture ??= CreateSolidTexture(new Color(0.94f, 0.92f, 0.88f, 1f));
            lightButtonHoverTexture ??= CreateSolidTexture(new Color(0.91f, 0.89f, 0.85f, 1f));
            lightButtonActiveTexture ??= CreateSolidTexture(new Color(0.87f, 0.85f, 0.81f, 1f));
            var style = new GUIStyle(sourceStyle);
            ApplyState(style.normal, lightButtonTexture);
            ApplyState(style.hover, lightButtonHoverTexture);
            ApplyState(style.active, lightButtonActiveTexture);
            ApplyState(style.focused, lightButtonHoverTexture);
            ApplyState(style.onNormal, lightButtonTexture);
            ApplyState(style.onHover, lightButtonHoverTexture);
            ApplyState(style.onActive, lightButtonActiveTexture);
            ApplyState(style.onFocused, lightButtonHoverTexture);
            return style;
        }

        private GUIStyle CreateTextInputStyle(GUIStyle sourceStyle)
        {
            lightTextFieldTexture ??= CreateSolidTexture(new Color(0.98f, 0.97f, 0.95f, 1f));
            return CreateBackgroundStyle(sourceStyle, lightTextFieldTexture);
        }

        private GUIStyle CreatePopupStyle()
        {
            GUIStyle style = CreateButtonStyle(EditorStyles.popup);
            style.alignment = TextAnchor.MiddleLeft;
            return style;
        }

        private GUIStyle CreatePreviewTextStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(6, 6, 4, 4)
            };
            SetTextColor(style, ControlContentColor);
            return style;
        }

        private GUISkin CreateLightSkin()
        {
            GUISkin sourceSkin = GUI.skin;
            GUISkin skin = UnityEngine.Object.Instantiate(sourceSkin);
            skin.label = CreateLabelStyle(sourceSkin.label);
            skin.button = CreateButtonStyle(sourceSkin.button);
            skin.textField = CreateTextInputStyle(sourceSkin.textField);
            skin.textArea = CreateTextInputStyle(sourceSkin.textArea);
            skin.box = CreateHelpBoxStyle();
            skin.window = CreateNodeWindowStyle();
            skin.toggle = CreateLabelStyle(sourceSkin.toggle);
            skin.settings.selectionColor = new Color(0.77f, 0.84f, 0.93f, 1f);
            skin.settings.cursorColor = ControlContentColor;
            skin.customStyles = RegisterLightCustomStyles(sourceSkin, skin.customStyles);
            return skin;
        }

        private GUIStyle[] RegisterLightCustomStyles(GUISkin sourceSkin, GUIStyle[] styles)
        {
            string[] textFields = { "TextField", "TextArea", "IN TextField", "ObjectField", "IN ObjectField", "IN ObjectFieldText", "ObjectFieldThumb", "ObjectFieldMiniThumb", "SearchTextField", "ToolbarSearchTextField", "ToolbarSeachTextField" };
            foreach (string styleName in textFields)
            {
                styles = RegisterNamedStyle(sourceSkin, styles, styleName, lightTextFieldTexture);
            }

            string[] buttons = { "ObjectFieldButton", "Popup", "IN Popup", "MiniPopup", "MiniPullDown", "DropDown", "DropDownButton" };
            foreach (string styleName in buttons)
            {
                styles = RegisterNamedStyle(sourceSkin, styles, styleName, lightButtonTexture);
            }

            return styles;
        }

        private GUIStyle[] RegisterNamedStyle(GUISkin skin, GUIStyle[] styles, string styleName, Texture2D texture)
        {
            GUIStyle source = skin.FindStyle(styleName);
            return source == null ? styles : GraphEditorGuiStyleUtility.AppendOrReplace(styles, CreateNamedStyle(source, styleName, texture));
        }

        private GUIStyle CreateLabelStyle(GUIStyle sourceStyle) => CreateLabelStyle(sourceStyle, ControlContentColor);

        private static GUIStyle CreateLabelStyle(GUIStyle sourceStyle, Color color)
        {
            var style = new GUIStyle(sourceStyle);
            SetTextColor(style, color);
            return style;
        }

        private GUIStyle CreateBackgroundStyle(GUIStyle sourceStyle, Texture2D texture)
        {
            var style = new GUIStyle(sourceStyle);
            ApplyState(style.normal, texture);
            ApplyState(style.hover, texture);
            ApplyState(style.active, texture);
            ApplyState(style.focused, texture);
            ApplyState(style.onNormal, texture);
            ApplyState(style.onHover, texture);
            ApplyState(style.onActive, texture);
            ApplyState(style.onFocused, texture);
            return style;
        }

        private GUIStyle CreateNamedStyle(GUIStyle sourceStyle, string styleName, Texture2D texture)
        {
            GUIStyle style = CreateBackgroundStyle(sourceStyle, texture);
            style.name = styleName;
            return style;
        }

        private void ApplyState(GUIStyleState state, Texture2D texture) => GraphEditorGuiStyleUtility.ApplyState(state, texture, ControlContentColor);

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static Texture2D CreateSolidTexture(Color color) => GraphEditorGuiStyleUtility.CreateSolidTexture(color);

        private static void DestroyCreatedObject(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private Color ControlBackgroundColor => IsLightTheme ? new Color(0.96f, 0.95f, 0.92f, 1f) : Color.white;
        private Color ControlContentColor => IsLightTheme ? Color.black : Color.white;
        private Color MutedContentColor => IsLightTheme ? Color.black : new Color(0.75f, 0.75f, 0.75f, 1f);
    }
}
