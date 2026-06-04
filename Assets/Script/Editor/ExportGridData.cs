using UnityEditor;
using UnityEngine;
using System.IO;

public class ExportGridData : EditorWindow
{
    string exportPath = "E:/VisualStudio/Projects/GameServer/map_grid.bin";

    [MenuItem("Tools/Export Grid Data For Server")]
    public static void ShowWindow()
    {
        GetWindow<ExportGridData>("Export Grid Data");
    }

    void OnGUI()
    {
        GUILayout.Label("Export A* Grid Data to Binary File", EditorStyles.boldLabel);
        GUILayout.Space(10);

        exportPath = EditorGUILayout.TextField("Export Path:", exportPath);
        GUILayout.Space(10);

        if (GUILayout.Button("Export", GUILayout.Height(30)))
        {
            Export();
        }
    }

    void Export()
    {
        Astar_GridMap gridMap = FindObjectOfType<Astar_GridMap>();
        if (gridMap == null)
        {
            Debug.LogError("No Astar_GridMap found in the scene. Open the game scene first.");
            EditorUtility.DisplayDialog("Error", "No Astar_GridMap found in the scene.", "OK");
            return;
        }

        if (gridMap.gridMap == null)
        {
            Debug.LogError("Grid map is not initialized. Ensure the scene is playing or the grid has been generated.");
            EditorUtility.DisplayDialog("Error", "Grid map not initialized.", "OK");
            return;
        }

        int width = gridMap.mapWidth;
        int height = gridMap.mapHeight;

        using (var fs = new FileStream(exportPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(width);
            bw.Write(height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bw.Write(gridMap.gridMap[x, y].walkable ? (byte)1 : (byte)0);
                }
            }
        }

        Debug.Log($"Grid data exported to {exportPath} ({width}x{height})");
        EditorUtility.DisplayDialog("Success", $"Grid data exported.\n{width}x{height} grid -> {exportPath}", "OK");
    }
}
