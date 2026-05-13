using System;
using System.Collections.Generic;
using MyGame.Services;

namespace MyGame
{
    public class GameSession
    {
        // Минимальное время удержания направления, после которого танк игрока начинает движение
        private const float MoveStartHoldSeconds = 0.1f;

        // Базовая пауза между выстрелами игрока в секундах
        private const float FireIntervalSeconds = 0.7f;

        // Скорость снаряда игрока в клетках карты за секунду
        private const float ProjectileSpeedCellsPerSecond = 6f;

        // Количество обычных волн до финальной волны
        private readonly int normalWaveCount = 8;

        // Базовое количество танков в обычной волне без бонусных врагов
        private readonly int tanksPerWave = 10;

        // Каждая N-я обычная волна получает бонусных врагов
        private readonly int bonusWavePeriod = 2;

        // Количество дополнительных танков второго уровня в бонусной волне
        private readonly int bonusLevel2Count = 5;

        // Количество дополнительных танков третьего уровня в бонусной волне
        private readonly int bonusLevel3Count = 5;

        // Номер финальной волны
        private readonly int finalWaveNumber = 9;

        // Общее количество танков в финальной волне
        private readonly int finalWaveTanksCount = 30;

        // Доля танков первого уровня в обычной волне
        private readonly float level1WaveShare = 0.5f;

        // Доля танков второго уровня в обычной волне
        private readonly float level2WaveShare = 0.3f;

        // Доля танков третьего уровня в обычной волне
        private readonly float level3WaveShare = 0.2f;

        // Множитель скорости вражеских танков и снарядов относительно игрока
        private readonly float enemySpeedMultiplier = 1f - 0.6f;

        // Радиус в клетках, в котором союзный танк отключает осторожность поврежденного врага
        private readonly int enemyCourageRadiusCells = 2;

        // Радиус в клетках, на котором враг раскрывает танк игрока в кусте
        private readonly int bushRevealRadiusCells = 1;

        // Время в секундах, на которое выстрел из куста отключает скрытность
        private readonly float bushShotRevealSeconds = 2f;

        // Период появления танков из текущей волны в секундах
        private readonly float waveTankSpawnPeriodSeconds = 3f;

        private float elapsedSinceShot = FireIntervalSeconds;
        private bool isFireHeld;
        private float elapsedSinceEnemySpawn;
        private float remainingBushShotRevealSeconds;
        private readonly List<MoveDirection> heldDirections = [];
        private readonly Dictionary<MoveDirection, float> heldDirectionSeconds = [];
        private readonly List<Projectile> activeProjectiles = [];
        private readonly Queue<TankLevel> pendingEnemyLevels = [];
        private readonly List<Tank> enemyTanks = [];
        private readonly Dictionary<Tank, float> elapsedSinceEnemyShot = [];
        private readonly Dictionary<Tank, MoveDirection> enemyPlayerFireBypassDirections = [];
        private readonly Dictionary<Tank, MoveDirection> enemyStoneBypassDirections = [];
        private CellPosition? levelBonusCell;
        private int currentWaveNumber;
        private bool allWavesCompleted;

        // Создает карту, игрока и первую волну врагов
        public GameSession(RandomSpawnService spawnService, GameMap? map = null)
        {
            Map = map ?? new GameMap();
            PlayerTank = new Tank(spawnService.GetPlayerSpawn(), TankLevel.Level1);
            StartNextWave();
            elapsedSinceEnemySpawn = waveTankSpawnPeriodSeconds;
            if (SpawnPendingEnemyTank())
                elapsedSinceEnemySpawn = 0f;
        }

        public GameMap Map { get; }
        public Tank PlayerTank { get; }
        public IReadOnlyList<Tank> EnemyTanks => enemyTanks;
        public IReadOnlyList<Projectile> ActiveProjectiles => activeProjectiles;
        public CellPosition? LevelBonusCell => levelBonusCell;
        public int CurrentWaveNumber => currentWaveNumber;
        public bool ShouldReturnToMainMenu { get; private set; }
        public event Action? ProjectileFired;
        public event Action? TankHit;

        // Запоминает нажатое направление и начинает считать время удержания
        public void PressDirection(MoveDirection direction)
        {
            if (direction == MoveDirection.None || heldDirectionSeconds.ContainsKey(direction))
                return;

            heldDirections.Add(direction);
            heldDirectionSeconds[direction] = 0f;
        }

        // Обрабатывает отпускание направления: поворот или возврат недоехавшего танка
        public void ReleaseDirection(MoveDirection direction)
        {
            if (direction == MoveDirection.None)
                return;

            if (PlayerTank.IsMoving && direction == PlayerTank.MovementDirection)
                PlayerTank.ResolveMoveAfterInputRelease();

            if (!heldDirectionSeconds.TryGetValue(direction, out var holdSeconds))
                return;

            heldDirections.Remove(direction);
            heldDirectionSeconds.Remove(direction);

            if (!PlayerTank.IsMoving && holdSeconds < MoveStartHoldSeconds)
                PlayerTank.RotateTo(direction);
        }

