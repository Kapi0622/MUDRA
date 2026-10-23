using System;
using R3;

/// <summary>
/// クリア記録と設定値を持ち、変わるたびに保存する（B5）。
///
/// 持つのは「どのステージをクリアしたか・ベストのランクと時間」と、設定画面の3項目と、チュートリアルを終えたか（B6）だけ。
/// ファイルの読み書きはISaveStorageに任せ、ここではUnity APIを使わない。
/// ステージの解放はIsUnlockedで判定する。今は全ステージを最初から開けているが、
/// 順番解放に切り替える時はここに「前のステージをクリアしたか」の判定を足すだけで済む。
/// </summary>
public class ProgressModel : IDisposable
{
    private readonly ISaveStorage _storage;
    private readonly SaveData _data;

    private readonly ReactiveProperty<bool> _isFullScreen;
    public ReadOnlyReactiveProperty<bool> IsFullScreen => _isFullScreen;

    private readonly ReactiveProperty<bool> _showCameraPreview;
    public ReadOnlyReactiveProperty<bool> ShowCameraPreview => _showCameraPreview;

    private readonly ReactiveProperty<bool> _isAutoPauseEnabled;
    public ReadOnlyReactiveProperty<bool> IsAutoPauseEnabled => _isAutoPauseEnabled;

    /// <summary>直前に記録したバトルでベストを更新したか。Result画面の「新記録」に使う</summary>
    public bool LastResultWasNewBest { get; private set; }

    public ProgressModel(ISaveStorage storage)
    {
        _storage = storage;
        _data = storage.Load() ?? new SaveData();
        _data.stageRecords ??= new();
        _data.settings ??= new SettingsData();

        _isFullScreen = new ReactiveProperty<bool>(_data.settings.isFullScreen);
        _showCameraPreview = new ReactiveProperty<bool>(_data.settings.showCameraPreview);
        _isAutoPauseEnabled = new ReactiveProperty<bool>(_data.settings.isAutoPauseEnabled);
    }

    /// <summary>ステージを選べるか。今は全て開放</summary>
    public bool IsUnlocked(StageData stage) => stage != null;

    /// <summary>ステージのクリア記録。未クリアならnull</summary>
    public StageRecordData FindRecord(StageData stage)
    {
        if (stage == null) return null;

        var record = _data.stageRecords.Find(r => r.stageNumber == stage.stageNumber);
        return record != null && record.isCleared ? record : null;
    }

    /// <summary>
    /// バトルの結果を記録する。クリアしてベスト（ランクが上、または同じランクで時間が短い）を更新したら保存する。
    /// </summary>
    public void RecordBattle(BattleRecord battle)
    {
        LastResultWasNewBest = false;
        if (!battle.IsWin || battle.Stage == null || !battle.Rank.HasValue) return;

        int rank = (int)battle.Rank.Value;
        float time = battle.Stats.ElapsedSeconds;

        var record = _data.stageRecords.Find(r => r.stageNumber == battle.Stage.stageNumber);
        if (record == null)
        {
            record = new StageRecordData { stageNumber = battle.Stage.stageNumber };
            _data.stageRecords.Add(record);
        }

        bool isBetter = !record.isCleared
                        || rank > record.bestRank
                        || (rank == record.bestRank && time < record.bestTimeSeconds);
        if (!isBetter) return;

        record.isCleared = true;
        record.bestRank = rank;
        record.bestTimeSeconds = time;
        LastResultWasNewBest = true;
        _storage.Save(_data);
    }

    public void SetFullScreen(bool value)
    {
        _data.settings.isFullScreen = value;
        _isFullScreen.Value = value;
        _storage.Save(_data);
    }

    public void SetShowCameraPreview(bool value)
    {
        _data.settings.showCameraPreview = value;
        _showCameraPreview.Value = value;
        _storage.Save(_data);
    }

    public void SetAutoPauseEnabled(bool value)
    {
        _data.settings.isAutoPauseEnabled = value;
        _isAutoPauseEnabled.Value = value;
        _storage.Save(_data);
    }

    public void Dispose()
    {
        _isFullScreen.Dispose();
        _showCameraPreview.Dispose();
        _isAutoPauseEnabled.Dispose();
    }
}
