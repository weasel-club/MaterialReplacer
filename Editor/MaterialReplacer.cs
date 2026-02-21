#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MaterialReplacerWindow : EditorWindow
{
    public enum Language
    {
        Korean,
        English,
        Japanese
    }

    private Language currentLanguage = Language.Korean; // 기본값

    private GameObject targetPrefab;
    private List<Material> uniqueMaterials;
    private List<string> materialUsage;
    private List<Material> newMaterials;

    // 옵션: 비활성 포함
    private bool includeInactive = false;

    // UI 상태
    private Vector2 scrollPos;

    // 언어별 문자열
    private string strSelectLanguage;
    private string strTargetObjectLabel;
    private string strMaterialUsedIn;
    private string strCloseButton;
    private string strDialogTitleConflict;
    private string strDialogMessageConflict;
    private string strDialogYes;
    private string strDialogNo;
    private string strIncludeInactiveLabel;
    private string strRefreshButton;
    private string strNoTargetHint;

    // EditorPrefs에 저장할 Key
    private const string PREF_KEY_LANGUAGE = "MaterialReplacer_Language";
    private const string PREF_KEY_INCLUDE_INACTIVE = "MaterialReplacer_IncludeInactive";

    [MenuItem("Tools/Material Replacer")]
    public static void ShowWindow()
    {
        GetWindow<MaterialReplacerWindow>("Material Replacer");
    }

    [MenuItem("GameObject/Material Replacer", false, 10)]
    public static void ShowWindowFromHierarchy()
    {
        var window = GetWindow<MaterialReplacerWindow>("Material Replacer");
        window.SetTargetPrefab(Selection.activeGameObject);
    }

    private void OnEnable()
    {
        // 저장된 언어/옵션 불러오기 (기본값: Korean, includeInactive=false)
        currentLanguage = (Language)EditorPrefs.GetInt(PREF_KEY_LANGUAGE, (int)Language.Korean);
        includeInactive = EditorPrefs.GetBool(PREF_KEY_INCLUDE_INACTIVE, false);

        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        UpdateLocalizedStrings();
    }

    private void OnDisable()
    {
        // 현재 언어/옵션 저장
        EditorPrefs.SetInt(PREF_KEY_LANGUAGE, (int)currentLanguage);
        EditorPrefs.SetBool(PREF_KEY_INCLUDE_INACTIVE, includeInactive);

        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    private void OnUndoRedoPerformed()
    {
        if (targetPrefab != null)
        {
            InitializeMaterials();
            Repaint();
        }
    }

    public void SetTargetPrefab(GameObject prefab)
    {
        targetPrefab = prefab;
        InitializeMaterials();
    }

    void OnGUI()
    {
        // 상단 컨트롤 영역
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        currentLanguage = (Language)EditorGUILayout.EnumPopup(strSelectLanguage, currentLanguage);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateLocalizedStrings();
        }

        EditorGUI.BeginChangeCheck();
        var newTarget = (GameObject)EditorGUILayout.ObjectField(strTargetObjectLabel, targetPrefab, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            if (newTarget != targetPrefab)
            {
                targetPrefab = newTarget;
                InitializeMaterials();
            }
        }

        EditorGUI.BeginChangeCheck();
        includeInactive = EditorGUILayout.ToggleLeft(strIncludeInactiveLabel, includeInactive);
        if (EditorGUI.EndChangeCheck())
        {
            if (targetPrefab != null)
                InitializeMaterials();
            EditorPrefs.SetBool(PREF_KEY_INCLUDE_INACTIVE, includeInactive);
        }

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = targetPrefab != null;
        if (GUILayout.Button(strRefreshButton, GUILayout.Height(22)))
        {
            InitializeMaterials();
        }
        GUI.enabled = true;

        if (GUILayout.Button(strCloseButton, GUILayout.Height(22)))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(6);

        if (targetPrefab == null)
        {
            EditorGUILayout.HelpBox(strNoTargetHint, MessageType.Info);
            return;
        }

        if (uniqueMaterials == null || uniqueMaterials.Count == 0)
        {
            InitializeMaterials();
        }

        // 소재 리스트
        EditorGUILayout.LabelField($"{(currentLanguage == Language.Korean ? "마테리얼 목록" : currentLanguage == Language.English ? "Materials" : "マテリアル一覧")} ({uniqueMaterials.Count})", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < uniqueMaterials.Count; i++)
        {
            var srcMat = uniqueMaterials[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            // 라벨
            GUILayout.Label(
                string.Format(
                    "{0} {1} ({2}: {3})",
                    (currentLanguage == Language.Korean
                        ? "마테리얼"
                        : currentLanguage == Language.English
                            ? "Material"
                            : "マテリアル"),
                    srcMat != null ? srcMat.name : "(null)",
                    strMaterialUsedIn,
                    materialUsage[i]
                ),
                EditorStyles.label
            );

            // 교체용 마테리얼 필드
            var newMat = (Material)EditorGUILayout.ObjectField(newMaterials[i], typeof(Material), false);

            // 변경 처리
            if (newMaterials[i] != newMat)
            {
                // 중복 검사
                if (newMat != null && newMaterials.Contains(newMat))
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        strDialogTitleConflict,
                        strDialogMessageConflict,
                        strDialogYes,
                        strDialogNo
                    );

                    if (confirm)
                    {
                        newMaterials[i] = newMat;
                        ApplyMaterialsToRenderers(targetPrefab.GetComponentsInChildren<Renderer>(includeInactive));
                        InitializeMaterials();
                    }
                    else
                    {
                        newMat = newMaterials[i];
                    }
                }
                else
                {
                    newMaterials[i] = newMat;
                    ApplyMaterialsToRenderers(targetPrefab.GetComponentsInChildren<Renderer>(includeInactive));
                    InitializeMaterials();
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 현재 언어에 따른 UI 문자열 업데이트
    /// </summary>
    void UpdateLocalizedStrings()
    {
        switch (currentLanguage)
        {
            case Language.Korean:
                strSelectLanguage = "언어 선택";
                strTargetObjectLabel = "타겟 오브젝트";
                strMaterialUsedIn = "사용 중";
                strCloseButton = "닫기";
                strDialogTitleConflict = "중복 마테리얼 경고";
                strDialogMessageConflict = "다른 부위에서 이미 사용 중인 마테리얼입니다. 정말 이 마테리얼로 변경하시겠습니까?";
                strDialogYes = "예";
                strDialogNo = "아니오";
                strIncludeInactiveLabel = "비활성 오브젝트 포함";
                strRefreshButton = "새로고침";
                strNoTargetHint = "타겟 오브젝트를 선택해주세요.";
                break;

            case Language.English:
                strSelectLanguage = "Select Language";
                strTargetObjectLabel = "Target Object";
                strMaterialUsedIn = "Used in";
                strCloseButton = "Close";
                strDialogTitleConflict = "Material Conflict Warning";
                strDialogMessageConflict = "This material is already used in another slot. Are you sure you want to change it?";
                strDialogYes = "Yes";
                strDialogNo = "No";
                strIncludeInactiveLabel = "Include Inactive GameObjects";
                strRefreshButton = "Refresh";
                strNoTargetHint = "Please select a target object.";
                break;

            case Language.Japanese:
                strSelectLanguage = "言語選択";
                strTargetObjectLabel = "ターゲットObject";
                strMaterialUsedIn = "使用中";
                strCloseButton = "閉じる";
                strDialogTitleConflict = "マテリアル重複警告";
                strDialogMessageConflict = "他の部位ですでに使用中のマテリアルです。本当にこのマテリアルに変更しますか？";
                strDialogYes = "はい";
                strDialogNo = "いいえ";
                strIncludeInactiveLabel = "非アクティブを含める";
                strRefreshButton = "再読み込み";
                strNoTargetHint = "ターゲットObjectを選択してください。";
                break;
        }
    }

    void InitializeMaterials()
    {
        if (targetPrefab == null) return;

        Renderer[] renderers = targetPrefab.GetComponentsInChildren<Renderer>(includeInactive);

        Dictionary<Material, List<string>> materialUsageDict = new Dictionary<Material, List<string>>();

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var sharedMaterial = renderer.sharedMaterials;
            if (sharedMaterial == null) continue;

            foreach (var material in sharedMaterial)
            {
                if (material == null) continue;

                if (!materialUsageDict.ContainsKey(material))
                {
                    materialUsageDict[material] = new List<string>();
                }
                materialUsageDict[material].Add(renderer.gameObject.name);
            }
        }

        uniqueMaterials = new List<Material>(materialUsageDict.Keys);
        materialUsage = new List<string>();

        foreach (var material in uniqueMaterials)
        {
            materialUsage.Add(string.Join(", ", materialUsageDict[material]));
        }

        newMaterials = new List<Material>(uniqueMaterials);
    }

    void ApplyMaterialsToRenderers(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return;

        Undo.RecordObjects(renderers, "Materials Changed");

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) continue;

            for (int i = 0; i < materials.Length; i++)
            {
                Material originalMaterial = materials[i];
                int index = uniqueMaterials.IndexOf(originalMaterial);
                if (index >= 0)
                {
                    materials[i] = newMaterials[index];
                }
            }
            renderer.sharedMaterials = materials;
        }

        EditorUtility.SetDirty(targetPrefab);
    }
}

#endif