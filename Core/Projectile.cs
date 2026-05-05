using System;

namespace MyGame
{
    public class Projectile(float x, float y, MoveDirection direction, float speed)
    {
        public float X { get; private set; } = x;
        public float Y { get; private set; } = y;
        public MoveDirection Direction { get; } = direction;
        public float Speed { get; } = speed;

        public void Update(float deltaTime)
        {
            var distance = Speed * deltaTime;

            switch (Direction)
            {
                case MoveDirection.Up:
                    Y -= distance;
                    break;
                case MoveDirection.Down:
                    Y += distance;
                    break;
                case MoveDirection.Left:
                    X -= distance;
                    break;
                case MoveDirection.Right:
                    X += distance;
                    break;
            }
        }
    }
}
