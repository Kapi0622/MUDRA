using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MUDRA.Data;
using R3;

/// <summary>
/// ステージの決着から、Result画面へ移るまでの間を受け持つ（B5）。
///
/// 決着の瞬間に画面を切り替えると、ボスの撃破演出や「討伐」「敗北」の表示が見えないまま終わってしまう。
/// Modelは演出の完了を待てない（Viewを知らない）ので、SectionProgressManagerと同じく
/// PresentationTimingDataの尺から待ち時間を決め、待ち終えたらOnFinishedを流す。
///
/// 待機はゲーム内時間（timeScaleに従う）。ポーズ中は進まず、ボス撃破のスローの間はその分だけ延びる。
/// </summary>
public class BattleEndModel : IDisposable
{
    private readonly Subject<bool> _onFinished = new();
    /// <summary>決着の演出を見せ終えた時に発火する。引数はクリアならtrue</summary>
    public Observable<bool> OnFinished => _onFinished;

    private readonly SectionProgressManager _sectionProgressManager;
    private readonly PresentationTimingData _timing;
    private readonly CompositeDisposable _disposables = new();
    private CancellationTokenSource _waitCts;

    public BattleEndModel(SectionProgressManager sectionProgressManager, PresentationTimingData timing)
    {
        _sectionProgressManager = sectionProgressManager;
        _timing = timing;

        // OnStageClearedの時点ではCurrentSectionはまだ撃破したセクションを指している
        _sectionProgressManager.OnStageCleared
            .Subscribe(_ => StartWait(true, _timing.ClearToResultDelay(_sectionProgressManager.CurrentSection.isBoss)))
            .AddTo(_disposables);

        _sectionProgressManager.OnGameOver
            .Subscribe(_ => StartWait(false, _timing.GameOverToResultDelay))
            .AddTo(_disposables);

        // デバッグのステージ/セクションジャンプで戦闘が再開したら、決着は無かったことにする
        _sectionProgressManager.OnSectionStarted
            .Subscribe(_ => CancelWait())
            .AddTo(_disposables);
    }

    private void StartWait(bool isWin, float delaySeconds)
    {
        CancelWait();
        _waitCts = new CancellationTokenSource();
        WaitAsync(isWin, delaySeconds, _waitCts.Token).Forget();
    }

    private async UniTaskVoid WaitAsync(bool isWin, float delaySeconds, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _onFinished.OnNext(isWin);
    }

    private void CancelWait()
    {
        _waitCts?.Cancel();
        _waitCts?.Dispose();
        _waitCts = null;
    }

    public void Dispose()
    {
        CancelWait();
        _disposables.Dispose();
        _onFinished.Dispose();
    }
}
