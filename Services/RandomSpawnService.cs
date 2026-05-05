using System;

namespace MyGame.Services
{
    public class RandomSpawnService
    {
        private readonly Random random = new();

        public CellPosition GetPlayerSpawn()
        {
            var spawns = new[]
            {
                new CellPosition(3, 9), // Ä10
                new CellPosition(4, 9)  // Å10
            };

            return spawns[random.Next(spawns.Length)];
        }
    }
}