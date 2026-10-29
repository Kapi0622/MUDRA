using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BGMを鳴らすView（B7）。App に1つだけ置く（InGame の読み込み・破棄をまたいで鳴り続けるように）。
/// バトル側からは BattleContext.Bgm で受け取る。
///
/// AudioSource は1つ。曲の切り替えは「今の曲をフェードアウト → 次の曲を頭からフェードイン」で、重ねない。
/// フェードは実時間で進める（ポーズ・ヒットストップの間も止まらない。BGM はポーズ中も流す方針）。
///
/// ジングル（討伐・敗北）は1回だけ鳴らす。ジングルの最中に次の曲を頼まれたら、鳴り終わるのを待ってから始める。
/// 決着の直後に Result へ移っても、勝敗の音が途中で切れたり、Result の曲と重なったりしないようにするため。
///
/// 要求は後から来たものが勝つ。前の要求のフェードや待ちは取り消す。
/// </summary>
public class BgmPlayer : MonoBehaviour
{
    private const float FadeOutSeconds = 0.8f;
    private const float FadeInSeconds = 0.5f;
    /// <summary>ジングルをすぐ鳴らす時（敗北）の前の曲の止め方。勝敗の表示に遅れないよう短くする</summary>
    private const float JingleFadeOutSeconds = 0.2f;
    private const float FullVolume = 1f;
    private const float SilentVolume = 0f;

    [Tooltip("出力先（MX_Main の BGM グループ）。設定画面のBGMの音量はこのグループにかかる")]
    [SerializeField] private AudioMixerGroup _outputGroup;

    private AudioSource _source;

    // 最後に頼まれたループ曲。同じ曲を頼まれても頭に戻さない（道中のセクションが変わるたびに呼ばれる）
    private AudioClip _requestedClip;
    private bool _isJinglePlaying;

    private CancellationTokenSource _requestCts;
    private MotionHandle _fadeHandle;
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.volume = SilentVolume;
        _source.outputAudioMixerGroup = _outputGroup;
    }

    /// <summary>
    /// ループ曲を流す。今と同じ曲なら何もしない。clip が null なら止める。
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null)
        {
            Stop();
            return;
        }

        EnsureInitialized();
        if (clip == _requestedClip) return;

        _requestedClip = clip;
        PlayLoopAsync(clip, RenewRequest()).Forget();
    }

    /// <summary>
    /// ジングルを1回だけ鳴らす。今の曲はすぐに止める。
    /// </summary>
    /// <param name="delay">鳴らすまでの待ち時間（ゲーム内時間）。BattleResultView.ShowClear の delay と同じ数え方にして、表示と揃える</param>
    public void PlayJingle(AudioClip clip, float delay = 0f)
    {
        EnsureInitialized();
        _requestedClip = null;
        PlayJingleAsync(clip, delay, RenewRequest()).Forget();
    }

    /// <summary>フェードアウトして止める</summary>
    public void Stop()
    {
        EnsureInitialized();
        _requestedClip = null;
        StopAsync(RenewRequest()).Forget();
    }

    private async UniTaskVoid PlayLoopAsync(AudioClip clip, CancellationToken token)
    {
        try
        {
            if (_isJinglePlaying)
                await UniTask.WaitWhile(() => _source.isPlaying, cancellationToken: token);
            else
                await FadeAsync(SilentVolume, FadeOutSeconds, token);

            StartClip(clip, isLoop: true);
            await FadeAsync(FullVolume, FadeInSeconds, token);
        }
        catch (OperationCanceledException)
        {
            // 次の要求に置き換えられた・破棄された
        }
    }

    private async UniTaskVoid PlayJingleAsync(AudioClip clip, float delay, CancellationToken token)
    {
        try
        {
            // 前の曲を止めるのと、表示を待つのを並べて行う。
            // 待つ時間があれば（ボス撃破）普段どおりゆっくり消し、無ければ（敗北）すぐ消して鳴らす
            float fadeSeconds = delay >= FadeOutSeconds ? FadeOutSeconds : JingleFadeOutSeconds;
            await UniTask.WhenAll(
                FadeAsync(SilentVolume, fadeSeconds, token),
                UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token));

            _source.Stop();
            if (clip == null) return;

            StartClip(clip, isLoop: false);
            _source.volume = FullVolume;
        }
        catch (OperationCanceledException)
        {
            // 次の要求に置き換えられた・破棄された
        }
    }

    private async UniTaskVoid StopAsync(CancellationToken token)
    {
        try
        {
            await FadeAsync(SilentVolume, FadeOutSeconds, token);
            _source.Stop();
            _isJinglePlaying = false;
        }
        catch (OperationCanceledException)
        {
            // 次の要求に置き換えられた・破棄された
        }
    }

    private void StartClip(AudioClip clip, bool isLoop)
    {
        _source.Stop();
        _source.clip = clip;
        _source.loop = isLoop;
        _source.volume = SilentVolume;
        _source.Play();
        _isJinglePlaying = !isLoop;
    }

    /// <summary>今の音量から target まで実時間で変える。鳴っていなければ音量だけ合わせてすぐ終わる</summary>
    private UniTask FadeAsync(float target, float duration, CancellationToken token)
    {
        CancelFade();
        if (!_source.isPlaying)
        {
            _source.volume = target;
            return UniTask.CompletedTask;
        }

        _fadeHandle = LMotion.Create(_source.volume, target, duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(volume => _source.volume = volume);
        return _fadeHandle.ToUniTask(token);
    }

    /// <summary>前の要求を取り消し、新しい要求用のトークンを返す</summary>
    private CancellationToken RenewRequest()
    {
        CancelRequest();
        _requestCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        return _requestCts.Token;
    }

    private void CancelRequest()
    {
        CancelFade();
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = null;
    }

    private void CancelFade()
    {
        if (_fadeHandle.IsActive()) _fadeHandle.Cancel();
    }

    private void OnDestroy()
    {
        CancelRequest();
    }
}