        // Начинает стрельбу и сразу пробует сделать первый выстрел
        public void PressFire()
        {
            if (isFireHeld)
                return;

            isFireHeld = true;
            TryFirePlayer();
        }

        // Останавливает стрельбу при отпускании пробела
        public void ReleaseFire()
        {
            isFireHeld = false;
        }

        // Создает снаряд игрока, если прошел период перезарядки
        private bool TryFirePlayer()
        {
            if (elapsedSinceShot < FireIntervalSeconds)
                return false;

            var spawnPoint = GetProjectileSpawnPoint(PlayerTank.CenterPoint, PlayerTank.FacingDirection);
            activeProjectiles.Add(new Projectile(
                spawnPoint.X,
                spawnPoint.Y,
                PlayerTank.FacingDirection,
                ProjectileSpeedCellsPerSecond,
                PlayerTank.Damage,
                ProjectileOwner.Player));
            elapsedSinceShot = 0f;

            if (IsPlayerInBush())
                remainingBushShotRevealSeconds = bushShotRevealSeconds;

            ProjectileFired?.Invoke();
            return true;
        }

        // Главный игровой тик: обновляет все состояние с учетом прошедшего времени
        public void Update(float deltaTime)
        {
            elapsedSinceShot += deltaTime;
            UpdateProjectiles(deltaTime);

            if (ShouldReturnToMainMenu)
                return;

            UpdateBushShotReveal(deltaTime);
            UpdateHeldDirections(deltaTime);
            PlayerTank.Update(deltaTime);

            var isPlayerInBush = IsPlayerInBush();

            if (isPlayerInBush && IsPlayerHiddenInBush())
                UpdateEnemyRandomSearch(deltaTime);
            else
                UpdateEnemyTanks(deltaTime);

            UpdateEnemyWaves(deltaTime);
            UpdateWaterCollision();
            UpdateLevelBonusCollision();
            UpdatePlayerFire();
            UpdatePlayerMovementDecision();
        }

        // Продолжает автоматическую стрельбу, пока пробел зажат
        private void UpdatePlayerFire()
        {
            if (isFireHeld)
                TryFirePlayer();
        }

        // Уменьшает таймер раскрытия после выстрела из куста.
        private void UpdateBushShotReveal(float deltaTime)
        {
            if (remainingBushShotRevealSeconds <= 0f)
                return;

            remainingBushShotRevealSeconds = MathF.Max(0f, remainingBushShotRevealSeconds - deltaTime);
        }

        // Решает, нужно ли начать движение игрока после удержания клавиши
        private void UpdatePlayerMovementDecision()
        {
            if (ShouldReturnToMainMenu)
                return;

            if (PlayerTank.IsMoving)
                return;

            var direction = GetActiveHeldDirection();

            if (direction == MoveDirection.None || heldDirectionSeconds[direction] < MoveStartHoldSeconds)
                return;

            PlayerTank.RotateTo(direction);
            TryStartTankMove(PlayerTank, direction);
        }

        // Увеличивает таймеры удержания для всех нажатых направлений
        private void UpdateHeldDirections(float deltaTime)
        {
            foreach (var direction in heldDirections)
                heldDirectionSeconds[direction] += deltaTime;
        }

        // Возвращает последнее нажатое направление
        private MoveDirection GetActiveHeldDirection()
        {
            if (heldDirections.Count == 0)
                return MoveDirection.None;

            return heldDirections[^1];
        }

        // Запускает новые волны и выпускает врагов с задержкой
        private void UpdateEnemyWaves(float deltaTime)
        {
            if (pendingEnemyLevels.Count == 0 && enemyTanks.Count == 0)
                StartNextWave();

            elapsedSinceEnemySpawn += deltaTime;

            if (elapsedSinceEnemySpawn < waveTankSpawnPeriodSeconds)
                return;

            if (SpawnPendingEnemyTank())
                elapsedSinceEnemySpawn = 0f;
        }

        // Готовит очередь врагов для следующей волны
        private void StartNextWave()
        {
            if (allWavesCompleted || pendingEnemyLevels.Count > 0 || enemyTanks.Count > 0)
                return;

            var nextWaveNumber = currentWaveNumber + 1;
            var waveLevels = CreateWaveLevels(nextWaveNumber);

            if (waveLevels.Count == 0)
            {
                allWavesCompleted = true;
                return;
            }

            currentWaveNumber = nextWaveNumber;

            foreach (var level in waveLevels)
                pendingEnemyLevels.Enqueue(level);
        }

