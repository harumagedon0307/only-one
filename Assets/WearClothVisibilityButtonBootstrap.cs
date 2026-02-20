using System.Collections;
using Mediapipe.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WearClothVisibilityButtonBootstrap : MonoBehaviour
{
    private const string TargetSceneName = "wear_to_3d";
    private const string ButtonObjectName = "ClothVisibilityButton";
    private const string LabelObjectName = "Label";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBootstrapper()
    {
        if (FindObjectOfType<WearClothVisibilityButtonBootstrap>() != null)
        {
            return;
        }

        var bootstrapper = new GameObject("WearClothVisibilityButtonBootstrap");
        DontDestroyOnLoad(bootstrapper);
        bootstrapper.AddComponent<WearClothVisibilityButtonBootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        TrySetup(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySetup(scene);
    }

    private void TrySetup(Scene scene)
    {
        if (scene.name != TargetSceneName)
        {
            return;
        }

        var ui = EnsureToggleButton();
        if (ui != null)
        {
            StartCoroutine(BindPointListAnnotation(ui));
        }
    }

    private WearClothVisibilityUI EnsureToggleButton()
    {
        var existing = GameObject.Find(ButtonObjectName);
        if (existing != null)
        {
            return existing.GetComponent<WearClothVisibilityUI>();
        }

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            uiLayer = 0;
        }

        var buttonObject = new GameObject(
            ButtonObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(WearClothVisibilityUI));
        buttonObject.layer = uiLayer;
        buttonObject.transform.SetParent(canvas.transform, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(220f, 64f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.9f);

        var button = buttonObject.GetComponent<Button>();
        var textObject = new GameObject(LabelObjectName, typeof(RectTransform), typeof(Text));
        textObject.layer = uiLayer;
        textObject.transform.SetParent(buttonObject.transform, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textObject.GetComponent<Text>();
        label.text = "Hide Cloth";
        label.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 24;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.raycastTarget = false;

        var ui = buttonObject.GetComponent<WearClothVisibilityUI>();
        ui.Initialize(null, button, null, label);
        return ui;
    }

    private IEnumerator BindPointListAnnotation(WearClothVisibilityUI ui)
    {
        if (ui == null)
        {
            yield break;
        }

        while (SceneManager.GetActiveScene().name == TargetSceneName)
        {
#if UNITY_2020_1_OR_NEWER
            var pointList = FindObjectOfType<PointListAnnotation>(true);
#else
            var pointList = FindObjectOfType<PointListAnnotation>();
#endif
            if (pointList != null)
            {
                ui.SetPointListAnnotation(pointList, true);
                yield break;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject(
            "ClothVisibilityCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }
}
