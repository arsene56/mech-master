namespace MechMaster.Runtime
{
    public interface IMechanicalMotionController
    {
        bool IsReady { get; }
        bool IsActive { get; }
        bool IsPlaying { get; }
        int Speed { get; }
        void SetSpeed(int value);
        void Play();
        void Pause();
        void Stop();
    }
}
