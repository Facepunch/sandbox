internal class ConstraintCleanup : Component
{
	[Property]
	public GameObject Attachment { get; set; }

	protected override void OnDestroy()
	{
		if ( Attachment.IsValid() )
		{
			Attachment.Destroy();
		}

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if (  !Attachment.IsValid() )
		{
			DestroyGameObject();
			return;
		}
	}
}
