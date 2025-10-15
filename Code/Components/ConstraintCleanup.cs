using System;
using System.Collections.Generic;

internal class ConstraintCleanup : Component
{
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
