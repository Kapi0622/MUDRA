# 📋 開発ログ：α版 A5（Guarding・StatusEffect実装）

> **ドキュメント種別:** マイルストーン開発ログ
> **対象フェーズ:** α版 A5
> **期間:** 2026/07/17
> **ステータス:** ✅ 完了

---

## 1. マイルストーン概要

| 項目 | 内容 |
|------|------|
| **完了条件** | Fist文脈判定によるGuarding状態が機能し、Stun付与でボスのStunned状態が実際に駆動し、DoTのtick処理が毎秒ダメージを与え続ける |
| **開発方針** | StatusEffect基盤を先に構築し、DoT・Stunを接続した後、独立軸としてGuardingを実装する |
| **作業シーン** | A4から引き続き、MediaPipeサンプルシーンの複製を使用 |

---

## 2. 実装ファイル一覧

### Step 1: DamageResult拡張

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `DamageResult.cs` | 改修 | `PerTickDamage`・`TickCount`フィールド追加（DoT専用。他Strategyは0） |
| `DamageOverTimeCalculator.cs` | 改修 | return文で`PerTickDamage`・`TickCount`を`DamageResult`に格納 |
| `SingleHitCalculator.cs` | 改修 | `AppliedEffect`・`EffectDuration`をreturnに追加（StatusEffect基盤との接続用） |
| `MultiHitCalculator.cs` | 改修 | 同上 |

### Step 2: StatusEffect管理基盤

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `IStatusEffect.cs` | 新規作成 | 時限効果の共通インターフェース（OnApply/OnTick/OnExpire） |
| `DotEffect.cs` | 新規作成 | DoTのtick駆動ロジック。毎秒perTickDamageをBattleModelに適用 |
| `StunEffect.cs` | 新規作成 | Stun開始/終了をEnemyStateManagerに委譲 |
| `StatusEffectManager.cs` | 新規作成 | アクティブ効果のコレクション管理。同種重複は無視（案C） |
| `StatusEffectFactory.cs` | 新規作成 | DamageResultからIStatusEffectを生成するファクトリ |

### Step 3: DoT tick駆動の接続

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `BattleModel.cs` | 改修 | `ApplyDotDamage(int)`追加。`SetStatusEffectDependencies()`追加。`ApplySpellDamage`内でStatusEffect付与を実行 |
| `BattlePresenter.cs` | 改修 | `StatusEffectManager`受取・`Update()`でTick駆動 |
| `BattleInitializer.cs` | 改修 | StatusEffectManager・StatusEffectFactory生成・配線。バトル終了時のClearAll追加 |

### Step 4: EnemyStateManager Stunned実接続

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `EnemyStateManager.cs` | 改修 | `ApplyStun()`・`EndStun()`追加。`_patternIndex`インクリメントをAttacking直後に移動 |

### Step 5: Guarding（タイミングガード方式）

| ファイル | 変更種別 | 役割 |
|----------|----------|------|
| `HandSignEnum.cs` | 改修 | `Guard = 7`（捌印「盾」親指のみ伸ばし）追加 |
| `HandTrackingService.cs` | 改修 | DetectSignにGuardパターン追加 |
| `GuardWindowManager.cs` | 新規作成 | ガード受付窓（0.5秒）の管理。PlayerPhaseとは独立 |
| `SpellSequenceRunner.cs` | 改修 | Guard印の振り分け追加。`GuardWindowManager`注入 |
| `BattlePresenter.cs` | 改修 | `GuardWindowManager`受取・Tick駆動・`HandleEnemyAttack`でisGuarding参照 |
| `BattleInitializer.cs` | 改修 | `GuardWindowManager`生成・Presenter群への注入 |

### Step 6: バグ修正

| ファイル | 変更種別 | 内容 |
|----------|----------|------|
| `StatusEffectManager.cs` | 改修 | Tick()ループ中にClearAll()が呼ばれた場合のインデックス安全ガード追加 |

---

## 3. 設計判断の記録

