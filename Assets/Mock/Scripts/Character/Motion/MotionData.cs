using UnityEngine;

[System.Serializable]
public struct MotionData
{
    public MotionData(
        string motionId,
        float duration,
        bool canCancel,
        MotionExecutor executor,
        string animatorTriggerName)
    {
        _motionId = motionId;
        _duration = Mathf.Max(0f, duration);
        _canCancel = canCancel;
        _executor = executor;
        _animatorTriggerName = animatorTriggerName;
    }

    public string MotionId => _motionId;
    public float Duration => Mathf.Max(0f, _duration);
    public bool CanCancel => _canCancel;
    public MotionExecutor Executor => _executor;
    public string AnimatorTriggerName => _animatorTriggerName;

    [SerializeField] private string _motionId;
    [SerializeField, Min(0f)] private float _duration;
    [SerializeField] private bool _canCancel;
    [SerializeField] private MotionExecutor _executor;
    [SerializeField] private string _animatorTriggerName;
}
