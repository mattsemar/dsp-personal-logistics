using CommonAPI;

namespace PersonalLogistics.PlayerInventory
{
    /// <summary>Provides storage that the player can dump items into</summary>
    public class RecycleBuffer : IStorage
    {
        private readonly int _size = 10;
        private readonly IItem[] _contents = new IItem[10];
        public IItem GetAt(int index) => _contents[index];

        public void SetAt(int index, IItem items)
        {
            _contents[index] = items;
        }

        public int size => _size;

        public bool changed { get; set; }
    }
}