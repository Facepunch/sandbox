using System.Collections.Generic;

namespace Sandbox.Tools
{
	public partial class BaseTool
	{
		internal List<PreviewEntity> Previews;

		protected virtual bool IsPreviewTraceValid( TraceResult tr )
		{
			if ( !tr.Hit )
				return false;

			if ( !tr.Entity.IsValid() )
				return false;

			return true;
		}

		public virtual void CreatePreviews()
		{
			// Nothing
		}

		public virtual void DeletePreviews()
		{
			if ( Previews == null || Previews.Count == 0 )
				return;

			foreach ( var preview in Previews )
			{
				preview.Delete();
			}

			Previews.Clear();
		}


		public virtual bool TryCreatePreview( ref PreviewEntity ent, string model )
		{
			if ( !ent.IsValid() )
			{
				ent = new PreviewEntity();
				ent.SetModel( model );
			}

			if ( Previews == null )
			{
				Previews = new List<PreviewEntity>();
			}

			if ( !Previews.Contains( ent ) )
			{
				Previews.Add( ent );
			}

			return ent.IsValid();
		}


		private void UpdatePreviews()
		{
			if ( Previews == null || Previews.Count == 0 )
				return;

			if ( !Owner.IsValid() )
				return;

			var startPos = Owner.EyePos;
			var dir = Owner.EyeRot.Forward;

			var tr = Trace.Ray( startPos, startPos + dir * 10000.0f )
				.Ignore( Owner )
				.Run();

			foreach ( var preview in Previews )
			{
				if ( !preview.IsValid() )
					continue;

				if ( IsPreviewTraceValid( tr ) && preview.UpdateFromTrace( tr ) )
				{
					preview.RenderColor = preview.RenderColor.WithAlpha( 0.5f );
				}
				else
				{
					preview.RenderColor = preview.RenderColor.WithAlpha( 0.0f );
				}
			}
		}
	}
}
