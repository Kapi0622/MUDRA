#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// デバッグ用Runtime Overlayメニュー。
/// F1キーでパネルの表示/非表示をトグルする。
/// BattleInitializerからInjectで依存を受け取る。
/// </summary>
public class DebugMenuView : MonoBehaviour
{
    private BattleModel _battleModel;
    private StatusEffectManager _statusEffectManager;
    private EnemyStateManager _enemyStateManager;

    private bool _isVisible;
    private bool _isInvincible;
    private Rect _windowRect = new(10, 10, 600, 700);

    // --- FPS計測用 ---
    private float _deltaTime;

    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _toggleStyle; 
    private bool _stylesInitialized;
    
    /// <summary>
    /// BattleInitializerから呼ばれる。デバッグ対象のModel群を受け取る。
    /// </summary>
    public void Inject(
        BattleModel battleModel,
        StatusEffectManager statusEffectManager,
        EnemyStateManager enemyStateManager)
    {
        _battleModel = battleModel;
        _statusEffectManager = statusEffectManager;
        _enemyStateManager = enemyStateManager;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            _isVisible = !_isVisible;

        // FPS用のdeltaTime平滑化
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 32,
            fixedHeight = 48
        };
        
        _toggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold
        };

        _stylesInitialized = true;
    }
    
    private void OnGUI()
    {
        InitStyles();
        
        // --- FPS表示（メニュー非表示でも常時表示） ---
        DrawFps();

        if (!_isVisible) return;

        _windowRect = GUI.Window(0, _windowRect, DrawWindowContents, "Debug Menu (F1)");
    }

    // ============================
    // FPS表示（画面右上に常時表示）
    // ============================
    private void DrawFps()
    {
        float fps = 1.0f / _deltaTime;
        string text = $"FPS: {fps:F0}";

        // 右上に配置
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };

        // FPS値に応じて色を変える
        style.normal.textColor = fps switch
        {
            >= 55f => Color.green,
            >= 30f => Color.yellow,
            _ => Color.red
        };

        GUI.Label(new Rect(Screen.width - 220, 10, 200, 50), text, style);
    }

    // ============================
    // メニュー描画
    // ============================
    private void DrawWindowContents(int windowId)
    {
        if (_battleModel == null)
        {
            GUILayout.Label("BattleModel未注入");
            GUI.DragWindow();
            return;
        }

        // --- Player HP ---
        GUILayout.Label($"--- Player HP: {_battleModel.PlayerHp.CurrentValue} / {_battleModel.PlayerMaxHp} ---", _labelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10", _buttonStyle))  _battleModel.DebugModifyPlayerHp(-10);
        if (GUILayout.Button("-50", _buttonStyle))  _battleModel.DebugModifyPlayerHp(-50);
        if (GUILayout.Button("+50", _buttonStyle))  _battleModel.DebugModifyPlayerHp(50);
        if (GUILayout.Button("全回復", _buttonStyle)) _battleModel.DebugModifyPlayerHp(_battleModel.PlayerMaxHp);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- Boss HP ---
        GUILayout.Label($"--- Boss HP: {_battleModel.BossHp.CurrentValue} / {_battleModel.BossMaxHp} ---", _labelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10", _buttonStyle))  _battleModel.DebugModifyBossHp(-10);
        if (GUILayout.Button("-50", _buttonStyle))  _battleModel.DebugModifyBossHp(-50);
        if (GUILayout.Button("+50", _buttonStyle))  _battleModel.DebugModifyBossHp(50);
        if (GUILayout.Button("瀕死", _buttonStyle)) _battleModel.DebugModifyBossHp(-_battleModel.BossMaxHp + 1);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- 無敵モード ---
        _isInvincible = GUILayout.Toggle(_isInvincible, "無敵モード", _toggleStyle);
        _battleModel.IsInvincible = _isInvincible;

        GUILayout.Space(8);

        // --- StatusEffect ---
        GUILayout.Label("--- StatusEffect ---", _labelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stun付与", _buttonStyle))
        {
            if (_statusEffectManager != null)
            {
                var stun = new StunEffect(3.0f, _enemyStateManager.ApplyStun, _enemyStateManager.EndStun);
                _statusEffectManager.ApplyEffect(stun);
                Debug.Log("[DebugMenu] Stun付与 (3秒)");
            }
        }
        if (GUILayout.Button("DoT付与", _buttonStyle))
        {
            if (_statusEffectManager != null)
            {
                var dot = new DotEffect(5.0f, 5, _battleModel.ApplyDotDamage);
                _statusEffectManager.ApplyEffect(dot);
                Debug.Log("[DebugMenu] DoT付与 (5dmg × 5秒)");
            }
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("全Effect解除", _buttonStyle))
        {
            _statusEffectManager?.ClearAll();
            Debug.Log("[DebugMenu] 全StatusEffect解除");
        }

        GUILayout.Space(8);

        // --- ステージ ---
        GUILayout.Label("--- Stage ---", _labelStyle);

        if (GUILayout.Button("シーンリロード (R)", _buttonStyle))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        GUILayout.Space(4);
        GUILayout.Label("※ステージジャンプはB3で実装");

        GUI.DragWindow();
    }
}

#endif