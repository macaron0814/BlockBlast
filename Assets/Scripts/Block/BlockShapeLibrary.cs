using UnityEngine;
using System.Collections.Generic;

namespace BlockBlastGame
{
    public static class BlockShapeLibrary
    {
        private static List<BlockData> _cachedShapes;

        public static List<BlockData> GenerateAllShapes()
        {
            if (_cachedShapes != null) return _cachedShapes;

            var shapes = new List<BlockData>();

            // 1x1 Single
            shapes.Add(BlockData.CreateShape("Single", BlockColorType.Red, 1, 1,
                new[] { true }));

            // 1x2 Vertical
            shapes.Add(BlockData.CreateShape("Vert2", BlockColorType.Blue, 1, 2,
                new[] { true, true }));

            // 2x1 Horizontal
            shapes.Add(BlockData.CreateShape("Horiz2", BlockColorType.Blue, 2, 1,
                new[] { true, true }));

            // 1x3 Vertical
            shapes.Add(BlockData.CreateShape("Vert3", BlockColorType.Green, 1, 3,
                new[] { true, true, true }));

            // 3x1 Horizontal
            shapes.Add(BlockData.CreateShape("Horiz3", BlockColorType.Green, 3, 1,
                new[] { true, true, true }));

            // 1x4 Vertical
            shapes.Add(BlockData.CreateShape("Vert4", BlockColorType.Cyan, 1, 4,
                new[] { true, true, true, true }));

            // 4x1 Horizontal
            shapes.Add(BlockData.CreateShape("Horiz4", BlockColorType.Cyan, 4, 1,
                new[] { true, true, true, true }));

            // 1x5 Vertical
            shapes.Add(BlockData.CreateShape("Vert5", BlockColorType.Cyan, 1, 5,
                new[] { true, true, true, true, true }));

            // 5x1 Horizontal
            shapes.Add(BlockData.CreateShape("Horiz5", BlockColorType.Cyan, 5, 1,
                new[] { true, true, true, true, true }));

            // 2x2 Square
            shapes.Add(BlockData.CreateShape("Square2", BlockColorType.Yellow, 2, 2,
                new[] { true, true, true, true }));

            // 3x3 Square
            shapes.Add(BlockData.CreateShape("Square3", BlockColorType.Yellow, 3, 3,
                new[] { true, true, true, true, true, true, true, true, true }));

            // L-shape
            shapes.Add(BlockData.CreateShape("L", BlockColorType.Orange, 2, 3,
                new[] {
                    true, false,
                    true, false,
                    true, true
                }));

            // L-shape mirrored
            shapes.Add(BlockData.CreateShape("L_Mirror", BlockColorType.Orange, 2, 3,
                new[] {
                    false, true,
                    false, true,
                    true, true
                }));

            // L-shape rotated
            shapes.Add(BlockData.CreateShape("L_Rot1", BlockColorType.Orange, 3, 2,
                new[] {
                    true, true, true,
                    true, false, false
                }));

            // L-shape rotated mirror
            shapes.Add(BlockData.CreateShape("L_Rot2", BlockColorType.Orange, 3, 2,
                new[] {
                    true, true, true,
                    false, false, true
                }));

            // T-shape
            shapes.Add(BlockData.CreateShape("T", BlockColorType.Purple, 3, 2,
                new[] {
                    true, true, true,
                    false, true, false
                }));

            // T-shape rotations
            shapes.Add(BlockData.CreateShape("T_Down", BlockColorType.Purple, 3, 2,
                new[] {
                    false, true, false,
                    true, true, true
                }));

            // S-shape
            shapes.Add(BlockData.CreateShape("S", BlockColorType.Green, 3, 2,
                new[] {
                    false, true, true,
                    true, true, false
                }));

            // Z-shape
            shapes.Add(BlockData.CreateShape("Z", BlockColorType.Red, 3, 2,
                new[] {
                    true, true, false,
                    false, true, true
                }));

            // Small L (2x2)
            shapes.Add(BlockData.CreateShape("SmallL", BlockColorType.Orange, 2, 2,
                new[] {
                    true, false,
                    true, true
                }));

            // Small L mirrored
            shapes.Add(BlockData.CreateShape("SmallL_Mirror", BlockColorType.Orange, 2, 2,
                new[] {
                    false, true,
                    true, true
                }));

            _cachedShapes = shapes;
            return _cachedShapes;
        }
    }
}
