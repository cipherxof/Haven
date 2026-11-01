# HavenStudio - MGS4 Stage Editor

A utility that allows you to edit stages for all games running on the MGS4 engine.

**Supported Games**

- Metal Gear Solid 4
- Metal Gear Online 2
- Metal Gear Arcade

<img width="2136" height="1158" alt="image" src="https://github.com/user-attachments/assets/ee34b060-ecbb-406d-ad2b-f1d978d059a5" />

## OctoCamo editing

This branch adds tools for inspecting and editing the surface data used when a character's OctoCamo contacts a stage. Haven reads the loaded stage's GEOM collision and OctoCamo table; the tools are not tied to a particular map. They are available only when the stage contains the required data.

1. Open a decrypted stage folder with **File > Open Folder**.
2. Choose **OctoCamo > Enable OctoCamo view** to show contact collision. Select a face to inspect its material/pattern and separate cloth colour. **Muscle texture** switches between a raw diffuse-pattern projection and the cloth-colour preview; neither is an exact render of the suit in-game.
3. For pattern images and stage-wide remapping, choose **OctoCamo > Load camo previews...** and select `slot_oct_list_online.slot` from the game installation. Haven remembers the selected folder. Then use **OctoCamo > Remap OctoCamo materials...** to assign a pattern to a GEOM material across the stage, including previously unassigned materials.
4. To edit individual or overlapping faces, click a face or use **Shift + left-drag** (or **OctoCamo > Box-select faces**) to list faces inside a box. Filter the list, tick individual faces or **Select all filtered**, and choose **Edit selected camo...** for a batch change. Focused and ticked faces remain outlined through the preview.
5. Choose **Save Map** to write pending polygon edits to the plaintext GEOM and stage-wide remaps to the OctoCamo table, as applicable.

Magenta indicates a missing mapping or preview. If a stage table contains conflicting duplicate rows for one material, Haven leaves that material unmapped and does not rewrite either row: the game's precedence is not yet established. Other materials remain available.

Cloth hue viewer
<img width="1920" height="1044" alt="Cloth hue viewer" src="https://github.com/user-attachments/assets/4ad1092b-f87d-44af-9fd9-b65d95f962af" />

Texture viewer
<img width="1920" height="1044" alt="Texture viewer" src="https://github.com/user-attachments/assets/199e72f0-628c-48bd-9400-b36639714dad" />

## Additional map-editor workflows (this branch)

The map editor also discovers supporting files from the opened stage rather than assuming one map's filenames or camera layout. These views have data to show only when Haven can decode the corresponding stage resource; an empty view on one stage does not mean every stage lacks that feature.

| Workflow | Where to find it | What an edit saves |
| --- | --- | --- |
| Placements and effects | **Placements**, **Effects**, **Add Object**, **Add Effect**; select an item for its Inspector | Writable GCX placement/script data or GEOM effects, according to the selected object's source. **Placement collision** separately previews referenced GEOM collision at the placed object's transform. |
| Spectator cameras | **View > Cameras**, then **Spectator cameras** in the outline | Camera position and target in the loaded GCX. The Inspector can **Use current view** or **Look through camera**. Only supported camera tables are shown. |
| Vegetation | **View > Vegetation**, then a PDL group or instance in the outline | Group movement or an individual instance's position in the linked PDL. |
| SDM area | **View > SDM Area**, then **SDM area** in the outline | The editable starting `area_max` radius in the loaded GCX. The final `area_min` boundary is displayed for context. |

Placement rotation
<img width="1920" height="1044" alt="Screenshot 2026-10-07 132753" src="https://github.com/user-attachments/assets/e50946b1-4dbf-4b86-8f6e-4a7cdf8be093" />

Effect highlighting with special behaviour for RACE goals show next available locations
<img width="1920" height="1044" alt="Effect highlighting with special behaviour for RACE goals show next available locations" src="https://github.com/user-attachments/assets/683cf097-d69f-49f7-94f9-48043876c8fa" />

Spectator Cameras
<img width="1920" height="1044" alt="Spectator Cameras" src="https://github.com/user-attachments/assets/5778eae7-be17-414f-a2ec-9ba53d8a1788" />

Vegetation viewer and editor as entire batch or individual instance
<img width="1920" height="1044" alt="Vegetation viewer and editor as entire batch or individual instance" src="https://github.com/user-attachments/assets/bed68f2f-5e19-46c3-b290-10c280e3ca2e" />

SDM circle starting size viewer and editor
<img width="1920" height="1044" alt="SDM circle starting size viewer and editor" src="https://github.com/user-attachments/assets/27f82d6c-b519-40c5-b7d7-d18433581eb2" />

## Build and test

HavenStudio targets .NET 10. From the repository root:

```text
dotnet build HavenStudio/HavenStudio.csproj
dotnet test HavenStudio.Tests/HavenStudio.Tests.csproj
```

The automated tests check editor and format behaviour. They do not establish that a saved stage is safe or visually correct in-game.

To run the optional, read-only stage-discovery checks against a local stage in PowerShell:

```powershell
$env:HAVEN_STAGE_FEATURE_ROOT = 'C:\path\to\stage'
dotnet test HavenStudio.Tests/HavenStudio.Tests.csproj --filter FullyQualifiedName~StageFeatureDiscoveryTests
```

These checks exercise OctoCamo discovery and camera-table scanning/editing in memory; they do not change the stage files. They have been run on AA, VV and JJ. Older offset-specific OctoCamo fixture tests remain explicitly JJ-only and use `HAVEN_JJ_OCTOCAMO_STAGE` instead.

## Credits

- GhzGangster and Jayveer for their various GCX and MDN projects
- Zoft for his dictionary contributions
- TrikzMe for some LT3 reverse engineering
