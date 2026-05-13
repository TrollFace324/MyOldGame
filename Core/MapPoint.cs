namespace MyGame
{
    // Хранит точку на карте в дробных координатах
    public readonly struct MapPoint(float x, float y)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
    }
}
