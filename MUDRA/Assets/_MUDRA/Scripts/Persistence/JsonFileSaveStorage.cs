using System;
using System.IO;
using UnityEngine;

/// <summary>
/// セーブデータをJSONファイルで読み書きする（B5）。
/// 保存先は Application.persistentDataPath（Windowsでは AppData/LocalLow/&lt;会社名&gt;/&lt;製品名&gt;/）。
///
/// 書き込みは一時ファイルに書いてから差し替える。書き込みの途中で落ちても、前のファイルが壊れずに残るようにするため。
/// ファイルが無い・壊れている時はnullを返し、ProgressModelが初期状態で始める（ゲームは止めない）。
/// </summary>
public class JsonFileSaveStorage : ISaveStorage
{
    private const string FileName = "save.json";
    private const string TempSuffix = ".tmp";

    private readonly string _path;

    public JsonFileSaveStorage()
    {
        _path = Path.Combine(Application.persistentDataPath, FileName);
    }

    public SaveData Load()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(_path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] セーブデータを読めなかったため初期状態で始めます: {e.Message}");
            return null;
        }
    }

    public void Save(SaveData data)
    {
        string tempPath = _path + TempSuffix;
        try
        {
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tempPath, _path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] セーブデータを保存できませんでした: {e.Message}");
        }
    }
}
