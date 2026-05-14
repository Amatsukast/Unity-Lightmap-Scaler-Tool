using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class LightmapScalerTool_EN : EditorWindow
{
    private const float MinScaleInLightmap = 0.05f;
    private const int DecimalPrecision = 2;

    private bool overwriteIfSufficient = false;
    private float gammaValue = 0.7f;

    // Overwrite mode enum
    private enum OverwriteMode
    {
        Always,         // Always overwrite
        OnlyExpand,     // Overwrite only if it expands
        OnlyShrink,     // Overwrite only if it shrinks
        IfNeeded        // Overwrite only if needed
    }
    private OverwriteMode overwriteMode = OverwriteMode.Always;

    // Default values
    private const bool DefaultUnifyOccupancyMode = true;
    private const bool DefaultGammaScaleMode = true;
    private const float DefaultGammaValue = 0.7f;
    private const OverwriteMode DefaultOverwriteMode = OverwriteMode.Always;

    private string statusMessage = ""; // Status message

    // Batch setting values
    private float batchScaleInLightmapValue = 1.0f; // Default 1

    // Filter inputs
    private string includeNameFilter = "";
    private string excludeNameFilter = "";

    // Batch conversion options
    private bool batchCaseSensitive = true;      // Case sensitive (default ON)
    private bool batchOnlyActive = true;         // Exclude inactive (default ON)

    // Auto-correction: Exclude inactive option (default ON)
    private bool autoOnlyActive = true;

    [MenuItem("Tools/Lightmap Scaler Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<LightmapScalerTool_EN>("Lightmap Scaler Tool");
        window.minSize = new Vector2(300, 580); // Initial size (min size)
    }

    private void OnGUI()
    {
        GUILayout.Label("Lightmap Scaler Tool", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Reset to defaults button
        if (GUILayout.Button("Reset All Settings to Default", GUILayout.Height(30)))
        {
            ResetToDefaults();
            statusMessage = "Settings have been reset to default.";
        }

        GUILayout.Space(10);

        // Status message display
        EditorGUILayout.HelpBox(
            string.IsNullOrEmpty(statusMessage) ? "Please select an object." : statusMessage,
            MessageType.Info
        );

        GUILayout.Space(20);

        // --- Batch Setting Section ---
        GUILayout.Label("Manual Batch Setting", EditorStyles.boldLabel);
        GUILayout.Label(
            "Batch change 'Scale In Lightmap' for the selected objects and their children.\n" +
            "* Filters can be specified with comma separation.",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);

        // Filter inputs (narrow label width)
        float prevLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 130;
        includeNameFilter = EditorGUILayout.TextField("Include Name Filter", includeNameFilter);
        excludeNameFilter = EditorGUILayout.TextField("Exclude Name Filter", excludeNameFilter);

        // Option checkboxes
        batchCaseSensitive = EditorGUILayout.ToggleLeft("Case Sensitive", batchCaseSensitive);
        batchOnlyActive = EditorGUILayout.ToggleLeft("Exclude Inactive Objects", batchOnlyActive);

        GUILayout.Space(10);

        // Batch target value
        batchScaleInLightmapValue = EditorGUILayout.FloatField("Batch Target Value", batchScaleInLightmapValue);
        EditorGUIUtility.labelWidth = prevLabelWidth; // Restore

        GUILayout.Space(10);

        if (GUILayout.Button("Apply Batch", GUILayout.Height(30)))
        {
            ApplyBatchScaleInLightmap(batchScaleInLightmapValue, includeNameFilter, excludeNameFilter, batchCaseSensitive, batchOnlyActive);
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(20);

        // --- Auto UV Scale Correction Section ---
        GUILayout.Label("Automatic UV Scale Adjustment", EditorStyles.boldLabel);
        GUILayout.Label("Automatically adjusts the assigned UV area based on the physical size of the objects.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // Gamma correction slider description
        GUILayout.Label("UV Area Correction\n(0 = Keep original ratio, 1 = Force equal area)");
        gammaValue = EditorGUILayout.Slider(gammaValue, 0.01f, 1.0f);

        GUILayout.Space(10);

        // Overwrite condition dropdown
        string[] overwriteOptions = {
            "Always Overwrite",
            "Overwrite Only If Too Small (Expand)",
            "Overwrite Only If Too Large (Shrink)",
            "Smart Overwrite"
        };
        string overwriteHint =
            "Always Overwrite:\nIgnores the current value and overwrites all targets with the calculated value.\n\n" +
            "Overwrite Only If Too Small:\nOverwrites only targets where the current value is smaller than the calculated value, expanding them.\n\n" +
            "Overwrite Only If Too Large:\nOverwrites only targets where the current value is larger than the calculated value, shrinking them.\n\n" +
            "Smart Overwrite:\nCorrects scale in both directions (expand and shrink). However, any object already set beyond the target value (above or below) is treated as an intentional manual adjustment and will be preserved.";

        float prevLabelWidth2 = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 140;
        GUIContent overwriteLabel = new GUIContent("Overwrite Condition ⓘ", overwriteHint);
        GUILayout.BeginHorizontal();
        GUILayout.Label(overwriteLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
        overwriteMode = (OverwriteMode)EditorGUILayout.Popup((int)overwriteMode, overwriteOptions);
        GUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = prevLabelWidth2;

        // Exclude inactive for auto correction
        autoOnlyActive = EditorGUILayout.ToggleLeft("Exclude Inactive Objects", autoOnlyActive);

        GUILayout.Space(10);

        // Scale correction execute button
        if (GUILayout.Button("Execute Scale Adjustment", GUILayout.Height(30)))
        {
            try
            {
                AdjustScalesSurfaceArea();
                if (string.IsNullOrEmpty(statusMessage))
                    statusMessage = "Scale adjustment complete.";
            }
            catch (System.Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void ResetToDefaults()
    {
        gammaValue = DefaultGammaValue;
        overwriteMode = DefaultOverwriteMode;
        batchScaleInLightmapValue = 1.0f;

        includeNameFilter = "";
        excludeNameFilter = "";
        batchCaseSensitive = true;
        batchOnlyActive = true;
        autoOnlyActive = true;
    }

    private float ComputeOccupancies(out List<(MeshRenderer r, float area, float occVirtual, float occActual)> list, out float dummy, bool useActualScale = false, bool onlyActive = true)
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
            .SelectMany(s => s.GetComponentsInChildren<MeshRenderer>(!onlyActive))
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

    private bool ShouldOverwrite(float current, float target)
    {
        const float EPSILON = 1e-6f;

        // Skip if current and target are already the same
        if (Mathf.Approximately(current, target))
            return false;

        // Protect user's manual settings (active for all modes except Always)
        if (overwriteMode != OverwriteMode.Always)
        {
            // Expand direction (>= closes the target=1.0 hole)
            if (target >= 1f && current - target > EPSILON)
                return false;

            // Shrink direction (<= closes the target=1.0 hole)
            if (target <= 1f && target - current > EPSILON)
                return false;
        }

        // Per-mode overwrite conditions
        switch (overwriteMode)
        {
            case OverwriteMode.Always:
                return true;
            case OverwriteMode.OnlyExpand:
                return current < target - EPSILON;
            case OverwriteMode.OnlyShrink:
                return current > target + EPSILON;
            case OverwriteMode.IfNeeded:
                return true; // Passed protection logic, so overwrite
            default:
                return false;
        }
    }

    private void AdjustScalesSurfaceArea()
    {
        if (Selection.activeGameObject == null)
        {
            statusMessage = "No object selected.";
            return;
        }

        if (Selection.gameObjects != null && Selection.gameObjects.Length > 1)
        {
            statusMessage = "Multiple objects selected. Please select only one.";
            return;
        }

        var selected = Selection.activeGameObject;
        var selGroup = selected.GetComponentInParent<BakeryLightmapGroupSelector>();
        if (selGroup == null || selGroup.lmgroupAsset == null)
        {
            statusMessage = "No 'Lightmap Group' found on the selected object.";
            return;
        }

        var totalOcc = ComputeOccupancies(out var all, out _, useActualScale: false, onlyActive: autoOnlyActive);

        if (totalOcc < 0f || all == null || all.Count == 0)
        {
            statusMessage = "The selected object is not valid.";
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

        statusMessage = $"Auto-correction complete:\n{changedCount} adjusted, {skippedCount} skipped due to overwrite conditions.";
        Debug.Log($"Gamma correction scaling complete: {all.Count} total ({changedCount} adjusted, {skippedCount} skipped)");

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

    private void ApplyBatchScaleInLightmap(float value, string includeFilter, string excludeFilter)
    {
        // Unused overload but kept for parity
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            statusMessage = "No object selected.";
            return;
        }

        var renderers = new List<MeshRenderer>();
        foreach (var go in selected)
        {
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));
        }

        var filtered = renderers.Where(r =>
        {
            string name = r.gameObject.name;
            bool include = string.IsNullOrEmpty(includeFilter) || name.Contains(includeFilter);
            bool exclude = !string.IsNullOrEmpty(excludeFilter) && name.Contains(excludeFilter);
            return include && !exclude;
        }).ToList();

        if (filtered.Count == 0)
        {
            statusMessage = "No objects found matching the criteria.";
            return;
        }

        Undo.RecordObjects(filtered.ToArray(), "Batch Set Scale In Lightmap");

        foreach (var r in filtered)
        {
            r.scaleInLightmap = value;
        }

        statusMessage = $"Batch setting complete: Applied to {filtered.Count} objects.";

        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    private void ApplyBatchScaleInLightmap(
        float value,
        string includeFilter,
        string excludeFilter,
        bool caseSensitive,
        bool onlyActive)
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            statusMessage = "No object selected.";
            return;
        }

        var renderers = new List<MeshRenderer>();
        foreach (var go in selected)
        {
            if (onlyActive && !go.activeInHierarchy) continue;
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(!onlyActive));
        }

        var filtered = renderers.Where(r =>
        {
            string name = r.gameObject.name;
            string inc = includeFilter ?? "";
            string exc = excludeFilter ?? "";

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
            statusMessage = "No objects found matching the criteria.";
            return;
        }

        Undo.RecordObjects(filtered.ToArray(), "Batch Set Scale In Lightmap");

        foreach (var r in filtered)
        {
            r.scaleInLightmap = value;
        }

        statusMessage = $"Batch setting complete: Applied to {filtered.Count} objects.";

        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}
