# Unity Lightmap Scaler Tool

An Editor Extension tool for efficiently bulk-setting and auto-correcting the `Scale In Lightmap` values for multiple objects in the Unity Editor. It is especially powerful when building large-scale background scenes using lightmappers like Bakery.

> **⚠️ Note: The Auto UV Scale Correction feature requires Bakery's `BakeryLightmapGroupSelector` component to be attached to the target objects (or their parent hierarchy).**

<img src="images/ui_main_en.webp" width="35%">

## What's New (v1.0.0)

- **v1.0.0**: Initial release! Implemented Manual Batch Setting and surface-area-based Auto UV Scale Correction.

## Overview

Manually adjusting the lightmap resolution (density) for each object when baking lightmaps for large background scenes is extremely tedious. This tool streamlines this process using two approaches:

1. **Manual Batch Setting**: Bulk-change the `Scale In Lightmap` for groups of objects with specific names using a filter feature.
2. **Auto UV Scale Correction**: Calculates the actual "surface area" of the 3D meshes and automatically assigns values so that objects of different sizes have a uniform lightmap density. (_Note: This feature requires Bakery's `BakeryLightmapGroupSelector` component to be attached to the target objects or their parent hierarchy._)

---

## Installation

1. Download the latest release ZIP file (e.g., `LightmapScalerTool_EN_v1.0.0.zip`) from the GitHub Releases page and extract it.
2. Place the extracted `LightmapScalerTool_EN.cs` inside a folder named **`Editor`** anywhere within your Unity project's `Assets` folder. (Example: `Assets/Editor/Tools/`)
3. Once compiled, **`Tools > Lightmap Scaler Tool`** will be added to the top menu in Unity, and it will be ready to use.

- Note: If you prefer the Japanese version, please download `LightmapScalerTool_JP_vX.X.X.zip`. The features are completely identical.

---

## Usage

When you open the tool window, there are two main feature sections.

### 1. Manual Batch Setting (For adjusting specific parts together)

Applies a specific scale value in bulk to all `MeshRenderer`s on the selected object(s) and their child hierarchies.

![Batch Setting UI](images/ui_batch_setting_en.webp)

- **Instructions**:
  1. Select the target parent object(s) in the Hierarchy (multiple selection supported).
  2. Enter the "Include Name Filter" or "Exclude Name Filter" if necessary (multiple terms can be comma-separated, e.g., `Wall,Floor`).
  3. Enter the "Batch Scale Value" and click the "Apply Batch Setting" button.

### 2. Auto UV Scale Correction (For naturally adjusting based on object surface area)

> **⚠️ Requirement**: This feature relies on the Bakery asset. All objects to be compared must have the `BakeryLightmapGroupSelector` component attached (either directly or on a parent object) and be assigned to the same Lightmap Group.

Compares the "3D surface area" among objects belonging to the same lightmap group and automatically assigns the optimal `Scale In Lightmap` value.

![Auto Correction UI](images/ui_auto_correction_en.webp)

- **UV Area Correction (Gamma Value) (0.01 - 1.0)**:
  Adjusts the strength of the correction. Values closer to 1.0 equalize the area differences more strongly, while values closer to 0 maintain the original ratios.

<table width="100%">
  <tr>
    <th width="33%" align="center">No Correction</th>
    <th width="33%" align="center">0.5</th>
    <th width="33%" align="center">0.7</th>
  </tr>
  <tr>
    <td align="center"><img src="images/gamma_basic_none.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_basic_05.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_basic_07.webp" width="100%"></td>
  </tr>
</table>

- **Instructions**:
  1. Select **exactly one** `MeshRenderer` that belongs to the Lightmap Group you want to correct.
  2. Set the "UV Area Correction (Gamma Value)" and "Overwrite Mode".
  3. Click the "Execute Scale Correction" button.

---

## Technical Details (Tips)

This auto-correction isn't just a simple scale alignment; it uses gamma correction based on the "median area".

1. Calculates the surface area of all meshes in the target group, using their **median** as the base area.
2. Divides each object's area by the base area to find the "relative area ratio".
3. Calculates the final target scale using the specified **UV Area Correction (Gamma Value)**:
   `targetScale = pow(1 / relativeRatio, gammaValue)`

- Here is an example of testing objects with the exact same shape but different scales. The left shows the setup before baking (testing environment), and the right shows the result after baking (lightmap applied). Below that, you can see how the allocated lightmap area changes when the gamma value is adjusted in this environment.

<table width="40%">
  <tr>
    <th align="center">Before Bake (Setup)</th>
    <th align="center">After Bake (Result)</th>
  </tr>
  <tr>
    <td align="center"><img src="images/mesh_setup_before.webp" width="100%"></td>
    <td align="center"><img src="images/mesh_setup_after.webp" width="100%"></td>
  </tr>
</table>

<table width="100%">
  <tr>
    <th width="20%" align="center">Before Correction</th>
    <th width="20%" align="center">0.2</th>
    <th width="20%" align="center">0.5</th>
    <th width="20%" align="center">0.7</th>
    <th width="20%" align="center">1.0</th>
  </tr>
  <tr>
    <td align="center"><img src="images/gamma_detail_before.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_detail_02.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_detail_05.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_detail_07.webp" width="100%"></td>
    <td align="center"><img src="images/gamma_detail_10.webp" width="100%"></td>
  </tr>
  <tr>
    <td align="center"><img src="images/inspector_value_before.webp" width="100%"></td>
    <td align="center"><img src="images/inspector_value_02.webp" width="100%"></td>
    <td align="center"><img src="images/inspector_value_05.webp" width="100%"></td>
    <td align="center"><img src="images/inspector_value_07.webp" width="100%"></td>
    <td align="center"><img src="images/inspector_value_10.webp" width="100%"></td>
  </tr>
</table>

---

## Frequently Asked Questions (FAQ)

**Q. If I run it by mistake, can I undo it?**  
A. Yes, all operations support Unity's standard `Undo` system. You can easily revert to the previous state using `Ctrl+Z`.

**Q. Will it overwrite values for objects I manually fine-tuned?**  
A. By changing the "Overwrite Mode" to "Only expand if too small" or "Only overwrite if needed", you can protect your intentionally set manual values while only raising the values of parts that are lacking.

**Q. Are inactive objects included in the correction target?**  
A. They are excluded by default, but by unchecking the "Exclude Inactive Objects" option, you can include disabled objects in the calculation target.