### 3-1. StatusEffect基盤: 案Cの採用

StunとDoTの管理方式として3案を比較し、案C（折衷）を採用した。

- **案A（一元化）** — StatusEffectManagerが全効果を管理。EnemyStateManagerのStunnedは外部指示待ちの受け身に。管理は1箇所だが、Stunnedの駆動方式が他状態と非対称になる
- **案B（責務分割）** — 敵状態はEnemyStateManager、HP効果はStatusEffectManager。共通抽象が薄い
- **案C（折衷・採用）** — `IStatusEffect`は`duration管理 + OnApply/OnTick/OnExpire`フックだけ共通化。StunEffect.OnApply/OnExpireがEnemyStateManagerを叩き、DotEffect.OnTickがBattleModelを叩く

**判断根拠:** Stunの本質は「敵にかかった外部効果」であり、ライフサイクル管理を共通化しつつ、実際の作用先（EnemyStateManager / BattleModel）は各Effect内に委譲する構造が最も自然。将来のSlow追加時もIStatusEffect実装を足すだけで対応可能。

### 3-2. Stun割り込み: 選択肢C（Idle復帰）の採用

Stun付与時の敵行動ループの扱いとして3案を比較し、選択肢C（Idle復帰）を採用した。

- **選択肢A（完全復帰）** — Stun前のフェーズ+残り時間を保存し解除後再開。実装コスト最高
- **選択肢B（フェーズ復帰・時間リセット）** — フェーズは戻るが待機時間はやり直し。中程度
- **選択肢C（Idle復帰・採用）** — Stun解除後は常にIdleに戻りループ再開。最もシンプル

**判断根拠:** 実装コストが低く、将来の完封対策（免疫期間・効果時間逓減・確率化）への拡張性も十分。Stun完封対策はB3（バランス調整）に延期。

### 3-3. _patternIndex進行タイミングの移動

`RunLoopAsync`のパターン進行を「intervalAfter完了後」から「Attacking突入直後」に移動した。

**理由:** 攻撃が確定した後のIdle待機中にStunが入ると、_patternIndexが未進行のまま残り、Stun解除後に同じ攻撃がもう一度Chargingから実行される直感に反する挙動が発生するため。攻撃確定時点でインデックスを進めることで、「まだ実行していないアクションから再開する」一貫した挙動を保証する。

### 3-4. 同種効果の重複: 案C（無視）の採用

- **案A（上書き）** — 既存効果を消して新効果に差し替え。タイマーリセット
- **案B（延長）** — 残り時間に加算。永久Stun問題が起きやすい
- **案C（無視・採用）** — 同種効果がアクティブなら新規付与を無視

**判断根拠:** A5時点では最もシンプル。B3で必要に応じて上書きや耐性に変更可能。

### 3-5. Guarding仕様の方向転換: タイミングガード方式

仕様書4-2の「Idle中にFist保持→Guarding状態遷移」を廃止し、タイミングガード方式に変更した。

**変更前（仕様書4-2）:**
- Idle中にFist保持→Guarding状態（PlayerPhaseの一部）
- Chanting中のFistはシーケンスの一部として扱う（Fist文脈判定）
- Guardingは持続状態（解除検出が必要）

**変更後（A5実装）:**
- 専用Guard印（捌印「盾」Guard = 7、親指のみ伸ばし）の確定で0.5秒の受付窓が開く
- PlayerPhaseとは独立した時限窓として管理（`GuardWindowManager`）
- 詠唱中でもガードが割り込み可能（シーケンスに影響しない）
- 受付窓は自動終了（解除検出不要）

**判断根拠:**
- **ゲーム性:** 持続ガードだと大技の前にずっとガード待機が容易で単調。タイミングガードなら攻撃予告を見てタイミングを合わせる攻略感が生まれる
- **技術的利点:** Fist文脈判定（Idle中かChanting中かの分岐）とGuarding解除検出が不要になり、実装が大幅に簡素化。既存のStateパターンを変更せずに済む
- **拡張性:** 将来的にパリィシステム（ジャストガードで反撃やスタン付与）への発展パスが開ける
- `PlayerPhase.Guarding`は仕様書から削除

