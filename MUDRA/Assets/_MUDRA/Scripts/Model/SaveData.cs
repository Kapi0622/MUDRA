using System;
using System.Collections.Generic;

/// <summary>
/// セーブファイルの中身（B5）。JSONに書き出すため、フィールドは全てpublicで、型はJsonUtilityが扱えるものに限る。
/// 中身を増やす時は、古いファイルに無いフィールドが既定値で読み込まれることを前提にする（versionで判別できるようにしておく）。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>形式の版。中身の形を変えた時に上げる</summary>
    public int version = 1;

    public List<StageRecordData> stageRecords = new();

    public SettingsData settings = new();
}

/// <summary>ステージ1つ分のクリア記録</summary>
[Serializable]
public class StageRecordData
{
    /// <summary>StageData.stageNumber。アセット名を変えても記録が外れないよう番号で引く</summary>
    public int stageNumber;

    public bool isCleared;

    /// <summary>BattleRankの整数値</summary>
    public int bestRank;

    /// <summary>ベストランクを出した時のクリア時間（秒）。同じランクなら短い方をベストにする</summary>
    public float bestTimeSeconds;
}

/// <summary>設定画面の値</summary>
[Serializable]
public class SettingsData
{
    public bool isFullScreen = true;
    public bool showCameraPreview = true;
    public bool isAutoPauseEnabled = true;

    // 音量（B7）。0〜1。古いファイルには無い項目なので、読み込み時はこの既定値になる
    public float bgmVolume = 0.7f;
    public float seVolume = 1f;
}
