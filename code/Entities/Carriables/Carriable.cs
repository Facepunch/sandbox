using Sandbox;

public partial class Carriable : BaseCarriable, IUse
{
	public virtual string ModelPath { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		if ( string.IsNullOrWhiteSpace( ModelPath ) )
			return;

		UpdateModel();
	}

	public virtual void UpdateModel()
	{
		SetModel( ModelPath );
	}

	public virtual void UpdateViewModel()
	{
		Host.AssertClient();

		ViewModelEntity?.Delete();
		CreateViewModel();
	}

	public override void CreateViewModel()
	{
		Host.AssertClient();

		if ( string.IsNullOrWhiteSpace( ViewModelPath ) )
			return;

		ViewModelEntity = new ViewModel
		{
			Position = Position,
			Owner = Owner,
			EnableViewmodelRendering = true
		};

		ViewModelEntity.SetModel( ViewModelPath );
	}

	public bool OnUse( Entity user )
	{
		return false;
	}

	public virtual bool IsUsable( Entity user )
	{
		return Owner is null;
	}
}