        // Создает список уровней танков для указанной волны
        private List<TankLevel> CreateWaveLevels(int waveNumber)
        {
            var waveLevels = new List<TankLevel>();

            if (waveNumber == finalWaveNumber)
            {
                AddLevel(waveLevels, TankLevel.Level1, finalWaveTanksCount / 3);
                AddLevel(waveLevels, TankLevel.Level2, finalWaveTanksCount / 3);
                AddLevel(waveLevels, TankLevel.Level3, finalWaveTanksCount / 3);
                return waveLevels;
            }

            if (waveNumber > normalWaveCount)
                return waveLevels;

            AddLevel(waveLevels, TankLevel.Level1, (int)MathF.Ceiling(tanksPerWave * level1WaveShare));
            AddLevel(waveLevels, TankLevel.Level2, (int)MathF.Ceiling(tanksPerWave * level2WaveShare));
            AddLevel(waveLevels, TankLevel.Level3, (int)MathF.Ceiling(tanksPerWave * level3WaveShare));

            if (waveNumber % bonusWavePeriod == 0)
            {
                AddLevel(waveLevels, TankLevel.Level2, bonusLevel2Count);
                AddLevel(waveLevels, TankLevel.Level3, bonusLevel3Count);
            }

            return waveLevels;
        }

        // Добавляет в волну несколько танков одного уровня
        private static void AddLevel(List<TankLevel> levels, TankLevel level, int count)
        {
            for (var i = 0; i < count; i++)
                levels.Add(level);
        }

        // Спавнит одного врага из очереди, если есть свободная точка спавна
        private bool SpawnPendingEnemyTank()
        {
            if (pendingEnemyLevels.Count == 0)
                return false;

            var availableSpawnCells = GetAvailableEnemySpawnCells();

            if (availableSpawnCells.Count == 0)
                return false;

            var selectedSpawnCell = availableSpawnCells[Random.Shared.Next(availableSpawnCells.Count)];
            var enemyTank = CreateEnemyTank(selectedSpawnCell, pendingEnemyLevels.Dequeue());
            enemyTanks.Add(enemyTank);
            elapsedSinceEnemyShot[enemyTank] = GetEnemyFireIntervalSeconds();
            return true;
        }

        // Возвращает свободные клетки, где можно создать врага
        private List<CellPosition> GetAvailableEnemySpawnCells()
        {
            var availableSpawnCells = new List<CellPosition>();

            foreach (var spawnCell in Map.EnemySpawnCells)
            {
                if (CanSpawnEnemyAt(spawnCell))
                    availableSpawnCells.Add(spawnCell);
            }

            return availableSpawnCells;
        }

        // Проверяет, подходит ли клетка для появления врага
        private bool CanSpawnEnemyAt(CellPosition cell) =>
            !Map.IsBlocked(cell) &&
            Map.GetTerrain(cell) != TerrainType.Water &&
            !IsAnyTankOccupyingCell(cell);

        // Создает объект вражеского танка нужного уровня
        private static Tank CreateEnemyTank(CellPosition spawnCell, TankLevel level) =>
            new(spawnCell, level);

        // Обновляет движение и решения всех врагов
        private void UpdateEnemyTanks(float deltaTime)
        {
            foreach (var enemyTank in enemyTanks)
            {
                elapsedSinceEnemyShot[enemyTank] = GetEnemyShotElapsed(enemyTank) + deltaTime;
                enemyTank.Update(deltaTime);

                if (enemyTank.IsMoving)
                    continue;

                var shouldAvoidPlayerFire = ShouldEnemyAvoidPlayerFire(enemyTank);

                if (TryContinueEnemyBypass(enemyTank, shouldAvoidPlayerFire))
                    continue;

                if (shouldAvoidPlayerFire && TryStartEnemyPlayerFireAvoidance(enemyTank))
                    continue;

                if (TryHandleEnemyAttack(enemyTank, shouldAvoidPlayerFire))
                    continue;

                MoveEnemyToShootingAxis(enemyTank, shouldAvoidPlayerFire);
            }
        }

        // Обновляет врагов, когда игрок спрятан в кусте: танки доезжают и ищут его случайными шагами.
        private void UpdateEnemyRandomSearch(float deltaTime)
        {
            foreach (var enemyTank in enemyTanks)
            {
                enemyTank.Update(deltaTime);

                if (enemyTank.IsMoving)
                    continue;

                enemyPlayerFireBypassDirections.Remove(enemyTank);
                enemyStoneBypassDirections.Remove(enemyTank);
                TryStartEnemyRandomMove(enemyTank);
            }
        }

        // Пробует сдвинуть врага на одну клетку в случайном доступном направлении.
        private bool TryStartEnemyRandomMove(Tank enemyTank)
        {
            foreach (var direction in GetRandomMoveDirections())
            {
                enemyTank.RotateTo(direction);

                if (TryStartTankMove(enemyTank, direction))
                    return true;
            }

            return false;
        }