### 3-6. Guard印の選択: 親指のみ伸ばし

既存のFist（全指曲げ）と親指1ビットだけ異なるパターン`[O, X, X, X, X]`を採用。A3で確認済みの親指判定マージン（14°以上）により、FistとGuardの誤認識リスクは十分に低い。全8種のパターンが衝突なく一意に識別可能。将来の両手印対応時に再検討の可能性あり（仮決定）。

### 3-7. GuardingをPlayerPhaseから外す設計

タイミングガードは「0.5秒間のダメージ軽減窓が開く」時限効果であり、Chanting等の排他的なPlayerPhaseと同時に成立しうる。PlayerPhase（排他的状態遷移）に組み込むとChanting+Guardingの同時成立が矛盾するため、独立した`GuardWindowManager`として管理する方向A（Guardingを状態から外す）を採用した。

### 3-8. BattleModelへのStatusEffect注入: ポストコンストラクション方式

`StatusEffectFactory`は`BattleModel.ApplyDotDamage`と`EnemyStateManager.ApplyStun/EndStun`のデリゲートを必要とするため、両インスタンスが揃った後に生成する必要がある。`BattleModel`のコンストラクタ時点ではファクトリを渡せない（循環）ため、`SetStatusEffectDependencies()`によるポストコンストラクション注入を採用。`BattleInitializer`のみが呼ぶ前提で、設計上の逸脱を限定的に許容する。

---

## 4. 統合テスト結果

全15項目パス。

### Guard

| # | テスト項目 | 結果 |
|---|-----------|------|
| ① | Guard印（親指のみ伸ばし）が認識され、ログが出る | ✅ |
| ② | ガード受付中に通常攻撃を受けるとダメージが軽減される（0.5倍） | ✅ |
| ③ | ガード受付窓（0.5秒）が切れた後は通常ダメージに戻る | ✅ |
| ④ | 詠唱中にGuard印を挟んでもシーケンスが壊れない | ✅ |
| ⑤ | Guard印とFistが混同しない | ✅ |

### StatusEffect（Stun）

| # | テスト項目 | 結果 |
|---|-----------|------|
| ⑥ | 雷連撃でStunが付与され、ボスがStunned状態になる | ✅ |
| ⑦ | Stun効果時間経過後にボスがIdle復帰しループ再開する | ✅ |
| ⑧ | Stun中に雷連撃を再度打っても重複付与されない | ✅ |

### StatusEffect（DoT）

| # | テスト項目 | 結果 |
|---|-----------|------|
| ⑨ | 火炎弾（DoT設定済み）で初撃ダメージがボスに入る | ✅ |
| ⑩ | 初撃後、毎秒tickダメージでボスHPが減少し続ける | ✅ |
| ⑪ | DoT効果時間終了後にtickダメージが止まる | ✅ |

### 既存機能の退行確認

| # | テスト項目 | 結果 |
|---|-----------|------|
| ⑫ | 風刃（SingleHit）が従来通り動作する | ✅ |
| ⑬ | 雷連撃のMultiHitダメージが正しく入る | ✅ |
| ⑭ | 暴発でセルフダメージ+コンボリセットが動作する | ✅ |
| ⑮ | ボスHP 0で勝利、プレイヤーHP 0で敗北が正しく判定される | ✅ |

---

## 5. フォルダ構成（A5時点）

```
Scripts/
├── Data/
├── Debug/
├── HandTracking/
├── Input/
├── Model/
│   ├── State/
│   ├── Strategy/
│   ├── StatusEffect/        ← A5新設
│   │   ├── IStatusEffect.cs
│   │   ├── DotEffect.cs
│   │   ├── StunEffect.cs
│   │   ├── StatusEffectManager.cs
│   │   └── StatusEffectFactory.cs
│   ├── BattleModel.cs
│   ├── EnemyStateManager.cs
│   ├── GuardWindowManager.cs  ← A5新設
│   └── PlayerStateManager.cs
├── Presenter/
└── View/
```

