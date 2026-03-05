
using Sandbox.MovieMaker;

namespace Sandbox;

public static class DemoRecording
{
	[DefaultMovieRecorderOptions]
	public static MovieRecorderOptions BuildMovieRecorderOptions( MovieRecorderOptions options )
	{
		return options
			.WithFilter( x => !x.Tags.Has( "viewmodel" ) )
			.WithFilter( x => x.PrefabInstanceSource?.StartsWith( "prefabs/surface/" ) is not true );
	}
}
