using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

public class Activity : MonoBehaviour
{
    [Header("UI 引用")]
    public Button activityButton;
    public GameObject activityPanel;
    public TMP_Text contentText;
    public Button button1;
    public Button button2;
    public Button button3;
    public Button button4;
    public Button closeButton;

    private Image btn1Img, btn2Img, btn3Img, btn4Img;
    private Color normalColor;
    private static readonly Color SelectedColor = new Color(0.2f, 0.55f, 0.85f);

    private LuaEnv luaEnv;
    private readonly List<Button> dynamicButtons = new List<Button>();
    private readonly List<Image> dynamicBtnImgs = new List<Image>();
    private readonly List<ActivityItem> activityItems = new List<ActivityItem>();
    private const int BaseButtonCount = 4;

    private GameObject downloadGO;
    private GameObject successGO;
    private TMP_Text downloadText;

    struct ActivityItem
    {
        public string btn;
        public string content;
    }

    void Start()
    {
        luaEnv = new LuaEnv();

        btn1Img = button1.targetGraphic as Image;
        btn2Img = button2.targetGraphic as Image;
        btn3Img = button3.targetGraphic as Image;
        btn4Img = button4.targetGraphic as Image;
        normalColor = btn1Img != null ? btn1Img.color : Color.white;

        LoadActivitiesFromLua();
        ApplyBaseButtonLabels();

        activityButton.onClick.AddListener(OnClickActivityButton);
        button1.onClick.AddListener(() => SelectActivity(0));
        button2.onClick.AddListener(() => SelectActivity(1));
        button3.onClick.AddListener(() => SelectActivity(2));
        button4.onClick.AddListener(() => SelectActivity(3));
        closeButton.onClick.AddListener(() => activityPanel.SetActive(false));

        CreateDownloadUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            CheckForUpdate();
    }

    void OnDestroy()
    {
        luaEnv?.Dispose();
    }

    // ==================== download UI ====================

    void CreateDownloadUI()
    {
        var canvas = activityButton.transform.parent;
        while (canvas != null && canvas.GetComponent<Canvas>() == null)
            canvas = canvas.parent;
        if (canvas == null) return;

        TMP_FontAsset font = null;
#if UNITY_EDITOR
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/GameResouce/Fonts/SIMKAI SDF.asset");
#endif

        // 下载按钮（左下角，白色默认样式）
        downloadGO = new GameObject("DownloadBtn", typeof(RectTransform));
        downloadGO.transform.SetParent(canvas, false);
        var rt = downloadGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(40, 120);
        rt.sizeDelta = new Vector2(300, 84);
        var img = downloadGO.AddComponent<Image>();
        img.color = Color.white;
        var btn = downloadGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnClickDownload);

        // 按钮中间的"下载"文字
        var btnLabelGO = new GameObject("BtnLabel", typeof(RectTransform));
        btnLabelGO.transform.SetParent(downloadGO.transform, false);
        var blRT = btnLabelGO.GetComponent<RectTransform>();
        blRT.anchorMin = new Vector2(0, 0);
        blRT.anchorMax = new Vector2(1, 1);
        blRT.sizeDelta = new Vector2(-16, -8);
        blRT.anchoredPosition = Vector2.zero;
        var btnLabel = btnLabelGO.AddComponent<TextMeshProUGUI>();
        btnLabel.text = "下载";
        btnLabel.fontSize = 34;
        btnLabel.color = Color.black;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.horizontalAlignment = HorizontalAlignmentOptions.Center;
        btnLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (font != null) btnLabel.font = font;

        // 更新成功提示文本（屏幕中下方，默认隐藏）
        successGO = new GameObject("SuccessHint", typeof(RectTransform));
        successGO.transform.SetParent(canvas, false);
        var hRT = successGO.GetComponent<RectTransform>();
        hRT.anchorMin = hRT.anchorMax = new Vector2(0.5f, 0);
        hRT.pivot = new Vector2(0.5f, 0);
        hRT.anchoredPosition = new Vector2(0, 60);
        hRT.sizeDelta = new Vector2(600, 150);
        downloadText = successGO.AddComponent<TextMeshProUGUI>();
        downloadText.text = "更新成功!";
        downloadText.fontSize = 56;
        downloadText.color = Color.white;
        downloadText.alignment = TextAlignmentOptions.Center;
        downloadText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        downloadText.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (font != null) downloadText.font = font;
        successGO.SetActive(false);

