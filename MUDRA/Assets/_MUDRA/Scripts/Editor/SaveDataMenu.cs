using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// セーブデータ（save.json）を手で消す・場所を開くEditorメニュー。
/// ビルド版は新しいビルドで自動的に初期化されるが（JsonFileSaveStorage）、エディタは記録を読み続けるので、
/// 初回プレイの流れを確かめたい時はここから消す。
///
/// 再生中は使えない。ProgressModelがメモリに持った値を次の保存で書き戻してしまい、消しても元に戻るため。
/// </summary>
public static class SaveDataMenu
{
    private const string ResetMenuPath = "MUDRA/Reset Save Data";
    private const string OpenFolderMenuPath = "MUDRA/Open Save Folder";

    [MenuItem(ResetMenuPath)]
    private static void ResetSaveData()
    {
        string path = JsonFileSaveStorage.FilePath;
        if (!File.Exists(path) && !File.Exists(JsonFileSaveStorage.TempFilePath))
        {
            Debug.Log($"[Save] セーブデータはありません: {path}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "セーブデータのリセット",
                $"クリア記録と設定を消して初期状態に戻します。\n{path}",
                "消す", "やめる"))
            return;

        File.Delete(path);
        File.Delete(JsonFileSaveStorage.TempFilePath);
        Debug.Log($"[Save] セーブデータを消しました: {path}");
    }

    [MenuItem(ResetMenuPath, true)]
    private static bool CanResetSaveData() => !EditorApplication.isPlaying;

    [MenuItem(OpenFolderMenuPath)]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
