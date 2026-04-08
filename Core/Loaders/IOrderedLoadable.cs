namespace TerraCraft.Core.Loaders
{
    public interface IOrderedLoadable
    {
        int Priority { get; }
        void Load();
        void Unload();
    }
    public enum LoadPriority
    {
        Highest = -100,
        High = -50,
        Normal = 0,
        Low = 50,
        Lowest = 100
    }
}