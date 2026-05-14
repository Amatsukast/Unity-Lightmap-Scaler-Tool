using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class LightmapScalerTool : EditorWindow
{
    private const float MinScaleInLightmap = 0.05f;
    private const int DecimalPrecision = 2;

    private bool overwriteIfSufficient = false;
    private float gammaValue = 0.7f;

    // 上書きモードの列挙型を追加
    private enum OverwriteMode
    {
        Always,         // 常に上書き
        OnlyExpand,     // 小さすぎるものだけ上書き
        OnlyShrink,     // 大きすぎるものだけ上書き
        IfNeeded        // 必要な場合のみ上書き
    }
    private OverwriteMode overwriteMode = OverwriteMode.Always;

    // デフォルト値を定数で定義
    private const bool DefaultUnifyOccupancyMode = true;
    private const bool DefaultGammaScaleMode = true;
    private const float DefaultGammaValue = 0.7f;
    private const OverwriteMode DefaultOverwriteMode = OverwriteMode.Always;

    private string statusMessage = ""; // ステータスメッセージ用

    // 一括設定用の値
    private float batchScaleInLightmapValue = 1.0f; // デフォルト値を1に

    // フィルター用の入力欄
    private string includeNameFilter = "";
    private string excludeNameFilter = "";

    // 一括変換オプション
    private bool batchCaseSensitive = true;      // 大文字小文字を区別（デフォルトON）
    private bool batchOnlyActive = true;         // 非アクティブを含まない（デフォルトON）

    // 自動補正用：非アクティブを含めるかどうかのオプション（デフォルトON）
    private bool autoOnlyActive = true;

    [MenuItem("Tools/ライトマップスケール調整ツール(Cherry)")]
    public static void ShowWindow()
    {
        var window = GetWindow<LightmapScalerTool>("ライトマップスケール調整ツール");
        window.minSize = new Vector2(300, 560); // 初期サイズ（最小サイズ）
    }

    private void OnGUI()
    {
        GUILayout.Label("ライトマップスケール調整ツール", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 設定をデフォルトに戻すボタン
        if (GUILayout.Button("すべての設定をデフォルトに戻す", GUILayout.Height(30)))
        {
            ResetToDefaults();
            statusMessage = "設定をデフォルトに戻しました。";
        }

        GUILayout.Space(10);

        // ステータスメッセージ表示
        EditorGUILayout.HelpBox(
            string.IsNullOrEmpty(statusMessage) ? "オブジェクトを選択してください。" : statusMessage, // 初期ステータス
            MessageType.Info
        );

        GUILayout.Space(20);

        // --- 一括設定セクション ---
        GUILayout.Label("手動一括設定", EditorStyles.boldLabel);
        GUILayout.Label(
            "選択オブジェクト配下の「Scale In Lightmap」を一括で変更できます。\n" +
            "※フィルターはカンマ区切りで複数指定できます。",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);

        // フィルター入力欄（ラベル幅を狭める）
        float prevLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 110;
        includeNameFilter = EditorGUILayout.TextField("対象名フィルター", includeNameFilter);
        excludeNameFilter = EditorGUILayout.TextField("除外名フィルター", excludeNameFilter);

        // オプションチェックボックス
        batchCaseSensitive = EditorGUILayout.ToggleLeft("大文字小文字を区別する", batchCaseSensitive);
        batchOnlyActive = EditorGUILayout.ToggleLeft("非アクティブのオブジェクトを含めない", batchOnlyActive);

        GUILayout.Space(10);

        // 一括設定値のラベル幅も狭める
        batchScaleInLightmapValue = EditorGUILayout.FloatField("一括設定値", batchScaleInLightmapValue);
        EditorGUIUtility.labelWidth = prevLabelWidth; // 元に戻す

        GUILayout.Space(10);

        if (GUILayout.Button("一括反映", GUILayout.Height(30)))
        {
            ApplyBatchScaleInLightmap(batchScaleInLightmapValue, includeNameFilter, excludeNameFilter, batchCaseSensitive, batchOnlyActive);
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(20);

        // --- UVスケール補正セクション ---
        GUILayout.Label("UVスケール自動補正", EditorStyles.boldLabel);
        GUILayout.Label("オブジェクトの大きさに合わせて、UV割り当て面積を自動で調整します。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // ガンマ補正スライダーの説明
        GUILayout.Label("UV面積補正\n（0=元の比率を維持, 1=全て同じ面積に補正）");
        gammaValue = EditorGUILayout.Slider(gammaValue, 0.01f, 1.0f);

        GUILayout.Space(10);

        // 上書き条件のプルダウン
        string[] overwriteOptions = {
            "常に上書き",
            "小さすぎるものだけ上書き",
            "大きすぎるものだけ上書き",
            "必要な場合のみ上書き"
        };
        string overwriteHint =
            "常に上書き：\n現在値を無視して、全ての対象に上書き\n\n" +
            "小さすぎるものだけ上書き：\n現在値が補正結果より小さい対象のみ拡大\n\n" +
            "大きすぎるものだけ上書き：\n現在値が補正結果より大きい対象のみ縮小\n\n" +
            "必要な場合のみ上書き：\n現在値が補正結果と異なり、かつ補正結果が有効な場合のみ上書き";

        float prevLabelWidth2 = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 100;
        GUIContent overwriteLabel = new GUIContent("上書き条件 ⓘ", overwriteHint);
        GUILayout.BeginHorizontal();
        GUILayout.Label(overwriteLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
        overwriteMode = (OverwriteMode)EditorGUILayout.Popup((int)overwriteMode, overwriteOptions);
        GUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = prevLabelWidth2;

        // 非アクティブのオブジェクトを含めない（自動補正用）
        autoOnlyActive = EditorGUILayout.ToggleLeft("非アクティブのオブジェクトを含めない", autoOnlyActive);

        GUILayout.Space(10);

        // スケール補正実行ボタン（高さ40）
        if (GUILayout.Button("スケール補正を実行", GUILayout.Height(30)))
        {
            try
            {
                AdjustScalesSurfaceArea();
                // AdjustScalesSurfaceArea内でstatusMessageがセットされていなければ完了メッセージ
                if (string.IsNullOrEmpty(statusMessage))
                    statusMessage = "スケール補正が完了しました。";
            }
            catch (System.Exception ex)
            {
                statusMessage = $"エラー: {ex.Message}";
            }
        }

        EditorGUILayout.EndVertical();
    }

    // 設定をデフォルト値にリセットする
    private void ResetToDefaults()
    {
        gammaValue = DefaultGammaValue;
        overwriteMode = DefaultOverwriteMode;
        batchScaleInLightmapValue = 1.0f; // デフォルトに戻すときも1に

        // フィルターとチェックボックスもデフォルトに戻す
        includeNameFilter = "";
        excludeNameFilter = "";
        batchCaseSensitive = true;
        batchOnlyActive = true;
        autoOnlyActive = true; // ← 自動補正用もデフォルトONに戻す
    }

    private float ComputeOccupancies(out List<(MeshRenderer r, float area, float occVirtual, float occActual)> list, out float dummy, bool useActualScale = false, bool onlyActive = true)
    {
        list = new List<(MeshRenderer, float, float, float)>();
        dummy = -1f;

        var selected = Selection.activeGameObject;
        if (selected == null) return -1f;

        // 配下すべて
        var all = selected.GetComponentsInChildren<MeshRenderer>(!onlyActive).ToList();

        float totalVirtual = 0f;
        float occTarget = -1f;

        foreach (var r in all)
        {
            float area = CalculateMeshSurfaceArea(r);
            float occVirtual = area * 1f;
            float occActual = area * r.scaleInLightmap;
            list.Add((r, area, occVirtual, occActual));
            totalVirtual += occVirtual;
            // occTargetは「選択オブジェクト自身」ではなく「全体の合計」にする
        }

        if (totalVirtual <= 0f) return -1f;

        dummy = 0f;
        // occTargetが-1fのままでも、totalVirtualで割って1.0fを返す
        return 1.0f;
    }

    private bool ShouldOverwrite(float current, float target)
    {
        const float EPSILON = 1e-6f;

        if (Mathf.Approximately(target, 1f))
            return false; // 変化なし

        // Always以外のときだけ、ユーザー値優先判定を行う
        if (overwriteMode != OverwriteMode.Always)
        {
            // 拡大方向
            if (target > 1f && current - target > EPSILON)
                return false; // さらに拡大している場合は上書きしない

            // 縮小方向
            if (target < 1f && target - current > EPSILON)
                return false; // さらに縮小している場合は上書きしない
        }

        // ここから下は従来の上書き条件
        switch (overwriteMode)
        {
            case OverwriteMode.Always:
                return true;
            case OverwriteMode.OnlyExpand:
                return current < target - EPSILON;
            case OverwriteMode.OnlyShrink:
                return current > target + EPSILON;
            case OverwriteMode.IfNeeded:
                return !Mathf.Approximately(current, target);
            default:
                return false;
        }
    }

    // スケール補正のメイン処理
    private void AdjustScalesSurfaceArea()
    {
        if (Selection.activeGameObject == null)
        {
            statusMessage = "オブジェクトが選択されていません。";
            return;
        }
        if (Selection.gameObjects != null && Selection.gameObjects.Length > 1)
        {
            statusMessage = "複数のオブジェクトが選択されています。1つだけ選択してください。";
            return;
        }

        var selected = Selection.activeGameObject;

        // 配下のMeshRendererを取得
        var meshRenderers = selected.GetComponentsInChildren<MeshRenderer>(!autoOnlyActive);
        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            statusMessage = "配下に有効なMeshRendererが存在しません。";
            return;
        }

        // ComputeOccupanciesで配下MeshRendererを使う
        var totalOcc = ComputeOccupancies(out var all, out _, useActualScale: false, onlyActive: autoOnlyActive);

        if (totalOcc < 0f || all == null || all.Count == 0)
        {
            statusMessage = "有効なMeshRendererが存在しません。";
            return;
        }

        Undo.RecordObjects(all.Select(p => p.r).ToArray(), "Adjust Scales (Surface Area)");

        var areas = all.Select(p => p.area).OrderBy(a => a).ToList();
        float medianArea = areas[areas.Count / 2];

        int changedCount = 0;
        int skippedCount = 0;
        foreach (var p in all)
        {
            if (p.area <= 0f) continue;

            float relativeRatio = p.area / medianArea;
            float targetScale = Mathf.Pow(1f / relativeRatio, gammaValue);
            if (ShouldOverwrite(p.r.scaleInLightmap, targetScale))
            {
                p.r.scaleInLightmap = Mathf.Max((float)System.Math.Round(targetScale, DecimalPrecision), MinScaleInLightmap);
                changedCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        statusMessage = $"スケール自動補正完了：\n{changedCount} 件を補正、{skippedCount} 件は上書き条件により除外されました。";
        Debug.Log($"ガンマ補正スケーリング完了：{all.Count} 件（{changedCount} 件補正、{skippedCount} 件除外）");

        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    private float CalculateMeshSurfaceArea(MeshRenderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return 0f;

        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;

        float area = 0f;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = r.transform.TransformPoint(verts[tris[i]]);
            Vector3 b = r.transform.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = r.transform.TransformPoint(verts[tris[i + 2]]);
            area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
        return area;
    }

    // 追加：選択オブジェクト配下の scale in lightmap を一括設定
    private void ApplyBatchScaleInLightmap(float value, string includeFilter, string excludeFilter)
    {
        // 選択中のGameObjectを取得
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            statusMessage = "オブジェクトが選択されていません。";
            return;
        }

        // 配下の全MeshRendererを再帰的に取得
        var renderers = new List<MeshRenderer>();
        foreach (var go in selected)
        {
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));
        }

        // フィルター適用
        var filtered = renderers.Where(r =>
        {
            string name = r.gameObject.name;
            bool include = string.IsNullOrEmpty(includeFilter) || name.Contains(includeFilter);
            bool exclude = !string.IsNullOrEmpty(excludeFilter) && name.Contains(excludeFilter);
            return include && !exclude;
        }).ToList();

        if (filtered.Count == 0)
        {
            statusMessage = "条件に合致するオブジェクトが見つかりませんでした。";
            return;
        }

        // Undo登録
        Undo.RecordObjects(filtered.ToArray(), "Batch Set Scale In Lightmap");

        // 一括で値を設定
        foreach (var r in filtered)
        {
            r.scaleInLightmap = value;
        }

        statusMessage = $"一括設定完了：{filtered.Count} 件のオブジェクトに適用しました。";

        // Inspectorを即時再描画
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    // 選択オブジェクト配下の scale in lightmap を一括設定（オプション対応）
    private void ApplyBatchScaleInLightmap(
        float value,
        string includeFilter,
        string excludeFilter,
        bool caseSensitive,
        bool onlyActive)
    {
        // 選択中のGameObjectを取得
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            statusMessage = "オブジェクトが選択されていません。";
            return;
        }

        // 配下の全MeshRendererを再帰的に取得（onlyActive対応）
        var renderers = new List<MeshRenderer>();
        foreach (var go in selected)
        {
            // 選択オブジェクト自身もactiveSelfチェック
            if (onlyActive && !go.activeInHierarchy) continue;
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(!onlyActive));
        }

        // フィルター適用
        var filtered = renderers.Where(r =>
        {
            string name = r.gameObject.name;
            string inc = includeFilter ?? "";
            string exc = excludeFilter ?? "";

            // 大文字小文字区別
            if (!caseSensitive)
            {
                name = name.ToLower();
                inc = inc.ToLower();
                exc = exc.ToLower();
            }

            bool include = string.IsNullOrEmpty(inc) || name.Contains(inc);
            bool exclude = !string.IsNullOrEmpty(exc) && name.Contains(exc);
            return include && !exclude;
        }).ToList();

        if (filtered.Count == 0)
        {
            statusMessage = "条件に合致するオブジェクトが見つかりませんでした。";
            return;
        }

        // Undo登録
        Undo.RecordObjects(filtered.ToArray(), "Batch Set Scale In Lightmap");

        // 一括で値を設定
        foreach (var r in filtered)
        {
            r.scaleInLightmap = value;
        }

        statusMessage = $"一括設定完了：{filtered.Count} 件のオブジェクトに適用しました。";

        // Inspectorを即時再描画
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}