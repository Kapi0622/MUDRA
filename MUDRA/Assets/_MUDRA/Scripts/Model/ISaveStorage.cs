/// <summary>
/// セーブデータの読み書き先（B5）。
/// ProgressModelはファイルの扱い（Unity API）を知らず、このインターフェース越しにだけ保存する。
/// 手の認識でIHandLandmarkProviderを挟み、MediaPipeとキーボードを差し替えられるようにしているのと同じ形。
/// </summary>
public interface ISaveStorage
{
    /// <summary>保存されているデータ。無い・読めない場合はnull</summary>
    SaveData Load();

    void Save(SaveData data);
}
