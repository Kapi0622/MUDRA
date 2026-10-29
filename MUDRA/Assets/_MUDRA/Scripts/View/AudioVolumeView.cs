using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 音量の設定を AudioMixer に反映する（B7）。DisplayModeView と同じ「設定値の反映役」。App の Audio に置く。
///
/// BgmPlayer / SePlayer の AudioSource は MX_Main の BGM / SE グループへ出力するので、
/// ここで書き換えるのはグループの音量だけでよい（InGame の SePlayer にも同じように効く）。
/// Mixer 側で Volume を BgmVolume / SeVolume の名前で公開しておくこと。
/// </summary>
public class AudioVolumeView : MonoBehaviour
{
    private const string BgmVolumeParameter = "BgmVolume";
    private const string SeVolumeParameter = "SeVolume";
    /// <summary>Mixer の音量の下限（無音）</summary>
    private const float MinDecibel = -80f;
    private const float DecibelPerLog10 = 20f;

    [SerializeField] private AudioMixer _mixer;

    // 起動直後（Awake の中）の SetFloat は Mixer に無視されることがあるため、値を覚えておいて Start でもう一度書く
    private float _bgmVolume = 1f;
    private float _seVolume = 1f;

    /// <summary>BGMの音量（0〜1）</summary>
    public void SetBgmVolume(float volume)
    {
        _bgmVolume = volume;
        Apply(BgmVolumeParameter, volume);
    }

    /// <summary>効果音の音量（0〜1）</summary>
    public void SetSeVolume(float volume)
    {
        _seVolume = volume;
        Apply(SeVolumeParameter, volume);
    }

    private void Start()
    {
        Apply(BgmVolumeParameter, _bgmVolume);
        Apply(SeVolumeParameter, _seVolume);
    }

    private void Apply(string parameter, float volume)
    {
        _mixer.SetFloat(parameter, ToDecibel(volume));
    }

    /// <summary>0〜1 の音量を Mixer のデシベルへ。耳の感じ方に合わせて対数にする。0 は無音</summary>
    private static float ToDecibel(float volume)
    {
        return volume > 0f ? Mathf.Max(MinDecibel, DecibelPerLog10 * Mathf.Log10(volume)) : MinDecibel;
    }
}
