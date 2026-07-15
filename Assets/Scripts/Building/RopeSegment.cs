// ── Rope / Zipline ────────────────────────────────────────────────────────
using System.Collections;
using TheForest.Building.Core;
using TheForest.Building.Events;
using UnityEngine;

public class RopeSegment : MonoBehaviour
{
    [SerializeField] private Vector3 anchorA;
    [SerializeField] private Vector3 anchorB;
    [SerializeField] private bool isBridge;
    [SerializeField] private bool isZipline;

    private LineRenderer _line;

    public bool IsZipline => isZipline;
    public Vector3 AnchorA => anchorA;
    public Vector3 AnchorB => anchorB;

    public void Initialize(Vector3 a, Vector3 b, bool bridge, bool zipline)
    {
        anchorA = a; anchorB = b;
        isBridge = bridge; isZipline = zipline;

        _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.SetPosition(0, a);
        _line.SetPosition(1, b);
        _line.startWidth = 0.05f;
        _line.endWidth = 0.05f;

        EventBus<RopeAttachedEvent>.Raise(new RopeAttachedEvent(a, b, bridge));
    }

    /// <summary>Slide a rider from anchorA to anchorB over duration.</summary>
    public Coroutine StartZiplineRide(MonoBehaviour runner, Transform rider,
                                      CharacterController cc, float speed = 8f)
        => runner.StartCoroutine(ZiplineRoutine(rider, cc, speed));

    private IEnumerator ZiplineRoutine(Transform rider, CharacterController cc, float speed)
    {
        EventBus<ZiplineRideEvent>.Raise(new ZiplineRideEvent(anchorA, anchorB, true));

        if (cc != null) cc.enabled = false;

        Vector3 dir = (anchorB - anchorA).normalized;
        float dist = Vector3.Distance(anchorA, anchorB);
        float t = 0f;

        while (t < dist)
        {
            t += speed * Time.deltaTime;
            rider.position = anchorA + dir * t;
            yield return null;
        }

        rider.position = anchorB;
        if (cc != null) cc.enabled = true;

        EventBus<ZiplineRideEvent>.Raise(new ZiplineRideEvent(anchorA, anchorB, false));
    }
}