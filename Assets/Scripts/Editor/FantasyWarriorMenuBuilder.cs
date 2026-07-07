using TMPro;
using UI.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FantasyWarriorMenuBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/Menu/Menu UI.prefab";
    private const string UIConfigPath = "Assets/Configs/UI/UI Config.asset";

    [MenuItem("Tools/RPG/UI/Rebuild Fantasy Warrior Menu UI")]
    public static void Rebuild()
    {
        AssetDatabase.Refresh();

        TMP_FontAsset headerFont = Load<TMP_FontAsset>("Assets/Content/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF.asset");
        TMP_FontAsset bodyFont = Load<TMP_FontAsset>("Assets/Content/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
        GameObject basicButtonPrefab = Load<GameObject>("Assets/Content/InterfaceFantasyWarriorHUD/Samples/Prefabs/AssetDemo_FantasyWarrior_Button_Basic01.prefab");

        if (headerFont == null || bodyFont == null || basicButtonPrefab == null)
        {
            Debug.LogError("FantasyWarrior menu rebuild failed: required font or button prefab is missing.");
            return;
        }

        Sprite panelSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box20_Variant01.png");
        Sprite darkBoxSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Box_Background_Shadowed.png");
        Sprite ruleSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Banner08_Fill01.png");
        Sprite lineSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Horizontal01.png");
        Sprite swordsSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Greeble_Swords01.png");
        Sprite dragonSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Greeble_Dragon01.png");
        Sprite arrowSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow02.png");
        Sprite shadowGradientSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Gradient_Horizontal_Smooth01.png");
        Sprite topFrameSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Top04.png");
        Sprite traceryBoxSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Tracery_Box02.png");
        Sprite frameDetailSprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Tracery_FrameDetail03.png");
        Sprite verticalTracerySprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Tracery_Vertical07.png");
        Sprite horizontalTracerySprite = Load<Sprite>("Assets/Content/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Tracery_Horizontal02.png");

        GameObject root = new GameObject("Menu UI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MenuUI));
        root.layer = 5;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = Color.clear;
        rootImage.raycastTarget = true;

        RectTransform shadowGradient = AddImage("Left Menu Shadow Gradient", rootRect, shadowGradientSprite, new Color(0.005f, 0.006f, 0.008f, 0.76f));
        Stretch(shadowGradient);
        Image shadowGradientImage = shadowGradient.GetComponent<Image>();
        shadowGradientImage.type = Image.Type.Simple;

        RectTransform panel = AddImage("Main Menu Frame", rootRect, panelSprite, new Color(0.88f, 0.71f, 0.39f, 0.95f));
        SetLeftCenter(panel, 380f, 4f, 660f, 760f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.type = Image.Type.Sliced;
        panelImage.raycastTarget = false;

        RectTransform panelFill = AddImage("Main Menu Background", panel, darkBoxSprite, new Color(0.035f, 0.028f, 0.021f, 0.98f));
        Stretch(panelFill);
        panelFill.offsetMin = new Vector2(34f, 34f);
        panelFill.offsetMax = new Vector2(-34f, -34f);

        AddPanelDecorations(panel, traceryBoxSprite, topFrameSprite, frameDetailSprite, verticalTracerySprite, horizontalTracerySprite);
        AddTitleOrnament(panel, dragonSprite);
        AddTitle(panel, headerFont);
        AddDivider("Top Divider", panel, lineSprite != null ? lineSprite : ruleSprite, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(450f, 30f), 0.9f);

        Button continueButton = CreateButton("Button Continue", "Continue", 80f, panel, basicButtonPrefab, bodyFont, arrowSprite);
        MakeInactiveWithoutVisualChange(continueButton);

        Button gameButton = CreateButton("Button New Game", "New Game", -20f, panel, basicButtonPrefab, bodyFont, arrowSprite);
        Button developButton = CreateButton("Button Develop", "Develop", -120f, panel, basicButtonPrefab, bodyFont, arrowSprite);

        AddDivider("Bottom Divider", panel, lineSprite != null ? lineSprite : ruleSprite, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(450f, 30f), 0.82f);
        AddBottomCrest(panel, swordsSprite);

        SerializedObject serializedMenu = new SerializedObject(root.GetComponent<MenuUI>());
        serializedMenu.FindProperty("<ToGameButton>k__BackingField").objectReferenceValue = gameButton;
        serializedMenu.FindProperty("<ToDevelopButton>k__BackingField").objectReferenceValue = developButton;
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);

        UpdateUIConfigReference();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("FantasyWarrior menu UI rebuilt: " + PrefabPath);
    }

    private static T Load<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static RectTransform AddImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null)
            image.type = Image.Type.Sliced;

        return rect;
    }

    private static TextMeshProUGUI AddText(string name, RectTransform parent, string text, float size, TMP_FontAsset font, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontSizeMin = size * 0.62f;
        label.enableAutoSizing = true;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetLeftCenter(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void AddTitleOrnament(RectTransform panel, Sprite dragonSprite)
    {
        RectTransform ornament = AddImage("Fantasy Warrior Title Ornament", panel, dragonSprite, new Color(0.95f, 0.76f, 0.42f, 0.36f));
        ornament.anchorMin = new Vector2(0.5f, 1f);
        ornament.anchorMax = new Vector2(0.5f, 1f);
        ornament.pivot = new Vector2(0.5f, 0.5f);
        ornament.anchoredPosition = new Vector2(0f, -91f);
        ornament.sizeDelta = new Vector2(250f, 120f);
    }

    private static void AddPanelDecorations(
        RectTransform panel,
        Sprite traceryBoxSprite,
        Sprite topFrameSprite,
        Sprite frameDetailSprite,
        Sprite verticalTracerySprite,
        Sprite horizontalTracerySprite)
    {
        Color brightGold = new Color(1f, 0.82f, 0.45f, 0.72f);
        Color softGold = new Color(0.95f, 0.74f, 0.39f, 0.42f);

        RectTransform innerTracery = AddImage("Inner Tracery Frame", panel, traceryBoxSprite, softGold);
        Stretch(innerTracery);
        innerTracery.offsetMin = new Vector2(62f, 68f);
        innerTracery.offsetMax = new Vector2(-62f, -68f);

        AddAnchoredImage("Top Crown Frame", panel, topFrameSprite, brightGold, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(390f, 92f), 0f, Vector3.one);
        AddAnchoredImage("Top Tracery Rule", panel, horizontalTracerySprite, softGold, new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(385f, 32f), 0f, Vector3.one);
        AddAnchoredImage("Bottom Tracery Rule", panel, horizontalTracerySprite, softGold, new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(385f, 32f), 180f, Vector3.one);

        AddAnchoredImage("Left Side Tracery", panel, verticalTracerySprite, softGold, new Vector2(0f, 0.5f), new Vector2(67f, -22f), new Vector2(36f, 430f), 0f, Vector3.one);
        AddAnchoredImage("Right Side Tracery", panel, verticalTracerySprite, softGold, new Vector2(1f, 0.5f), new Vector2(-67f, -22f), new Vector2(36f, 430f), 0f, new Vector3(-1f, 1f, 1f));

        AddAnchoredImage("Top Left Frame Detail", panel, frameDetailSprite, brightGold, new Vector2(0f, 1f), new Vector2(92f, -94f), new Vector2(86f, 86f), 0f, Vector3.one);
        AddAnchoredImage("Top Right Frame Detail", panel, frameDetailSprite, brightGold, new Vector2(1f, 1f), new Vector2(-92f, -94f), new Vector2(86f, 86f), 0f, new Vector3(-1f, 1f, 1f));
        AddAnchoredImage("Bottom Left Frame Detail", panel, frameDetailSprite, brightGold, new Vector2(0f, 0f), new Vector2(92f, 94f), new Vector2(86f, 86f), 180f, Vector3.one);
        AddAnchoredImage("Bottom Right Frame Detail", panel, frameDetailSprite, brightGold, new Vector2(1f, 0f), new Vector2(-92f, 94f), new Vector2(86f, 86f), 180f, new Vector3(-1f, 1f, 1f));
    }

    private static RectTransform AddAnchoredImage(string name, RectTransform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size, float rotationZ, Vector3 scale)
    {
        RectTransform rect = AddImage(name, parent, sprite, color);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        rect.localScale = scale;
        return rect;
    }

    private static void AddTitle(RectTransform panel, TMP_FontAsset headerFont)
    {
        TextMeshProUGUI title = AddText("Game Title", panel, "RPG", 82f, headerFont, TextAlignmentOptions.Center, new Color(1f, 0.89f, 0.58f, 1f));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, -156f);
        titleRect.sizeDelta = new Vector2(-112f, 86f);
        title.outlineWidth = 0.12f;
        title.outlineColor = new Color(0.05f, 0.025f, 0.01f, 0.92f);

    }

    private static void AddDivider(string name, RectTransform parent, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size, float alpha)
    {
        RectTransform divider = AddImage(name, parent, sprite, new Color(0.96f, 0.72f, 0.35f, alpha));
        divider.anchorMin = anchor;
        divider.anchorMax = anchor;
        divider.pivot = new Vector2(0.5f, 0.5f);
        divider.anchoredPosition = position;
        divider.sizeDelta = size;
    }

    private static Button CreateButton(string name, string text, float y, RectTransform panel, GameObject prefab, TMP_FontAsset font, Sprite arrowSprite)
    {
        GameObject buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        buttonObject.name = name;
        SetLayerRecursive(buttonObject, 5);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(420f, 84f);

        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.95f, 0.76f, 0.43f, 0.96f);
            image.raycastTarget = true;
        }

        foreach (TextMeshProUGUI label in buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.font = font;
            label.text = text;
            label.fontSize = 36f;
            label.fontSizeMin = 24f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.92f, 0.68f, 1f);
            label.outlineWidth = 0.08f;
            label.outlineColor = new Color(0.04f, 0.02f, 0.01f, 0.86f);
        }

        Transform content = buttonObject.transform.Find("Content");
        if (content != null)
        {
            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding.left = 28;
                layout.padding.right = 28;
                layout.spacing = 14f;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }

            Transform next = content.Find("ICON_Next");
            if (next != null)
            {
                Image icon = next.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = arrowSprite != null ? arrowSprite : icon.sprite;
                    icon.color = new Color(1f, 0.86f, 0.55f, 0.95f);
                }
                next.gameObject.SetActive(true);
            }

            Transform previous = content.Find("ICON_Previous");
            if (previous != null)
                previous.gameObject.SetActive(false);
        }

        return buttonObject.GetComponent<Button>();
    }

    private static void MakeInactiveWithoutVisualChange(Button button)
    {
        button.transition = Selectable.Transition.None;

        ColorBlock colors = button.colors;
        colors.disabledColor = colors.normalColor;
        button.colors = colors;

        button.interactable = false;
    }

    private static void AddBottomCrest(RectTransform panel, Sprite swordsSprite)
    {
        RectTransform swords = AddImage("Menu Crest", panel, swordsSprite, new Color(0.94f, 0.75f, 0.42f, 0.58f));
        swords.anchorMin = new Vector2(0.5f, 0f);
        swords.anchorMax = new Vector2(0.5f, 0f);
        swords.pivot = new Vector2(0.5f, 0.5f);
        swords.anchoredPosition = new Vector2(0f, 98f);
        swords.sizeDelta = new Vector2(128f, 128f);
    }

    private static void UpdateUIConfigReference()
    {
        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        MenuUI menuUI = savedPrefab != null ? savedPrefab.GetComponent<MenuUI>() : null;
        ScriptableObject uiConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UIConfigPath);

        if (menuUI == null || uiConfig == null)
        {
            Debug.LogError("FantasyWarrior menu rebuild failed to update UIConfig reference.");
            return;
        }

        SerializedObject serializedConfig = new SerializedObject(uiConfig);
        serializedConfig.FindProperty("<MenuUI>k__BackingField").objectReferenceValue = menuUI;
        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(uiConfig);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
    }
}