        // Продолжает уже начатый обход линии огня или камня
        private bool TryContinueEnemyBypass(Tank enemyTank, bool avoidPlayerFireLine) =>
            TryContinueEnemyPlayerFireBypass(enemyTank, avoidPlayerFireLine) ||
            TryContinueEnemyStoneBypass(enemyTank, avoidPlayerFireLine);

        // Пытается увести поврежденного врага с линии огня игрока
        private bool TryStartEnemyPlayerFireAvoidance(Tank enemyTank) =>
            IsEnemyInPlayerFireLine(enemyTank) &&
            TryStartEnemyPlayerFireBypass(enemyTank);

        // Обрабатывает атаку врага, если он стоит на одной оси с игроком
        private bool TryHandleEnemyAttack(Tank enemyTank, bool avoidPlayerFireLine)
        {
            var attackDirection = GetDirectionToPlayerOnAxis(enemyTank);

            if (attackDirection == MoveDirection.None)
                return false;

            enemyTank.RotateTo(attackDirection);
            TryFireEnemy(enemyTank);

            if (TryStartEnemyStoneBypass(enemyTank, attackDirection, avoidPlayerFireLine))
                return true;

            if (avoidPlayerFireLine)
                TryStartEnemyMoveAvoidingPlayerFire(enemyTank, [attackDirection]);
            else
                TryStartEnemyMoveIgnoringPlayerFire(enemyTank, [attackDirection]);

            return true;
        }


        // Двигает врага к ближайшей строке или колонке игрока
        private void MoveEnemyToShootingAxis(Tank enemyTank, bool avoidPlayerFireLine)
        {
            var directions = GetDirectionsToNearestShootingAxis(enemyTank);

            if (avoidPlayerFireLine)
                TryStartEnemyMoveAvoidingPlayerFire(enemyTank, directions);
            else
                TryStartEnemyMoveIgnoringPlayerFire(enemyTank, directions);
        }

        // Поврежденный враг боится линии огня только если рядом нет союзного танка.
        private bool ShouldEnemyAvoidPlayerFire(Tank enemyTank) =>
            IsEnemyDamaged(enemyTank) &&
            !HasNearbyEnemyTank(enemyTank);

        // Проверяет, есть ли рядом другой вражеский танк в радиусе поддержки.
        private bool HasNearbyEnemyTank(Tank enemyTank)
        {
            var enemyCell = GetCellAt(enemyTank.CenterPoint);

            foreach (var otherEnemyTank in enemyTanks)
            {
                if (ReferenceEquals(otherEnemyTank, enemyTank) || !otherEnemyTank.IsAlive)
                    continue;

                var otherEnemyCell = GetCellAt(otherEnemyTank.CenterPoint);

                if (Math.Abs(otherEnemyCell.Column - enemyCell.Column) <= enemyCourageRadiusCells &&
                    Math.Abs(otherEnemyCell.Row - enemyCell.Row) <= enemyCourageRadiusCells)
                {
                    return true;
                }
            }

            return false;
        }

        // Проверяет, спрятан ли игрок в кусте без врагов рядом.
        private bool IsPlayerHiddenInBush() =>
            IsPlayerInBush() &&
            remainingBushShotRevealSeconds <= 0f &&
            !IsAnyEnemyNearCell(GetCellAt(PlayerTank.CenterPoint), bushRevealRadiusCells);

        // Проверяет, находится ли игрок в кусте.
        private bool IsPlayerInBush() =>
            Map.GetTerrainAt(PlayerTank.CenterPoint) == TerrainType.Bush;

        // Проверяет, есть ли вражеский танк рядом с указанной клеткой.
        private bool IsAnyEnemyNearCell(CellPosition cell, int radiusCells)
        {
            foreach (var enemyTank in enemyTanks)
            {
                if (!enemyTank.IsAlive)
                    continue;

                var enemyCell = GetCellAt(enemyTank.CenterPoint);

                if (Math.Abs(enemyCell.Column - cell.Column) <= radiusCells &&
                    Math.Abs(enemyCell.Row - cell.Row) <= radiusCells)
                {
                    return true;
                }
            }

            return false;
        }

        // Проверяет, получил ли враг урон
        private static bool IsEnemyDamaged(Tank enemyTank) =>
            enemyTank.Health < enemyTank.MaxHealth;

        // Проверяет клетку и запускает движение танка
        private bool TryStartTankMove(Tank tank, MoveDirection direction, bool avoidPlayerFireLine = false)
        {
            var current = tank.CurrentCell;
            var next = GetNextCell(current, direction);

            if (direction == MoveDirection.None)
                return false;

            if (!Map.IsInside(next) || IsCellBlockedForTank(tank, next) || IsTankCellBlocked(tank, next))
                return false;

            if (avoidPlayerFireLine &&
                !ReferenceEquals(tank, PlayerTank) &&
                IsEnemyCellInPlayerFireLine(tank, next))
            {
                return false;
            }

            Map.TryBreakFenceBetween(current, next);
            tank.StartMoveTo(
                next,
                direction,
                ReferenceEquals(tank, PlayerTank) ? 1f : enemySpeedMultiplier);
            return true;
        }

