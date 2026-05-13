using System;

namespace MyGame
{
    public readonly record struct TankStats(int Damage, int Defense, float MoveSecondsPerCell)
    {
        // Возвращает характеристики танка по уровню
        public static TankStats ForLevel(TankLevel level) =>
            level switch
            {
                TankLevel.Level1 => new TankStats(1, 4, 0.4f),
                TankLevel.Level2 => new TankStats(2, 2, 0.65f),
                TankLevel.Level3 => new TankStats(4, 2, 0.8f),
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };
    }
}
