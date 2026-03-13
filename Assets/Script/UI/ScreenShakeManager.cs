using UnityEngine;
using Unity.Cinemachine; // Required for screen shake

[RequireComponent(typeof(CinemachineImpulseSource))]
public class ScreenShakeManager : MonoBehaviour
{
    public static ScreenShakeManager Instance { get; private set; }

    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public void ShakeScreen(float intensity = 1f)
    {
        _impulseSource.GenerateImpulseWithVelocity(Random.insideUnitCircle * intensity);
    }
}