        // Проверяет препятствия на клетке для конкретного танка
        private bool IsCellBlockedForTank(Tank tank, CellPosition cell)
        {
            if (Map.IsBlocked(cell))
                return true;

            return !ReferenceEquals(tank, PlayerTank) &&
                Map.GetTerrain(cell) == TerrainType.Water;
        }

        // Выбирает ход врага с попыткой не попасть на линию огня
        private bool TryStartEnemyMoveAvoidingPlayerFire(Tank enemyTank, IEnumerable<MoveDirection> preferredDirections)
        {
            var directions = GetUniqueDirections(preferredDirections);

            if (TryStartEnemyMove(enemyTank, directions, true))
                return true;

            var fallbackDirections = GetFallbackDirections(directions);

            if (TryStartEnemyMove(enemyTank, fallbackDirections, true))
                return true;

            if (TryStartEnemyMove(enemyTank, directions, false))
                return true;

            return TryStartEnemyMove(enemyTank, fallbackDirections, false);
        }

        // Выбирает ход врага без учета линии огня игрока
        private bool TryStartEnemyMoveIgnoringPlayerFire(Tank enemyTank, IEnumerable<MoveDirection> preferredDirections)
        {
            var directions = GetUniqueDirections(preferredDirections);
            return TryStartEnemyMove(enemyTank, directions, false) ||
                TryStartEnemyMove(enemyTank, GetFallbackDirections(directions), false);
        }

        // Пробует запустить движение врага по одному из направлений
        private bool TryStartEnemyMove(Tank enemyTank, IEnumerable<MoveDirection> directions, bool avoidPlayerFireLine)
        {
            foreach (var direction in directions)
            {
                enemyTank.RotateTo(direction);

                if (TryStartTankMove(enemyTank, direction, avoidPlayerFireLine))
                    return true;
            }

            return false;
        }

        // Проверяет, стоит ли враг сейчас на линии выстрела игрока
        private bool IsEnemyInPlayerFireLine(Tank enemyTank)
        {
            return IsEnemyCellInPlayerFireLine(enemyTank, GetCellAt(enemyTank.CenterPoint));
        }

        // Проверяет, будет ли клетка врага на линии выстрела игрока
        private bool IsEnemyCellInPlayerFireLine(Tank enemyTank, CellPosition enemyCell)
        {
            var playerCell = GetCellAt(PlayerTank.CenterPoint);
            var fireDirection = PlayerTank.FacingDirection;
            var distanceToEnemy = GetDistanceAlongDirection(playerCell, enemyCell, fireDirection);

            if (distanceToEnemy <= 0 || HasStoneBetween(playerCell, enemyCell, fireDirection))
                return false;

            foreach (var otherEnemyTank in enemyTanks)
            {
                if (ReferenceEquals(otherEnemyTank, enemyTank) || !otherEnemyTank.IsAlive)
                    continue;

                var otherEnemyCell = GetCellAt(otherEnemyTank.CenterPoint);
                var distanceToOtherEnemy = GetDistanceAlongDirection(playerCell, otherEnemyCell, fireDirection);

                if (distanceToOtherEnemy > 0 && distanceToOtherEnemy < distanceToEnemy)
                    return false;
            }

            return true;
        }

        // Проверяет, есть ли камень между двумя клетками
        private bool HasStoneBetween(CellPosition start, CellPosition target, MoveDirection direction)
        {
            var current = GetNextCell(start, direction);

            while (!AreSameCell(current, target))
            {
                if (!Map.IsInside(current))
                    return true;

                if (Map.GetTerrain(current) == TerrainType.Stone)
                    return true;

                current = GetNextCell(current, direction);
            }

            return false;
        }

        // Начинает боковой уход врага с линии огня игрока
        private bool TryStartEnemyPlayerFireBypass(Tank enemyTank)
        {
            var playerFireDirection = PlayerTank.FacingDirection;

            foreach (var sideDirection in GetSideStepDirections(playerFireDirection))
            {
                enemyTank.RotateTo(sideDirection);

                if (!TryStartTankMove(enemyTank, sideDirection, true))
                    continue;

                enemyPlayerFireBypassDirections[enemyTank] = playerFireDirection;
                return true;
            }

            return false;
        }

        // Продолжает движение врага после ухода с линии огня
        private bool TryContinueEnemyPlayerFireBypass(Tank enemyTank, bool avoidPlayerFireLine)
        {
            if (!enemyPlayerFireBypassDirections.TryGetValue(enemyTank, out var direction))
                return false;

            if (!avoidPlayerFireLine)
            {
                enemyPlayerFireBypassDirections.Remove(enemyTank);
                return false;
            }

            enemyTank.RotateTo(direction);

            if (TryStartTankMove(enemyTank, direction, true))
            {
                enemyPlayerFireBypassDirections.Remove(enemyTank);
                return true;
            }

            enemyPlayerFireBypassDirections.Remove(enemyTank);
            return false;
        }

