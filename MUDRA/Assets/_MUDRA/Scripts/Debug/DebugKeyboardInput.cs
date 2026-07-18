#if UNITY_EDITOR || DEVELOPMENT_BUILD

using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// デバッグ用キーボード手印シミュレーション。
/// キー押下で即座に HandSign を発火し、カメラなしでバトル全機能をテスト可能にする。
/// UNITY_EDITOR または DEVELOPMENT_BUILD でのみコンパイルされる。
/// </summary>
public class DebugKeyboardInput : MonoBehaviour
{
    // HandSignPresenter が Subscribe する Observable
    private readonly Subject<HandSign> _onHandSignInput = new();
    public Observable<HandSign> OnHandSignInput => _onHandSignInput;

    private void Update()
    {
        // 詠唱印（1〜5キー）
        if (Input.GetKeyDown(KeyCode.Alpha1)) _onHandSignInput.OnNext(HandSign.Open);
        if (Input.GetKeyDown(KeyCode.Alpha2)) _onHandSignInput.OnNext(HandSign.Fist);
        if (Input.GetKeyDown(KeyCode.Alpha3)) _onHandSignInput.OnNext(HandSign.Point);
        if (Input.GetKeyDown(KeyCode.Alpha4)) _onHandSignInput.OnNext(HandSign.Scissors);
        if (Input.GetKeyDown(KeyCode.Alpha5)) _onHandSignInput.OnNext(HandSign.Palm);

        // 特殊印
        if (Input.GetKeyDown(KeyCode.Space))      _onHandSignInput.OnNext(HandSign.Release);
        if (Input.GetKeyDown(KeyCode.Backspace))  _onHandSignInput.OnNext(HandSign.Cancel);
        if (Input.GetKeyDown(KeyCode.G))          _onHandSignInput.OnNext(HandSign.Guard);
        
        // --- デバッグリセット ---
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[DebugMode] シーンリロード");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnDestroy()
    {
        _onHandSignInput.Dispose();
    }
}

#endif