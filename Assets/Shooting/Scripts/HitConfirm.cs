using System;

public static class HitConfirm
{
    public static Action OnHit; // UI bundan dinler

    public static void Raise()
    {
        OnHit?.Invoke();
    }
}
