using UnityEngine;

namespace MUDRA.Data
{
    /// <summary>
    /// 演出の時間表。複数のクラスにまたがる尺だけをここに集める。
    ///
    /// ボスの登場・撃破は複数のView（暗転・敵本体・カメラ・ボス名・決着表示）にまたがって決まった順番で起きる。
    /// Presenterにタイマーを持たせない（ロジックを持たない規約）ため、各Viewは「開始までの遅延」を受け取り、
    /// その秒数をここに集約して、順番の食い違いが起きないようにしている。
    ///
    /// また、Model（SectionProgressManager）はViewを知らず演出の完了を待てないため、
    /// 「演出の尺の合計＋余白」だけ待つ。この待機時間は尺から計算するプロパティにしてあり、
    /// 尺を変えれば自動で追従する（以前はModelとViewのconstをコメントで揃えていた）。
    ///
    /// ボスの登場シーケンス（0.0 = ボスのセクション開始。初期値の場合）
    ///   0.0         暗転 ＋「強敵出現」の帯
    ///   0.6〜1.4    影（黒）の状態でフェードイン
    ///   1.6〜2.0    影から本来の色に戻る
    ///   2.0         咆哮（カメラ大揺れ ＋ ボス名）
    ///   2.0〜2.4    暗転が明ける
    ///
    /// 各View固有の振幅・倍率などはここに置かず、それぞれのViewのconstに残している。
    /// </summary>
    [CreateAssetMenu(fileName = "PT_Battle", menuName = "MUDRA/Presentation Timing")]
    public class PresentationTimingData : ScriptableObject
    {
        [Header("雑魚")]
        [Tooltip("画面右外から滑り込む登場の長さ")]
        public float normalAppearDuration = 0.6f;

        [Tooltip("撃破演出の前半（点滅）の長さ")]
        public float normalDefeatBlinkDuration = 0.4f;

        [Tooltip("撃破演出の後半（縮みながら消える）の長さ")]
        public float normalDefeatVanishDuration = 0.4f;

        [Header("前進")]
        [Tooltip("撃破後の前進演出（ズーム＋暗転→明転）の長さ。雑魚の撃破演出が終わってから始まる")]
        public float advanceDuration = 1.2f;

        [Header("ボス登場（セクション開始からの秒数）")]
        [Tooltip("暗転が入りきるまでの長さ")]
        public float dimFadeInDuration = 0.3f;

        [Tooltip("「強敵出現」の帯が中央まで入る長さ")]
        public float bandInDuration = 0.3f;

        [Tooltip("帯が右へ抜け始める時刻")]
        public float bandOutStart = 1.3f;

        [Tooltip("帯が抜けきるまでの長さ")]
        public float bandOutDuration = 0.3f;

        [Tooltip("影（黒）の状態でフェードインし始める時刻")]
        public float silhouetteStart = 0.6f;

        [Tooltip("影のフェードインの長さ")]
        public float silhouetteFadeDuration = 0.8f;

        [Tooltip("影から本来の色に戻り始める時刻")]
        public float revealStart = 1.6f;

        [Tooltip("本来の色に戻るまでの長さ")]
        public float revealDuration = 0.4f;

        [Tooltip("咆哮（カメラ大揺れ＋ボス名）の時刻。暗転はここから明け始める")]
        public float roar = 2.0f;

        [Tooltip("暗転が明けきるまでの長さ")]
        public float dimFadeOutDuration = 0.4f;

        [Header("ボス撃破")]
        [Tooltip("ヒットストップの長さ（実時間）。この間timeScaleを0にする")]
        public float hitStopRealtime = 0.2f;

        [Tooltip("撃破演出の前半（点滅）の長さ（ゲーム内時間）")]
        public float bossDefeatBlinkDuration = 1.0f;

        [Tooltip("撃破演出の後半（爆散して消える）の長さ（ゲーム内時間）")]
        public float bossDefeatVanishDuration = 1.0f;

        [Header("Modelの待機に足す余白")]
        [Tooltip("雑魚の登場が終わってから1手目の予告までの間")]
        [Min(0f)] public float normalEntryPadding = 0.4f;

        [Tooltip("前進演出が終わってから次の敵が出るまでの間")]
        [Min(0f)] public float transitionPadding = 0.5f;

        [Tooltip("ボスの登場シーケンスが終わってから1手目の予告までの間")]
        [Min(0f)] public float bossEntryPadding = 0.6f;

        /// <summary>雑魚の撃破演出全体の長さ。前進演出はこの後に始まる</summary>
        public float NormalDefeatDuration => normalDefeatBlinkDuration + normalDefeatVanishDuration;

        /// <summary>ボスの登場シーケンス全体の長さ（暗転が明けきるまで）</summary>
        public float EncounterDuration => roar + dimFadeOutDuration;

        /// <summary>ボスの撃破演出全体の長さ（ゲーム内時間）。「討伐」の表示をこの後に遅らせる</summary>
        public float BossDefeatDuration => bossDefeatBlinkDuration + bossDefeatVanishDuration;

        /// <summary>雑魚の出現から1手目の予告に入るまでの待機。登場演出の間に予告が始まらないようにする</summary>
        public float NormalEntryDelay => normalAppearDuration + normalEntryPadding;

        /// <summary>ボスの出現から1手目の予告に入るまでの待機。登場シーケンスの間に予告が始まらないようにする</summary>
        public float BossEntryDelay => EncounterDuration + bossEntryPadding;

        /// <summary>敵の撃破から次の敵が出現するまでの待機。この間にViewが撃破演出→前進演出を流す</summary>
        public float TransitionDuration => NormalDefeatDuration + advanceDuration + transitionPadding;

#if UNITY_EDITOR
        /// <summary>
        /// ボス登場の順番が崩れる値を入れたら警告する。
        /// Modelの待機は余白を0以上に制限しているので、演出より短くなることはない。
        /// </summary>
        private void OnValidate()
        {
            if (silhouetteStart + silhouetteFadeDuration > revealStart)
                Debug.LogWarning($"[{name}] 影のフェードインが終わる前に色が戻り始めます（silhouetteStart + silhouetteFadeDuration > revealStart）", this);
            if (revealStart + revealDuration > roar)
                Debug.LogWarning($"[{name}] 色が戻りきる前に咆哮します（revealStart + revealDuration > roar）", this);
            if (bandInDuration > bandOutStart)
                Debug.LogWarning($"[{name}] 帯が入りきる前に抜け始めます（bandInDuration > bandOutStart）", this);
            if (dimFadeInDuration > roar)
                Debug.LogWarning($"[{name}] 暗転が入りきる前に咆哮します（dimFadeInDuration > roar）", this);
        }
#endif
    }
}
