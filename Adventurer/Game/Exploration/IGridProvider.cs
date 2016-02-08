namespace Adventurer.Game.Exploration
{
    public interface IGridProvider
    {
        void Update();
        IGrid<INode> NavigationGrid { get; }
        IGrid<INode> ExplorationGrid { get; }
    }
}
