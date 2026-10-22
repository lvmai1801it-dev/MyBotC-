using MyBot.Game.CoC.Enums;

namespace MyBot.Game.CoC.Math;

/// <summary>Decides the base type from collector, storage, and town-hall signals.</summary>
public static class BaseClassifier
{
    private const int DeadBaseCollectorThreshold = 5;

    /// <summary>Classifies a base using the detection signals gathered from the battle frame.</summary>
    public static BaseType Classify(int uniqueCollectorCount, bool hasFullStorage, bool isTownHallOutside)
    {
        if (isTownHallOutside)
        {
            return BaseType.THOutside;
        }

        if (uniqueCollectorCount >= DeadBaseCollectorThreshold && !hasFullStorage)
        {
            return BaseType.DeadBase;
        }

        if (uniqueCollectorCount < DeadBaseCollectorThreshold && hasFullStorage)
        {
            return BaseType.LiveBase;
        }

        if (uniqueCollectorCount == 0 && !hasFullStorage)
        {
            return BaseType.ProgressBase;
        }

        return BaseType.Unknown;
    }
}