        // Начинает обход камня, который мешает ехать к игроку
        private bool TryStartEnemyStoneBypass(Tank enemyTank, MoveDirection blockedDirection, bool avoidPlayerFireLine)
        {
            if (!IsNextCellStone(enemyTank, blockedDirection))
                return false;

            if (TryStartEnemyStoneSideStep(enemyTank, blockedDirection, avoidPlayerFireLine))
                return true;

            if (!avoidPlayerFireLine)
                return false;

            foreach (var sideDirection in GetSideStepDirections(blockedDirection))
            {
                enemyTank.RotateTo(sideDirection);

                if (!TryStartTankMove(enemyTank, sideDirection))
                    continue;

                enemyStoneBypassDirections[enemyTank] = blockedDirection;
                return true;
            }

            enemyTank.RotateTo(blockedDirection);
            return false;
        }

        // Пробует сделать боковой шаг при обходе камня
        private bool TryStartEnemyStoneSideStep(Tank enemyTank, MoveDirection blockedDirection, bool avoidPlayerFireLine)
        {
            foreach (var sideDirection in GetSideStepDirections(blockedDirection))
            {
                enemyTank.RotateTo(sideDirection);

                if (!TryStartTankMove(enemyTank, sideDirection, avoidPlayerFireLine))
                    continue;

                enemyStoneBypassDirections[enemyTank] = blockedDirection;
                return true;
            }

            return false;
        }

        // Продолжает движение врага после бокового обхода камня
        private bool TryContinueEnemyStoneBypass(Tank enemyTank, bool avoidPlayerFireLine)
        {
            if (!enemyStoneBypassDirections.TryGetValue(enemyTank, out var direction))
                return false;

            enemyTank.RotateTo(direction);

            if (TryStartTankMove(enemyTank, direction, avoidPlayerFireLine) ||
                avoidPlayerFireLine && TryStartTankMove(enemyTank, direction))
            {
                enemyStoneBypassDirections.Remove(enemyTank);
                return true;
            }

            enemyStoneBypassDirections.Remove(enemyTank);
            return false;
        }

        // Проверяет, стоит ли камень в следующей клетке
        private bool IsNextCellStone(Tank tank, MoveDirection direction)
        {
            var next = GetNextCell(tank.CurrentCell, direction);
            return direction != MoveDirection.None &&
                Map.IsInside(next) &&
                Map.GetTerrain(next) == TerrainType.Stone;
        }

        // Возвращает боковые направления для обхода
        private static IEnumerable<MoveDirection> GetSideStepDirections(MoveDirection blockedDirection)
        {
            if (blockedDirection == MoveDirection.Up || blockedDirection == MoveDirection.Down)
            {
                yield return MoveDirection.Left;
                yield return MoveDirection.Right;
            }
            else if (blockedDirection == MoveDirection.Left || blockedDirection == MoveDirection.Right)
            {
                yield return MoveDirection.Up;
                yield return MoveDirection.Down;
            }
        }

        // Убирает пустые и повторяющиеся направления
        private static List<MoveDirection> GetUniqueDirections(IEnumerable<MoveDirection> directions)
        {
            var uniqueDirections = new List<MoveDirection>();

            foreach (var direction in directions)
            {
                if (direction == MoveDirection.None || uniqueDirections.Contains(direction))
                    continue;

                uniqueDirections.Add(direction);
            }

            return uniqueDirections;
        }

        // Возвращает запасные направления после предпочитаемых
        private static IEnumerable<MoveDirection> GetFallbackDirections(IReadOnlyList<MoveDirection> preferredDirections)
        {
            foreach (var direction in GetAllMoveDirections())
            {
                if (ContainsDirection(preferredDirections, direction))
                    continue;

                yield return direction;
            }
        }

        // Проверяет, есть ли направление в списке
        private static bool ContainsDirection(IReadOnlyList<MoveDirection> directions, MoveDirection direction)
        {
            foreach (var candidate in directions)
            {
                if (candidate == direction)
                    return true;
            }

            return false;
        }

        // Возвращает все направления движения
        private static IEnumerable<MoveDirection> GetAllMoveDirections()
        {
            yield return MoveDirection.Up;
            yield return MoveDirection.Down;
            yield return MoveDirection.Left;
            yield return MoveDirection.Right;
        }

        // Возвращает все направления движения в случайном порядке.
        private static List<MoveDirection> GetRandomMoveDirections()
        {
            var directions = GetUniqueDirections(GetAllMoveDirections());

            for (var i = directions.Count - 1; i > 0; i--)
            {
                var randomIndex = Random.Shared.Next(i + 1);
                (directions[i], directions[randomIndex]) = (directions[randomIndex], directions[i]);
            }

            return directions;
        }

