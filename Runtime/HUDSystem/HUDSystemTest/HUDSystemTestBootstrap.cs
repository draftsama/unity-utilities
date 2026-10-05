using Modules.Utilities;
using UnityEngine;
using UnityEngine.UI;

// Manual test rig for Modules.Utilities.HUDSystem. Builds everything at runtime so the scene only
// holds this component. Covers:
//  - camera spawns late (null camera must not throw),
//  - targets behind the camera still get an off-screen arrow,
//  - an indicator toggling between throttled sort frames must not throw, and reuses its pooled view,
//  - an indicator spawned after startup registers itself and reuses a destroyed indicator's view,
//  - world offset, off-screen ignore-distance-fade, AutoSize under a CanvasScaler,
//  - Screen Space - Camera canvases.
public class HUDSystemTestBootstrap : MonoBehaviour
{
    [SerializeField] RenderMode canvasMode = RenderMode.ScreenSpaceOverlay;
    [SerializeField] float cameraSpawnDelay = 0.5f;
    [SerializeField] float toggleInterval = 0.13f;
    [SerializeField] int sortUpdateFrequency = 3;
    [SerializeField] float destroyTime = 1.5f;
    [SerializeField] float lateSpawnTime = 2f;

    HUDRenderer hudRenderer;
    Canvas canvas;
    GameObject toggler;
    GameObject doomed;
    GameObject sharedOnScreen, sharedOffScreen, sharedArrow;
    float toggleTimer;
    bool cameraSpawned;
    bool destroyed;
    bool lateSpawned;
    bool reported;

    void Awake()
    {
        hudRenderer = BuildCanvas();

        sharedOnScreen = CreateTemplate("OnScreen Shared", new Vector2(220f, 28f), new Color(0f, 0.3f, 0.6f, 0.85f), "shared");
        sharedOffScreen = CreateTemplate("OffScreen Shared", new Vector2(220f, 28f), new Color(0.6f, 0.2f, 0f, 0.85f), "OFF shared");
        sharedArrow = CreateArrowTemplate();

        // Camera at the origin looking down +Z.
        CreateTarget("Front +offset", new Vector3(0f, 0f, 8f), Color.green).GetComponent<HUDIndicator>().m_WorldOffset = new Vector3(0f, 1.2f, 0f);
        CreateTarget("FrontLeft", new Vector3(-3f, 1.5f, 10f), Color.green);
        CreateTarget("Right (front, off-screen)", new Vector3(30f, 0f, 5f), Color.cyan);
        CreateTarget("BehindLeft", new Vector3(-6f, 0f, -8f), Color.magenta);
        CreateTarget("BehindBelow", new Vector3(0f, -5f, -8f), Color.magenta);
        toggler = CreateTarget("Toggler", new Vector3(3f, -1f, 12f), Color.yellow);

        // Past the fade range: only the one ignoring distance fade should show
        CreateTarget("FarIgnoreFade", new Vector3(0f, 8f, -80f), Color.white).GetComponent<HUDIndicator>().m_IndicatorData.m_OffScreenIgnoreDistanceFade = true;
        CreateTarget("FarFaded", new Vector3(-80f, 0f, 0f), Color.gray);

        var auto = CreateTarget("AutoSize", new Vector3(-2f, -1.5f, 6f), Color.blue);
        auto.GetComponent<HUDIndicator>().m_IndicatorData.m_AutoSize = true; // Start picks up the cube's BoxCollider

        doomed = CreateSharedTarget("Doomed", new Vector3(1.5f, 2f, 9f));
    }

