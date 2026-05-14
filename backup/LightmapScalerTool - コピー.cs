using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class LightmapScalerTool : EditorWindow
{
    private const float MinScaleInLightmap = 0.05f;
    private const int DecimalPrecision = 2;
    private float minOccupancyRatio = 0.015f;

    private bool preserveRelativeScale = false;
    private bool overwriteIfSufficient = false;
    private bool unifyOccupancyMode = false;
    private bool gammaScaleMode = false;
    private float gammaValue = 0.5f;

    private float lastComputedOccupancy = -1f;

    [MenuItem("Tools/背景用ツール/ライトマップスケール調整")]
    public static void ShowWindow()
    {
        GetWindow<LightmapScalerTool>("Lightmap Scaler Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("ライトマップスケール補正ツール", EditorStyles.boldLabel);
        GUILayout.Space(5);

        unifyOccupancyMode = EditorGUILayout.Toggle("占有率を統一する（中央値基準）", unifyOccupancyMode);
        gammaScaleMode = EditorGUILayout.Toggle("占有率を比率維持で補正（ガンマ補正）", gammaScaleMode);

        if (!unifyOccupancyMode && !gammaScaleMode)
        {
            minOccupancyRatio = EditorGUILayout.FloatField("最小占有率（推定）", minOccupancyRatio);
            preserveRelativeScale = EditorGUILayout.Toggle("元の比率を保持", preserveRelativeScale);
        }

        if (gammaScaleMode)
        {
            GUILayout.Label("ガンマ値（0=補正なし, 1=通常補正）");
            gammaValue = EditorGUILayout.Slider(gammaValue, 0.0f, 2.0f);
            gammaValue = EditorGUILayout.FloatField("ガンマ値を直接入力", gammaValue);
        }

        overwriteIfSufficient = EditorGUILayout.Toggle("足りていても上書き", overwriteIfSufficient);

        GUILayout.Space(5);
        if (GUILayout.Button("選択オブジェクトの占有率を計算"))
        {
            ComputeAndShowOccupancySurfaceArea();
        }

        GUILayout.Space(5);
        if (lastComputedOccupancy >= 0f)
            GUILayout.Label($"推定占有率: {(lastComputedOccupancy * 100f):F2} %");
        else
            GUILayout.Label("未計算 または 計算できません");

        GUILayout.Space(10);
        if (GUILayout.Button("スケール補正を実行"))
        {
            AdjustScalesSurfaceArea();
        }
    }

    private void ComputeAndShowOccupancySurfaceArea()
    {
        lastComputedOccupancy = ComputeOccupancies(out _, out lastComputedOccupancy, useActualScale: true);
    }

    private float ComputeOccupancies(out List<(MeshRenderer r, float area, float occVirtual, float occActual)> list, out float dummy, bool useActualScale = false)
    {
        list = new List<(MeshRenderer, float, float, float)>();
        dummy = -1f;

        var selected = Selection.activeGameObject;
        if (selected == null) return -1f;

        var sel = selected.GetComponentInParent<BakeryLightmapGroupSelector>();
        var rSel = selected.GetComponent<MeshRenderer>();
        if (sel == null || rSel == null || sel.lmgroupAsset == null) return -1f;

        var all = GameObject.FindObjectsOfType<BakeryLightmapGroupSelector>()
            .Where(s => s.lmgroupAsset == sel.lmgroupAsset)
            .SelectMany(s => s.GetComponentsInChildren<MeshRenderer>())
            .ToList();

        float totalVirtual = 0f;
        float occTarget = -1f;

        foreach (var r in all)
        {
            float area = CalculateMeshSurfaceArea(r);
            float occVirtual = area * 1f;
            float occActual = area * r.scaleInLightmap;
            list.Add((r, area, occVirtual, occActual));
            totalVirtual += occVirtual;
            if (r == rSel) occTarget = useActualScale ? occActual : occVirtual;
        }

        if (totalVirtual <= 0f) return -1f;

        dummy = 0f;
        return occTarget / totalVirtual;
    }

    private void AdjustScalesSurfaceArea()
    {
        var totalOcc = ComputeOccupancies(out var all, out _, useActualScale: false);
        if (totalOcc < 0f) return;

        Undo.RecordObjects(all.Select(p => p.r).ToArray(), "Adjust Scales (Surface Area)");

        // モード③: ガンマ補正付き比率スケーリング
        if (gammaScaleMode)
        {
            var areas = all.Select(p => p.area).OrderBy(a => a).ToList();
            float medianArea = areas[areas.Count / 2];

            foreach (var p in all)
            {
                if (p.area <= 0f) continue;

                float relativeRatio = p.area / medianArea;
                float targetScale = Mathf.Pow(1f / relativeRatio, gammaValue);
                if (overwriteIfSufficient || p.r.scaleInLightmap < targetScale)
                {
                    p.r.scaleInLightmap = Mathf.Max((float)System.Math.Round(targetScale, DecimalPrecision), MinScaleInLightmap);
                }
            }

            Debug.Log($"ガンマ補正スケーリング完了：{all.Count} 件");
            return;
        }

        // モード①: 占有率統一（中央値基準）
        if (unifyOccupancyMode)
        {
            var areas = all.Select(p => p.area).OrderBy(a => a).ToList();
            float medianArea = areas[areas.Count / 2];

            foreach (var p in all)
            {
                if (p.area <= 0f) continue;

                float targetScale = medianArea / p.area;
                if (overwriteIfSufficient || p.r.scaleInLightmap < targetScale)
                {
                    p.r.scaleInLightmap = Mathf.Max((float)System.Math.Round(targetScale, DecimalPrecision), MinScaleInLightmap);
                }
            }

            Debug.Log($"占有率統一（中央値）完了：{all.Count} 件");
            return;
        }

        // モード②: 小さすぎるオブジェクトの補正
        float total = all.Sum(p => p.occVirtual);
        var smallObjs = all.Where(p => p.occVirtual / total < minOccupancyRatio).ToList();
        if (smallObjs.Count == 0) return;

        if (preserveRelativeScale)
        {
            float minRatio = smallObjs.Min(p => p.occVirtual / total);
            float scaleFactor = minOccupancyRatio / minRatio;

            foreach (var p in smallObjs)
            {
                float targetScale = Mathf.Max(scaleFactor, MinScaleInLightmap);
                if (overwriteIfSufficient || p.r.scaleInLightmap < targetScale)
                {
                    p.r.scaleInLightmap = (float)System.Math.Round(targetScale, DecimalPrecision);
                }
            }
        }
        else
        {
            foreach (var p in smallObjs)
            {
                float currentRatio = p.occVirtual / total;
                float targetScale = Mathf.Max(minOccupancyRatio / currentRatio, MinScaleInLightmap);
                if (overwriteIfSufficient || p.r.scaleInLightmap < targetScale)
                {
                    p.r.scaleInLightmap = (float)System.Math.Round(targetScale, DecimalPrecision);
                }
            }
        }

        Debug.Log($"小物補正完了：{smallObjs.Count} 件");
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
}
