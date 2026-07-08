using UnityEngine;
using MUDRA.HandTracking;
using R3;

public sealed class TempHandTrackingRunner : MonoBehaviour
{
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;
    private HandTrackingService _service;
    private readonly CompositeDisposable _disposables = new();

    private void Start()
    {
        _service = new HandTrackingService(_provider);
        _service.OnHandSignRecognized
            .Subscribe(sign => Debug.Log($"確定: {sign}"))
            .AddTo(_disposables);
    }

    private void Update()
    {
        _service.Tick();
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
        _service.Dispose();
    }
}