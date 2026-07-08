using MUDRA.HandTracking;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private MediaPipeHandLandmarkProvider _provider;
    
    private void Update()
    {
        Debug.Log($"HandCount: {_provider.DetectedHandCount}, LandmarkCount: {_provider.GetCurrentLandmarks().Count}");
    }
}
