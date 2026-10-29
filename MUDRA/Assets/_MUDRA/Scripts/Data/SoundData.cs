using UnityEngine;
using UnityEngine.Audio;

namespace MUDRA.Data
{
    /// <summary>
    /// 固定の音（術・敵の攻撃・ステージのBGM以外）をまとめた定義データ（B7）。
    /// App と InGame の両方から同じアセット（SND_Sound）を参照する。
    ///
    /// 効果音は AudioResource 型にしている。AudioClip（wav をそのまま）と Audio Random Container
    /// （複数の音から選ぶ・ピッチと音量をばらつかせる）のどちらも入れられ、コードはどちらでも変わらない。
    /// 音量もここに入れる素材の側（Audio Random Container の Volume）で調整し、コードには音量の値を持たない。
    ///
    /// 未設定の欄は鳴らさないだけ（SePlayer / BgmPlayer が null を無視する）。
    /// 各欄の素材の目安は docs/sound_asset_list.md。Tooltip の名前はそこでのファイル名。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSound", menuName = "MUDRA/Sound Data")]
    public class SoundData : ScriptableObject
    {
        [Header("BGM・ジングル（ステージの道中・ボスの曲は StageData に持つ）")]
        [Tooltip("BGM_Title。タイトル・キャリブレーション・ステージ選択")]
        public AudioClip titleBgm;

        [Tooltip("BGM_Result。リザルト画面")]
        public AudioClip resultBgm;

        [Tooltip("JGL_Victory。「討伐」の表示と同時に1回だけ鳴らす")]
        public AudioClip victoryJingle;

        [Tooltip("JGL_Defeat。「敗北」の表示と同時に1回だけ鳴らす")]
        public AudioClip defeatJingle;

        [Header("手印・術（術ごとの発動音は SpellData.castSE）")]
        [Tooltip("SE_SignConfirm。詠唱印の確定。印の数に応じて再生側でピッチを上げる")]
        public AudioResource signConfirm;

        [Tooltip("SE_CastCommon。術の発動（全術共通。castSE に重ねる）")]
        public AudioResource castCommon;

        [Tooltip("SE_Misfire。暴発")]
        public AudioResource misfire;

        [Tooltip("SE_SignCancel。解除印")]
        public AudioResource signCancel;

        [Tooltip("SE_GuardStance。ガードの構え")]
        public AudioResource guardStance;

        [Tooltip("SE_GuardSuccess。ガード成功")]
        public AudioResource guardSuccess;

        [Header("命中・状態")]
        [Tooltip("SE_Hit。術の命中（MultiHit はヒット数ぶん鳴らす）")]
        public AudioResource hit;

        [Tooltip("SE_HitWeakness。弱点命中（hit に重ねる）")]
        public AudioResource hitWeakness;

        [Tooltip("SE_SpeedBonus。迅速ボーナス")]
        public AudioResource speedBonus;

        [Tooltip("SE_ComboUp。コンボ加算（2コンボ以上）")]
        public AudioResource comboUp;

        [Tooltip("SE_ComboBreak。コンボが途切れた")]
        public AudioResource comboBreak;

        [Tooltip("SE_DotTick。燃焼の継続ダメージ（毎秒）")]
        public AudioResource dotTick;

        [Tooltip("SE_HealTick。継続回復（毎秒）")]
        public AudioResource healTick;

        [Tooltip("SE_Stun。敵のスタン")]
        public AudioResource stun;

        [Header("敵（攻撃音は EnemyAttackData.attackSE）")]
        [Tooltip("SE_EnemyCharge。攻撃の溜め。溜めが終わったら途中で止める")]
        public AudioResource enemyCharge;

        [Tooltip("SE_HeavyWarning。ボスの大技予告（enemyCharge に重ねる）")]
        public AudioResource heavyWarning;

        [Tooltip("SE_PlayerDamage。被弾")]
        public AudioResource playerDamage;

        [Tooltip("SE_PlayerDamageHeavy。大技の被弾")]
        public AudioResource playerDamageHeavy;

        [Tooltip("SE_EnemyAppear。雑魚の登場")]
        public AudioResource enemyAppear;

        [Tooltip("SE_BossAppear。ボスの登場演出")]
        public AudioResource bossAppear;

        [Tooltip("SE_BossRoar。ボスの咆哮（登場演出の最後）")]
        public AudioResource bossRoar;

        [Tooltip("SE_EnemyDefeat。雑魚の撃破")]
        public AudioResource enemyDefeat;

        [Tooltip("SE_BossDefeat。ボスの撃破。2秒後に勝利ジングルが鳴るので、2秒以内に収まる素材にする")]
        public AudioResource bossDefeat;

        [Tooltip("SE_Advance。次の敵へ前進")]
        public AudioResource advance;

        [Header("ステージの流れ・ポーズ")]
        [Tooltip("SE_StageIntro。ステージ開始の帯")]
        public AudioResource stageIntro;

        [Tooltip("SE_Ketsuin。「結印！」（ステージ開始・ポーズ明け）")]
        public AudioResource ketsuin;

        [Tooltip("VO_Ketsuin。「結印！」の掛け声（ketsuin に重ねる）")]
        public AudioResource ketsuinVoice;

        [Tooltip("SE_Pause。ポーズの開閉")]
        public AudioResource pause;

        [Tooltip("SE_CountdownTick。再開のカウントダウン")]
        public AudioResource countdownTick;

        [Header("画面・UI")]
        [Tooltip("SE_Transition。墨のワイプ")]
        public AudioResource transition;

        [Tooltip("SE_UiDecide。決定")]
        public AudioResource uiDecide;

        [Tooltip("SE_UiBack。戻る・閉じる")]
        public AudioResource uiBack;

        [Tooltip("SE_UiCursor。カーソル移動")]
        public AudioResource uiCursor;

        [Tooltip("SE_RankStamp。リザルトのランクのハンコ")]
        public AudioResource rankStamp;

        [Tooltip("SE_NewRecord。自己ベスト更新")]
        public AudioResource newRecord;

        [Tooltip("SE_UiToggle。設定の切り替え")]
        public AudioResource uiToggle;
    }
}