        downloadGO.SetActive(false);
    }

    void CheckForUpdate()
    {
        int oldCount = activityItems.Count;
        LoadActivitiesFromLua();
        if (activityItems.Count > oldCount)
        {
            downloadGO.SetActive(true);
            successGO.SetActive(false);
            Debug.Log($"[Activity] 发现更新: {activityItems.Count} 个活动");
        }
        else
        {
            Debug.Log($"[Activity] 无更新. 共 {activityItems.Count} 个活动");
        }
    }

    void OnClickDownload()
    {
        successGO.SetActive(true);

        foreach (var b in dynamicButtons)
            if (b != null) Destroy(b.gameObject);
        dynamicButtons.Clear();
        dynamicBtnImgs.Clear();

        ApplyBaseButtonLabels();

        Transform parent = button1.transform.parent;
        for (int i = BaseButtonCount; i < activityItems.Count; i++)
        {
            int idx = i;
            var go = Instantiate(button1.gameObject, parent);
            go.name = $"Button{idx + 1}";

            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectActivity(idx));

            var btnImg = go.GetComponent<Image>();
            btnImg.color = normalColor;
            dynamicBtnImgs.Add(btnImg);

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = activityItems[idx].btn;

            dynamicButtons.Add(btn);
        }

        if (activityPanel.activeSelf)
            SelectActivity(0);

        StartCoroutine(HideDownloadAfterDelay(2f));
    }

    IEnumerator HideDownloadAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (downloadGO != null) downloadGO.SetActive(false);
        if (successGO != null) successGO.SetActive(false);
    }

    // ==================== Lua ====================

    void LoadActivitiesFromLua()
    {
        activityItems.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, "Lua", "activity_data.lua");
        if (!File.Exists(path)) return;

        string code = File.ReadAllText(path);
        var ret = luaEnv.DoString(code);
        if (ret == null || ret.Length == 0 || ret[0] == null) return;

        LuaTable data = ret[0] as LuaTable;
        if (data == null) return;

        LuaTable list = data.Get<LuaTable>("activities");
        if (list == null) { data.Dispose(); return; }

        int count = list.Length;
        for (int i = 1; i <= count; i++)
        {
            LuaTable item = list.Get<LuaTable>(i);
            activityItems.Add(new ActivityItem
            {
                btn = item.Get<string>("btn"),
                content = item.Get<string>("content"),
            });
            item.Dispose();
        }
        list.Dispose();
        data.Dispose();
    }

    // ==================== helpers ====================

    void ApplyBaseButtonLabels()
    {
        if (activityItems.Count > 0) SetButtonLabel(button1, activityItems[0].btn);
        if (activityItems.Count > 1) SetButtonLabel(button2, activityItems[1].btn);
        if (activityItems.Count > 2) SetButtonLabel(button3, activityItems[2].btn);
        if (activityItems.Count > 3) SetButtonLabel(button4, activityItems[3].btn);
    }

    void SetButtonLabel(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    // ==================== button actions ====================

    void OnClickActivityButton()
    {
        activityPanel.SetActive(!activityPanel.activeSelf);
        if (activityPanel.activeSelf)
            SelectActivity(0);
    }

    void SelectActivity(int index)
    {
        if (index < 0 || index >= activityItems.Count) return;

        contentText.text = activityItems[index].content;

        if (btn1Img != null) btn1Img.color = (index == 0) ? SelectedColor : normalColor;
        if (btn2Img != null) btn2Img.color = (index == 1) ? SelectedColor : normalColor;
        if (btn3Img != null) btn3Img.color = (index == 2) ? SelectedColor : normalColor;
        if (btn4Img != null) btn4Img.color = (index == 3) ? SelectedColor : normalColor;
        for (int i = 0; i < dynamicBtnImgs.Count; i++)
        {
            int dynIdx = BaseButtonCount + i;
            if (dynamicBtnImgs[i] != null)
                dynamicBtnImgs[i].color = (index == dynIdx) ? SelectedColor : normalColor;
        }
    }
}
