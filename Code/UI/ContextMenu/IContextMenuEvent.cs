/// <summary>
/// Lets scene components add actions to an object's right-click menu.
/// </summary>
public interface IContextMenuEvent : ISceneEvent<IContextMenuEvent>
{
	public readonly record struct Event( MenuPanel Menu, GameObject Target );

	void PopulateContextMenu( Event e ) { }
}