---

## 6. 技術的負債・保留事項

| 負債 | 対処方針 |
|------|----------|
| `SpellSequenceRunner`→`HandSignPresenter`リネーム | A1から継続。β版で整理 |
| `ResolveCalculator`が毎回newしている | A2から継続。ステートレスなので実害なし |
| MultiHitのView側時間差演出 | A4ストレッチ未着手。β版で対応 |
| Stun完封対策（免疫期間 or 逓減） | B3（バランス調整）で対応 |
| Guard印の手形は仮決定 | 両手印対応時に再検討 |
| `GuardWindowManager.WindowDuration`のSO化 | β版でバランス調整時に検討 |
| `DotEffect.TickInterval`と`DamageOverTimeCalculator.TickInterval`の二重定義 | 実害なし。定数が増えた場合に共通化を検討 |
| 仕様書更新（A5実装内容の反映） | dev_log_A5完了後に実施 |

---

## 7. 仕様書更新対象リスト

A5の実装に伴い、以下の仕様書セクションを更新する必要がある。

| セクション | 更新内容 |
|------------|----------|
| 2-1 クラス一覧 Model Layer | `GuardWindowManager`追加 |
| 2-1 クラス一覧 StatusEffect（新設） | `IStatusEffect`・`DotEffect`・`StunEffect`・`StatusEffectManager`・`StatusEffectFactory`追加 |
| 3-1 HandSign enum | `Guard = 7`追加。パターン表に`Guard: [O, X, X, X, X]`追加 |
| 3-1 PlayerPhase enum | `Guarding`を削除 |
| 3-3 DamageResult | `PerTickDamage`・`TickCount`フィールド追加 |
| 4-2 PlayerStateManager状態遷移 | Guarding状態を削除。タイミングガード方式の説明を追記 |
| 4-3 EnemyStateManager状態遷移 | Stunnedへの遷移を「任意フェーズからの割り込み」に更新。`ApplyStun`/`EndStun`の記述追加 |
| 5-2 手印識別パターン表 | Guardパターン追加 |
| 7-1 BattleModelメソッド | `ApplyDotDamage`・`SetStatusEffectDependencies`追加 |
| 7-2 DamageOverTimeCalculator | tick情報がDamageResultに格納される旨を更新（A3時点の「未反映」注記を削除） |
| A3 SpellDataアセット表 | 火炎弾のdamageType変更を反映 |

---

## 8. β版への引継ぎ

**α版完了条件の達成状況:** 1体のボスと最後まで戦えるバトルが成立する → ✅

### 前提として把握しておくべきこと

- Guard印（捌印「盾」Guard = 7）はPlayerPhaseと独立した0.5秒の受付窓で動作する。詠唱中にも割り込み可能
- StatusEffect基盤は`IStatusEffect`の共通ライフサイクルフックで拡張可能。Slow追加時は`SlowEffect`実装+ファクトリ分岐追加のみ
- DoTは初撃（速度・コンボ倍率あり）+毎秒tick（弱点倍率のみ）の2段構成
- Stun解除後はIdle復帰→ループ再開（攻撃確定済みのアクションから次に進む）
- 同種StatusEffectの重複は無視（A5方針）。B3で上書きや耐性に変更可能

### β版で新たに必要になるもの

- B1: 全ステージ実装（4ステージ分のボス戦。EnemyData/StageDataアセット追加）
- B2: チュートリアル・キャリブレーション（初見プレイヤー向けフロー整備）
- B3: バランス調整（Stun完封対策、Guard窓のSO化、全ステージクリア難易度）
- B4: バグフィックス・最終調整（SE・BGM実装、クリティカルバグ修正）

### B1の実働日数について

A5がA3から切り出されて新設された工程のため、β版の開始が当初予定から遅延している可能性がある。B1の実働日数とスケジュール全体の再調整を検討する必要がある。