        // Создает снаряд врага, если прошла перезарядка
        private bool TryFireEnemy(Tank enemyTank)
        {
            if (GetEnemyShotElapsed(enemyTank) < GetEnemyFireIntervalSeconds())
                return false;

            var spawnPoint = GetProjectileSpawnPoint(enemyTank.CenterPoint, enemyTank.FacingDirection);
            activeProjectiles.Add(new Projectile(
                spawnPoint.X,
                spawnPoint.Y,
                enemyTank.FacingDirection,
                ProjectileSpeedCellsPerSecond * enemySpeedMultiplier,
                enemyTank.Damage,
                ProjectileOwner.Enemy));
            elapsedSinceEnemyShot[enemyTank] = 0f;
            ProjectileFired?.Invoke();
            return true;
        }

        // Возвращает период стрельбы врага
        private float GetEnemyFireIntervalSeconds() =>
            FireIntervalSeconds / enemySpeedMultiplier;

        // Возвращает время после последнего выстрела врага
        private float GetEnemyShotElapsed(Tank enemyTank) =>
            elapsedSinceEnemyShot.TryGetValue(enemyTank, out var elapsed)
                ? elapsed
                : FireIntervalSeconds;

        // Возвращает направление к игроку, если он на той же оси
        private MoveDirection GetDirectionToPlayerOnAxis(Tank enemyTank)
        {
            var enemyCell = enemyTank.CurrentCell;
            var playerCell = PlayerTank.CurrentCell;

            if (enemyCell.Column == playerCell.Column)
                return GetVerticalDirection(enemyCell, playerCell);

            if (enemyCell.Row == playerCell.Row)
                return GetHorizontalDirection(enemyCell, playerCell);

            return MoveDirection.None;
        }

        // Возвращает направления к ближайшей оси стрельбы по игроку
        private IEnumerable<MoveDirection> GetDirectionsToNearestShootingAxis(Tank enemyTank)
        {
            var enemyCell = enemyTank.CurrentCell;
            var playerCell = PlayerTank.CurrentCell;
            var horizontalDistance = Math.Abs(playerCell.Column - enemyCell.Column);
            var verticalDistance = Math.Abs(playerCell.Row - enemyCell.Row);
            var horizontalDirection = GetHorizontalDirection(enemyCell, playerCell);
            var verticalDirection = GetVerticalDirection(enemyCell, playerCell);

            if (horizontalDistance <= verticalDistance)
            {
                yield return horizontalDirection;
                yield return verticalDirection;
            }
            else
            {
                yield return verticalDirection;
                yield return horizontalDirection;
            }
        }

        // Возвращает горизонтальное направление от клетки к клетке
        private static MoveDirection GetHorizontalDirection(CellPosition from, CellPosition to)
        {
            if (to.Column < from.Column)
                return MoveDirection.Left;

            if (to.Column > from.Column)
                return MoveDirection.Right;

            return MoveDirection.None;
        }

        // Возвращает вертикальное направление от клетки к клетке
        private static MoveDirection GetVerticalDirection(CellPosition from, CellPosition to)
        {
            if (to.Row < from.Row)
                return MoveDirection.Up;

            if (to.Row > from.Row)
                return MoveDirection.Down;

            return MoveDirection.None;
        }

        // Проверяет, занята ли клетка другим танком
        private bool IsTankCellBlocked(Tank movingTank, CellPosition cell)
        {
            if (!ReferenceEquals(movingTank, PlayerTank) && IsTankOccupyingCell(PlayerTank, cell))
                return true;

            foreach (var enemyTank in enemyTanks)
            {
                if (ReferenceEquals(movingTank, enemyTank))
                    continue;

                if (IsTankOccupyingCell(enemyTank, cell))
                    return true;
            }

            return false;
        }

        // Проверяет, занята ли клетка любым танком
        private bool IsAnyTankOccupyingCell(CellPosition cell)
        {
            if (IsTankOccupyingCell(PlayerTank, cell))
                return true;

            foreach (var enemyTank in enemyTanks)
            {
                if (IsTankOccupyingCell(enemyTank, cell))
                    return true;
            }

            return false;
        }

        // Проверяет, занимает ли конкретный танк клетку
        private static bool IsTankOccupyingCell(Tank tank, CellPosition cell)
        {
            if (!tank.IsAlive)
                return false;

            return AreSameCell(tank.CurrentCell, cell) ||
                tank.IsMoving && AreSameCell(tank.MoveTargetCell, cell);
        }

        // Двигает снаряды и удаляет их при столкновениях
        private void UpdateProjectiles(float deltaTime)
        {
            if (activeProjectiles.Count == 0)
                return;

            for (var i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = activeProjectiles[i];
                var startX = projectile.X;
                var startY = projectile.Y;

                projectile.Update(deltaTime);

                if (!IsInsideMapBounds(projectile.X, projectile.Y) ||
                    Map.TryBreakFenceCrossedBySegment(startX, startY, projectile.X, projectile.Y) ||
                    Map.GetTerrainAt(projectile.CenterPoint) == TerrainType.Stone ||
                    TryDamageTank(projectile))
                {
                    activeProjectiles.RemoveAt(i);
                }
            }
        }

