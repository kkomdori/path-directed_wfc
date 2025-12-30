using UnityEngine;
using System.IO;

[CreateAssetMenu(fileName = "MapSettings", menuName = "MapSettings")]
public class MapSettingsSO : ScriptableObject
{
    [Header("Map Setting")]
    public Vector3Int cubeMapSize;
    public Vector3Int entryCell;
    public Vector3Int exitCell;
    [Range(0f, 30f)] public float pathNoise;
    [Range(0f, 1f)] public float windingTolerence;

    // 저장 파일 경로 (플랫폼 독립적 경로)
    private string SavePath => Path.Combine(Application.persistentDataPath, "map_settings.json");

    public void SaveToDisk()
    {
        string json = JsonUtility.ToJson(this, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("Settings Saved to: " + SavePath);
    }

    public void LoadFromDisk()
    {
        if (!File.Exists(SavePath)) return; // 저장된 파일 없으면 기본값 사용

        string json = File.ReadAllText(SavePath);
        JsonUtility.FromJsonOverwrite(json, this); // JSON 데이터를 현재 SO에 덮어씌움
        Debug.Log("Settings Loaded!");
    }
}