    void Update()
    {
        if (!cameraSpawned && Time.timeSinceLevelLoad >= cameraSpawnDelay)
        {
            var camGo = new GameObject("Main Camera (late)");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            if (canvasMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
            cameraSpawned = true;
            Debug.Log("[HUDTest] camera spawned at t=" + Time.timeSinceLevelLoad.ToString("F2"));
        }

        toggleTimer += Time.deltaTime;
        if (toggleTimer >= toggleInterval)
        {
            toggleTimer = 0f;
            toggler.SetActive(!toggler.activeSelf);
        }

        if (!destroyed && Time.timeSinceLevelLoad >= destroyTime)
        {
            destroyed = true;
            Destroy(doomed);
        }

        if (!lateSpawned && Time.timeSinceLevelLoad >= lateSpawnTime)
        {
            lateSpawned = true;
            CreateSharedTarget("LateSpawn", new Vector3(1.5f, 2f, 9f));
        }

        if (!reported && Time.timeSinceLevelLoad >= 3f)
        {
            reported = true;
            Report();
        }
    }

    HUDRenderer BuildCanvas()
    {
        var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = canvasMode;

        // Half reference resolution gives scaleFactor 2 at 1080p, which exposes unscaled AutoSize
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 1f;

        var rendererGo = new GameObject("HUDRenderer", typeof(RectTransform));
        var rt = (RectTransform)rendererGo.transform;
        rt.SetParent(canvasGo.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Awake runs inside AddComponent; no camera exists yet, which is the late-camera case.
        var hud = rendererGo.AddComponent<HUDRenderer>();
        hud.m_FadeDistance = new Vector2(40f, 60f);
        hud.m_SortUpdateFrequency = sortUpdateFrequency;
        return hud;
    }

    GameObject CreateTarget(string label, Vector3 position, Color color)
    {
        return CreateTarget(label, position, color,
            CreateTemplate("OnScreen " + label, new Vector2(110f, 16f), new Color(0f, 0.5f, 0f, 0.75f), label),
            CreateTemplate("OffScreen " + label, new Vector2(110f, 16f), new Color(0.6f, 0.2f, 0f, 0.85f), "OFF: " + label),
            CreateArrowTemplate());
    }

    GameObject CreateSharedTarget(string label, Vector3 position)
    {
        return CreateTarget(label, position, new Color(0.3f, 0.6f, 1f), sharedOnScreen, sharedOffScreen, sharedArrow);
    }

    GameObject CreateTarget(string label, Vector3 position, Color color, GameObject onScreen, GameObject offScreen, GameObject arrow)
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = label;
        target.transform.position = position;
        target.GetComponent<Renderer>().material.color = color;

        // HUDIndicator registers in Start, so data set right after AddComponent is picked up.
        var indicator = target.AddComponent<HUDIndicator>();
        indicator.m_IndicatorData = new HUDIndicator.HUDIndicatorData
        {
            m_UseOnScreen = true,
            m_OnScreenPrefab = onScreen,
            m_UseOffScreen = true,
            m_OffScreenPrefab = offScreen,
            m_OffScreenArrowPrefab = arrow,
        };
        return target;
    }

    GameObject CreateTemplate(string name, Vector2 size, Color background, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        ((RectTransform)go.transform).sizeDelta = size;
        go.AddComponent<Image>().color = background;

        var textGo = new GameObject("Label", typeof(RectTransform));
        var textRt = (RectTransform)textGo.transform;
        textRt.SetParent(go.transform, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var t = textGo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 9;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;

        go.AddComponent<HUDTestLabelBinder>().prefix = text.StartsWith("OFF") ? "OFF: " : "";
        return go;
    }

    GameObject CreateArrowTemplate()
    {
        // Shaft along +X with a red tip at the +X end, so rotation shows which way it points.
        var go = new GameObject("Arrow", typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(20f, 5f);
        go.AddComponent<Image>().color = Color.yellow;

        var tip = new GameObject("Tip", typeof(RectTransform));
        var tipRt = (RectTransform)tip.transform;
        tipRt.SetParent(go.transform, false);
        tipRt.sizeDelta = new Vector2(8f, 8f);
        tipRt.anchoredPosition = new Vector2(8f, 0f);
        tip.AddComponent<Image>().color = Color.red;
        return go;
    }

    void Report()
    {
        var allViews = hudRenderer.GetComponentsInChildren<HUDIndicatorView>(true);
        Debug.Log("[HUDTest] mode=" + canvasMode + " scaleFactor=" + canvas.scaleFactor.ToString("F2") +
                  " canvasRect=" + ((RectTransform)hudRenderer.transform).rect.size +
                  " boundViews=" + hudRenderer.m_IndicatorViewList.Count + " totalViewObjects=" + allViews.Length +
                  " togglerBinds=" + HUDTestLabelBinder.BindCount("Toggler"));

        foreach (var view in allViews)
        {
            if (view.Indicator == null)
            {
                Debug.Log("[HUDTest] pooled view: " + view.name + " active=" + view.gameObject.activeSelf);
                continue;
            }
            string state = "hidden";
            Vector2 pos = Vector2.zero;
            Vector2 size = Vector2.zero;
            string text = "";
            if (view.OnScreenRectTransform != null && view.OnScreenRectTransform.gameObject.activeSelf)
            {
                state = "on-screen";
                pos = view.OnScreenRectTransform.anchoredPosition;
                size = view.OnScreenRectTransform.rect.size;
                text = view.OnScreenRectTransform.GetComponentInChildren<Text>().text;
            }
            else if (view.OffScreenRectTransform != null && view.OffScreenRectTransform.gameObject.activeSelf)
            {
                state = "off-screen";
                pos = view.OffScreenRectTransform.anchoredPosition;
                text = view.OffScreenRectTransform.GetComponentInChildren<Text>().text;
            }
            Debug.Log("[HUDTest] " + view.Indicator.name + ": " + state + " pos=" + pos + " size=" + size +
                      " alpha=" + view.CanvasGroup.alpha.ToString("F2") + " text='" + text + "'");
        }
    }
}
