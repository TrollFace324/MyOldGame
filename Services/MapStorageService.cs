using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyGame.Services
{
    public static class MapStorageService
    {
        private static readonly string MapFileName = "map.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string MapFilePath =>
            Path.Combine(GetProjectRoot(), MapFileName);

        // Сохраняет карту в json файл
        public static string Save(GameMap map)
        {
            var path = MapFilePath;
            var json = JsonSerializer.Serialize(CreateSaveData(map), JsonOptions);
            File.WriteAllText(path, json);
            return path;
        }

        // Загружает карту или создает стандартную
        public static GameMap LoadOrCreateDefault()
        {
            var path = MapFilePath;

            if (!File.Exists(path))
                return new GameMap();

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<MapSaveData>(json, JsonOptions);

                if (data == null || data.Width != GameMap.Width || data.Height != GameMap.Height)
                    return new GameMap();

                return CreateMap(data);
            }
            catch
            {
                return new GameMap();
            }
        }

        // Превращает карту в данные для сохранения
        private static MapSaveData CreateSaveData(GameMap map)
        {
            var terrain = new List<TerrainSaveData>();
            var fences = new List<FenceSaveData>();

            for (var row = 0; row < GameMap.Height; row++)
            {
                for (var column = 0; column < GameMap.Width; column++)
                {
                    var cell = new CellPosition(column, row);
                    var type = map.GetTerrain(cell);

                    if (type == TerrainType.Empty)
                        continue;

                    terrain.Add(new TerrainSaveData
                    {
                        Column = column,
                        Row = row,
                        Type = type
                    });
                }
            }

            foreach (var fence in map.Fences)
            {
                fences.Add(new FenceSaveData
                {
                    First = CellSaveData.From(fence.FirstCell),
                    Second = CellSaveData.From(fence.SecondCell)
                });
            }

            return new MapSaveData
            {
                Width = GameMap.Width,
                Height = GameMap.Height,
                Terrain = terrain,
                Fences = fences
            };
        }

        // Создает карту из сохраненных данных
        private static GameMap CreateMap(MapSaveData data)
        {
            var map = new GameMap(false);

            foreach (var terrain in data.Terrain)
            {
                var cell = new CellPosition(terrain.Column, terrain.Row);

                if (map.IsInside(cell) && terrain.Type != TerrainType.Empty)
                    map.SetTerrain(cell, terrain.Type);
            }

            foreach (var fence in data.Fences)
            {
                var first = fence.First.ToCellPosition();
                var second = fence.Second.ToCellPosition();

                if (map.IsInside(first) && map.IsInside(second) && AreAdjacent(first, second))
                    map.AddFenceBetween(first, second);
            }

            return map;
        }

        // Проверяет, являются ли клетки соседними
        private static bool AreAdjacent(CellPosition first, CellPosition second)
        {
            var columnDistance = Math.Abs(first.Column - second.Column);
            var rowDistance = Math.Abs(first.Row - second.Row);
            return columnDistance + rowDistance == 1;
        }

        // Ищет корень проекта по csproj файлу
        private static string GetProjectRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MyOldGame.csproj")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        public sealed class MapSaveData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public List<TerrainSaveData> Terrain { get; set; } = [];
            public List<FenceSaveData> Fences { get; set; } = [];
        }

        public sealed class TerrainSaveData
        {
            public int Column { get; set; }
            public int Row { get; set; }
            public TerrainType Type { get; set; }
        }

        public sealed class FenceSaveData
        {
            public CellSaveData First { get; set; } = new();
            public CellSaveData Second { get; set; } = new();
        }

        public sealed class CellSaveData
        {
            public int Column { get; set; }
            public int Row { get; set; }

            // Создает данные клетки из позиции
            public static CellSaveData From(CellPosition cell) =>
                new()
                {
                    Column = cell.Column,
                    Row = cell.Row
                };

            // Превращает данные клетки обратно в позицию
            public CellPosition ToCellPosition() =>
                new(Column, Row);
        }
    }
}
