using System;
using System.Collections.Generic;
using MyGame.Services;

namespace MyGame
{
    public class GameSession(RandomSpawnService spawnService)
    {
        private const float MoveIntervalSeconds = 0.5f;
        private const float FireIntervalSeconds = 0.10f;
        private const float ProjectileSpeedCellsPerSecond = 12f;
        private float elapsedSinceMove;
        private float elapsedSinceShot = FireIntervalSeconds;
        private MoveDirection activeDirection = MoveDirection.None;
        private readonly List<Projectile> activeProjectiles = [];

        public GameMap Map { get; } = new GameMap();
        public Tank PlayerTank { get; } = new Tank(spawnService.GetPlayerSpawn());
        public IReadOnlyList<Projectile> ActiveProjectiles => activeProjectiles;

        public void SetDirection(MoveDirection direction)
        {
            if (direction == MoveDirection.None)
            {
                activeDirection = MoveDirection.None;
                return;
            }

            PlayerTank.RotateTo(direction);

            if (direction == activeDirection)
                return;

            activeDirection = direction;

            elapsedSinceMove = 0f;
        }

        public void Fire()
        {
            if (elapsedSinceShot < FireIntervalSeconds)
                return;

            var spawnPoint = GetProjectileSpawnPoint(PlayerTank.CurrentCell, PlayerTank.FacingDirection);
            activeProjectiles.Add(new Projectile(
                spawnPoint.X,
                spawnPoint.Y,
                PlayerTank.FacingDirection,
                ProjectileSpeedCellsPerSecond));
            elapsedSinceShot = 0f;
        }

        public void Update(float deltaTime)
        {
            elapsedSinceShot += deltaTime;
            UpdateProjectiles(deltaTime);

            if (activeDirection == MoveDirection.None)
                return;

            elapsedSinceMove += deltaTime;

            if (elapsedSinceMove >= MoveIntervalSeconds)
            {
                elapsedSinceMove = 0f;
                TryMoveTank(activeDirection);
            }
        }

        private void TryMoveTank(MoveDirection direction)
        {
            var current = PlayerTank.CurrentCell;
            CellPosition next;

            switch (direction)
            {
                case MoveDirection.Up:
                    next = new CellPosition(current.Column, current.Row - 1);
                    break;
                case MoveDirection.Down:
                    next = new CellPosition(current.Column, current.Row + 1);
                    break;
                case MoveDirection.Left:
                    next = new CellPosition(current.Column - 1, current.Row);
                    break;
                case MoveDirection.Right:
                    next = new CellPosition(current.Column + 1, current.Row);
                    break;
                case MoveDirection.None:
                default:
                    return;
            }

            if (IsInside(next))
                PlayerTank.MoveTo(next);
        }

        private void UpdateProjectiles(float deltaTime)
        {
            if (activeProjectiles.Count == 0)
                return;

            for (var i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = activeProjectiles[i];
                projectile.Update(deltaTime);

                if (!IsInsideMapBounds(projectile.X, projectile.Y))
                    activeProjectiles.RemoveAt(i);
            }
        }

        private static (float X, float Y) GetProjectileSpawnPoint(CellPosition tankCell, MoveDirection direction)
        {
            var centerX = tankCell.Column + 0.5f;
            var centerY = tankCell.Row + 0.5f;
            const float muzzleOffset = 0.42f;

            return direction switch
            {
                MoveDirection.Up => (centerX, centerY - muzzleOffset),
                MoveDirection.Down => (centerX, centerY + muzzleOffset),
                MoveDirection.Left => (centerX - muzzleOffset, centerY),
                MoveDirection.Right => (centerX + muzzleOffset, centerY),
                _ => (centerX, centerY)
            };
        }

        private bool IsInside(CellPosition cell) =>
            cell.Column >= 0 &&
            cell.Column < GameMap.Width &&
            cell.Row >= 0 &&
            cell.Row < GameMap.Height;

        private static bool IsInsideMapBounds(float x, float y) =>
            x >= 0f &&
            x <= GameMap.Width &&
            y >= 0f &&
            y <= GameMap.Height;
    }
}
