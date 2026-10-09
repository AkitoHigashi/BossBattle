public interface IMotionSystem
{
    void EnterMotion(MotionData motionData);
    void EnterMotion(MotionContext motionContext);
    void ExitMotion();
    bool GetIsMotionActive();
    void SetIsMotionActive(bool isActive);
}
