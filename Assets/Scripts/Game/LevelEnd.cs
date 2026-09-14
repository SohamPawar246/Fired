using UnityEngine;

/// <summary>
/// LEVEL DESIGNER COMPONENT — the finish line. Put it on a trigger collider
/// spanning the end of the lane; when the bullet flies through, the run ends
/// with a WIN (score + distance carried to the results screen).
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelEnd : MonoBehaviour
{
    private bool _done;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (_done) return;
        var bullet = other.GetComponentInParent<BulletController>();
        if (bullet == null) return;
        _done = true;
        FindFirstObjectByType<FiredGameController>()?.OnLevelComplete();
    }
}
