using System;
using System.IO;
using UnityEngine;

/// <summary>
/// セーブデータをJSONファイルで読み書きする（B5）。
/// 保存先は Application.persistentDataPath（Windowsでは AppData/LocalLow/&lt;会社名&gt;/&lt;製品名&gt;/）。
///
/// 書き込みは一時ファイルに書いてから差し替える。書き込みの途中で落ちても、前のファイルが壊れずに残るようにするため。
/// ファイルが無い・壊れている時はnullを返し、ProgressModelが初期状態で始める（ゲームは止めない）。
///
/// ビルド版は、ファイルを書いたビルドのIDが今のビルドと違えばnullを返し、記録も設定も初期状態で始める。
/// エディタと同じファイルを共有しているため、テストプレイや前のビルドの記録を新しいビルドへ持ち込まないようにするため。
/// エディタはIDを見ずに読み、保存時は空のIDを書く（＝エディタで遊んだ後もビルド版は初期状態で始まる）。
/// </summary>
public class JsonFileSaveStorage : ISaveStorage
{
    private const string FileName = "save.json";
    private const string TempSuffix = ".tmp";

    /// <summary>セーブファイルの場所。Editorメニューの手動リセットも同じパスを使う</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>書き込み途中の一時ファイルの場所</summary>
    public static string TempFilePath => FilePath + TempSuffix;

    private readonly string _path;

    public JsonFileSaveStorage()
    {
        _path = FilePath;
    }

    public SaveData Load()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(_path));
#if !UNITY_EDITOR
            if (data != null && data.buildId != Application.buildGUID)
            {
                Debug.Log("[Save] 新しいビルドのため初期状態で始めます");
                return null;
            }
#endif
            return data;
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
        data.buildId = Application.buildGUID;
        try
        {
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(_path))
                File.Replace(tempPath, _path, null);
            else
                File.Move(tempPath, _path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] セーブデータを保存できませんでした: {e.Message}");
        }
    }
}
