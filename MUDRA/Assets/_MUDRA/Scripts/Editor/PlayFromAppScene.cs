using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// エディタの再生ボタンを、開いているシーンに関係なく常駐シーン（App）から始めるEditor拡張（B5）。
/// 実機と同じくタイトルから始まるので、InGameを開いて作業していても通しの流れを確認できる。
///
/// ステージから直接試したい時はメニューでOFFにする。OFFの間はInGameを開いて再生すると、
/// 従来どおりタイトルを飛ばしてバトルが始まる（BattleInitializerがAppを加算ロードする経路）。
/// 設定はEditorPrefsに保存するので、プロジェクトではなくこのPCのエディタごとに持つ。
/// </summary>
[InitializeOnLoad]
public static class PlayFromAppScene
{
    private const string MenuPath = "MUDRA/Play From App Scene";
    private const string PrefsKey = "MUDRA.PlayFromAppScene";
    private const string AppScenePath = "Assets/_MUDRA/Scenes/App.unity";

    private static bool IsEnabled
    {
        get => EditorPrefs.GetBool(PrefsKey, true);
        set => EditorPrefs.SetBool(PrefsKey, value);
    }

    // playModeStartSceneはシリアライズされずドメインリロードで消えるため、読み込みのたびに設定し直す
    static PlayFromAppScene()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        IsEnabled = !IsEnabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static void Apply()
    {
        EditorSceneManager.playModeStartScene = IsEnabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(AppScenePath)
            : null;
    }
}
