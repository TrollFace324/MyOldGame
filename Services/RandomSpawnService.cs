using System;

namespace MyGame.Services
{
    public class RandomSpawnService
    {
        private readonly Random random = new();

        // Возвращает случайную стартовую клетку игрока
        public CellPosition GetPlayerSpawn()
        {
            var spawns = new[]
            {
                new CellPosition(3, 9),
                new CellPosition(4, 9)
            };

            return spawns[random.Next(spawns.Length)];
        }
    }
}
