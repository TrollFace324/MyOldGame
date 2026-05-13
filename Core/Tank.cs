using System;

namespace MyGame
{
    // Хранит состояние танка, его уровень, здоровье и движение
    public class Tank(CellPosition startCell, TankLevel level = TankLevel.Level1)
    {
        private CellPosition moveStartCell = startCell;
        private CellPosition moveTargetCell = startCell;
        private float moveStartX = GetCellCenterX(startCell);
        private float moveStartY = GetCellCenterY(startCell);
        private float moveTargetX = GetCellCenterX(startCell);
        private float moveTargetY = GetCellCenterY(startCell);
        private float moveElapsedSeconds;
        private float moveDurationSeconds;

        public CellPosition CurrentCell { get; private set; } = startCell;
        public MoveDirection FacingDirection { get; private set; } = MoveDirection.Up;
        public TankLevel Level { get; private set; } = level;
        public TankStats Stats => TankStats.ForLevel(Level);
        public int Damage => Stats.Damage;
        public int Defense => Stats.Defense;
        public int MaxHealth => Defense;
        public int Health { get; private set; } = TankStats.ForLevel(level).Defense;
        public float MoveSecondsPerCell => Stats.MoveSecondsPerCell;
        public float CenterX { get; private set; } = GetCellCenterX(startCell);
        public float CenterY { get; private set; } = GetCellCenterY(startCell);
        public MapPoint CenterPoint => new MapPoint(CenterX, CenterY);
        public bool IsMoving { get; private set; }
        public MoveDirection MovementDirection { get; private set; } = MoveDirection.None;
        public CellPosition MoveTargetCell => moveTargetCell;
        public bool IsAlive => Health > 0;

        // Запускает плавное движение танка к соседней клетке
        public void StartMoveTo(CellPosition nextCell, MoveDirection direction, float speedMultiplier = 1f)
        {
            if (IsMoving)
                return;

            moveStartCell = CurrentCell;
            moveTargetCell = nextCell;
            moveStartX = CenterX;
            moveStartY = CenterY;
            moveTargetX = GetCellCenterX(nextCell);
            moveTargetY = GetCellCenterY(nextCell);
            moveElapsedSeconds = 0f;
            moveDurationSeconds = MoveSecondsPerCell / MathF.Max(speedMultiplier, 0.001f);
            IsMoving = true;
            MovementDirection = direction;
        }

        // Возвращает танк назад, если клавишу отпустили до грани клетки
        public void ResolveMoveAfterInputRelease()
        {
            if (!IsMoving || HasReachedCellEdge())
                return;

            var distanceToStart = MathF.Abs(CenterX - GetCellCenterX(moveStartCell)) +
                MathF.Abs(CenterY - GetCellCenterY(moveStartCell));

            moveTargetCell = moveStartCell;
            moveStartX = CenterX;
            moveStartY = CenterY;
            moveTargetX = GetCellCenterX(moveStartCell);
            moveTargetY = GetCellCenterY(moveStartCell);
            moveElapsedSeconds = 0f;
            moveDurationSeconds = MathF.Max(MoveSecondsPerCell * distanceToStart, 0.001f);
        }

        // Обновляет плавное движение танка
        public void Update(float deltaTime)
        {
            if (!IsMoving)
                return;

            moveElapsedSeconds += deltaTime;
            var progress = moveDurationSeconds <= 0f
                ? 1f
                : MathF.Min(moveElapsedSeconds / moveDurationSeconds, 1f);

            CenterX = Lerp(moveStartX, moveTargetX, progress);
            CenterY = Lerp(moveStartY, moveTargetY, progress);

            if (progress < 1f)
                return;

            CurrentCell = moveTargetCell;
            moveStartCell = moveTargetCell;
            CenterX = moveTargetX;
            CenterY = moveTargetY;
            IsMoving = false;
            MovementDirection = MoveDirection.None;
        }

        // Поворачивает танк в выбранном направлении
        public void RotateTo(MoveDirection direction)
        {
            if (direction == MoveDirection.None)
                return;

            FacingDirection = direction;
        }

        // Повышает уровень танка и чинит его
        public void Upgrade()
        {
            if (Level < TankLevel.Level3)
            {
                Level++;
                Repair();
            }
        }

        // Сбрасывает танк на первый уровень и чинит его
        public void ResetLevel()
        {
            Level = TankLevel.Level1;
            Repair();
        }

        // Уменьшает здоровье танка
        public void TakeDamage(int damage)
        {
            if (damage <= 0)
                return;

            Health = Math.Max(0, Health - damage);
        }

        // Полностью чинит танк
        public void Repair()
        {
            Health = MaxHealth;
        }

        // Возвращает X центра клетки
        private static float GetCellCenterX(CellPosition cell) =>
            cell.Column + 0.5f;

        // Возвращает Y центра клетки
        private static float GetCellCenterY(CellPosition cell) =>
            cell.Row + 0.5f;

        // Считает промежуточное значение между двумя числами
        private static float Lerp(float start, float end, float progress) =>
            start + (end - start) * progress;

        // Проверяет, пересек ли танк грань стартовой клетки
        private bool HasReachedCellEdge()
        {
            return MovementDirection switch
            {
                MoveDirection.Up => CenterY <= moveStartCell.Row,
                MoveDirection.Down => CenterY >= moveStartCell.Row + 1f,
                MoveDirection.Left => CenterX <= moveStartCell.Column,
                MoveDirection.Right => CenterX >= moveStartCell.Column + 1f,
                _ => true
            };
        }
    }
}
