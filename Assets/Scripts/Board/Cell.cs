namespace BlockBlastGame
{
    [System.Serializable]
    public class Cell
    {
        public CellState state;
        public BlockColorType colorType;
        public ItemData itemData;

        public Cell()
        {
            state = CellState.Empty;
            colorType = BlockColorType.Red;
            itemData = null;
        }

        public bool IsEmpty => state == CellState.Empty;
        public bool IsFilled => state == CellState.Filled;
        public bool HasItem => state == CellState.Item || itemData != null;

        public void Fill(BlockColorType color)
        {
            state = CellState.Filled;
            colorType = color;
        }

        public void PlaceItem(ItemData item)
        {
            itemData = item;
            if (state == CellState.Empty)
                state = CellState.Item;
        }

        public ItemData ClearAndCollectItem()
        {
            var item = itemData;
            Clear();
            return item;
        }

        public void ClearItem()
        {
            itemData = null;
            if (state == CellState.Item)
                state = CellState.Empty;
        }

        public void Clear()
        {
            state = CellState.Empty;
            itemData = null;
        }
    }
}