        // Наносит урон танку, если снаряд попал
        private bool TryDamageTank(Projectile projectile)
        {
            var projectileCell = GetCellAt(projectile.CenterPoint);

            if (!Map.IsInside(projectileCell))
                return false;

            if (projectile.Owner == ProjectileOwner.Enemy)
            {
                if (!IsTankCenterInCell(PlayerTank, projectileCell))
                    return false;

                PlayerTank.TakeDamage(projectile.Damage);
                TankHit?.Invoke();

                if (!PlayerTank.IsAlive)
                    ShouldReturnToMainMenu = true;

                return true;
            }

            for (var i = enemyTanks.Count - 1; i >= 0; i--)
            {
                var enemyTank = enemyTanks[i];

                if (!IsTankCenterInCell(enemyTank, projectileCell))
                    continue;

                enemyTank.TakeDamage(projectile.Damage);
                TankHit?.Invoke();

                if (!enemyTank.IsAlive)
                {
                    RemoveEnemyTankAt(i);
                }

                return true;
            }

            return false;
        }

        // Удаляет убитого врага и оставляет бонус на его клетке
        private void RemoveEnemyTankAt(int index)
        {
            var enemyTank = enemyTanks[index];
            levelBonusCell = GetCellAt(enemyTank.CenterPoint);
            elapsedSinceEnemyShot.Remove(enemyTank);
            enemyPlayerFireBypassDirections.Remove(enemyTank);
            enemyStoneBypassDirections.Remove(enemyTank);
            enemyTanks.RemoveAt(index);
        }

        // Проверяет, заехал ли игрок на воду
        private void UpdateWaterCollision()
        {
            if (Map.GetTerrainAt(PlayerTank.CenterPoint) == TerrainType.Water)
                ShouldReturnToMainMenu = true;
        }

        // Проверяет, находится ли центр танка в клетке
        private static bool IsTankCenterInCell(Tank tank, CellPosition cell) =>
            tank.IsAlive &&
            AreSameCell(GetCellAt(tank.CenterPoint), cell);

        // Обрабатывает подбор стрелки улучшения
        private void UpdateLevelBonusCollision()
        {
            if (!levelBonusCell.HasValue || !AreSameCell(GetCellAt(PlayerTank.CenterPoint), levelBonusCell.Value))
                return;

            if (PlayerTank.Health < PlayerTank.MaxHealth)
            {
                PlayerTank.Repair();
            }
            else if (PlayerTank.Level < TankLevel.Level3)
            {
                PlayerTank.Upgrade();
            }
            else
            {
                PlayerTank.ResetLevel();
            }

            levelBonusCell = null;
        }

        // Возвращает точку появления снаряда у дула танка
        private static (float X, float Y) GetProjectileSpawnPoint(MapPoint tankCenter, MoveDirection direction)
        {
            var centerX = tankCenter.X;
            var centerY = tankCenter.Y;
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

        // Проверяет, находится ли точка внутри карты
        private static bool IsInsideMapBounds(float x, float y) =>
            x >= 0f &&
            x <= GameMap.Width &&
            y >= 0f &&
            y <= GameMap.Height;

        // Переводит точку карты в клетку
        private static CellPosition GetCellAt(MapPoint point) =>
            new((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));

        // Возвращает соседнюю клетку по направлению
        private static CellPosition GetNextCell(CellPosition current, MoveDirection direction) =>
            direction switch
            {
                MoveDirection.Up => new CellPosition(current.Column, current.Row - 1),
                MoveDirection.Down => new CellPosition(current.Column, current.Row + 1),
                MoveDirection.Left => new CellPosition(current.Column - 1, current.Row),
                MoveDirection.Right => new CellPosition(current.Column + 1, current.Row),
                _ => current
            };

        // Считает расстояние по выбранному направлению
        private static int GetDistanceAlongDirection(CellPosition from, CellPosition to, MoveDirection direction)
        {
            return direction switch
            {
                MoveDirection.Up when from.Column == to.Column && to.Row < from.Row => from.Row - to.Row,
                MoveDirection.Down when from.Column == to.Column && to.Row > from.Row => to.Row - from.Row,
                MoveDirection.Left when from.Row == to.Row && to.Column < from.Column => from.Column - to.Column,
                MoveDirection.Right when from.Row == to.Row && to.Column > from.Column => to.Column - from.Column,
                _ => 0
            };
        }

        // Проверяет, совпадают ли две клетки
        private static bool AreSameCell(CellPosition first, CellPosition second) =>
            first.Column == second.Column &&
            first.Row == second.Row;
    }
}
