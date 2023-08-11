using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Sandbox
{
	public partial class VertexMeshBuilder
	{
		public List<MeshVertex> vertices = new();
		public static Dictionary<string, Model> Models = new();
		public static string CreateRectangleModel( Vector3 size, int texSize = 64 )
		{
			var key = $"rect_{size.x}_{size.y}_{size.z}_{texSize}";
			if ( Models.ContainsKey( key ) )
			{
				return key;
			}

			var mins = size * -0.5f;
			var maxs = size * 0.5f;
			var vertexBuilder = new VertexMeshBuilder();
			vertexBuilder.AddRectangle( Vector3.Zero, size, texSize, Color.White );

			var mesh = new Mesh( Material.Load( "materials/default/vertex_color.vmat" ) );

			mesh.CreateVertexBuffer<MeshVertex>( vertexBuilder.vertices.Count, MeshVertex.Layout, vertexBuilder.vertices.ToArray() );
			mesh.SetBounds( mins, maxs );

			var modelBuilder = new ModelBuilder();
			modelBuilder.AddMesh( mesh );
			var box = new BBox( mins, maxs );
			modelBuilder.AddCollisionBox( box.Size * 0.5f, box.Center );
			modelBuilder.WithMass( box.Size.x * box.Size.y * box.Size.z / 1000 );

			Models[key] = modelBuilder.Create();
			return key;
		}

		[ConCmd.Server( "spawn_dynplate" )]
		public static void SpawnEntityCmd( int length, int width, int height, int texScale = 100 )
		{
			if ( ConsoleSystem.Caller == null )
				return;
			var entity = SpawnEntity( length, width, height, texScale );
			SandboxPlayer pawn = ConsoleSystem.Caller.Pawn as SandboxPlayer;
			TraceResult trace = Trace.Ray( pawn.EyePosition, pawn.EyePosition + pawn.EyeRotation.Forward * 5000.0f ).UseHitboxes().Ignore( pawn ).Run();

			entity.Position = trace.EndPosition + trace.Normal;
			Event.Run( "entity.spawned", entity, ConsoleSystem.Caller.Pawn );
		}

		public static MeshEntity SpawnEntity( int length, int width, int height, int texScale = 64 )
		{
			var vertexModel = GenerateRectangleServer( length, width, height, texScale );
			MeshEntity entity = new() { ModelId = vertexModel };
			entity.Tick();
			return entity;
		}

		[ClientRpc]
		public static void GenerateRectangleClient( int length, int width, int height, int texSize )
		{
			GenerateRectangle( length, width, height, texSize );
		}
		public static string GenerateRectangleServer( int length, int width, int height, int texSize )
		{
			GenerateRectangleClient( length, width, height, texSize );
			return GenerateRectangle( length, width, height, texSize );
		}
		public static string GenerateRectangle( int length, int width, int height, int texSize )
		{
			return CreateRectangleModel( new Vector3( length, width, height ), texSize );
		}

		private void AddRectangle( Vector3 position, Vector3 size, int texSize, Color color = new Color() )
		{
			Rotation rot = Rotation.Identity;

			var f = size.x * rot.Forward * 0.5f;
			var l = size.y * rot.Left * 0.5f;
			var u = size.z * rot.Up * 0.5f;

			CreateQuad( vertices, new Ray( position + f, f.Normal ), l, u, texSize, color );
			CreateQuad( vertices, new Ray( position - f, -f.Normal ), l, -u, texSize, color );

			CreateQuad( vertices, new Ray( position + l, l.Normal ), -f, u, texSize, color );
			CreateQuad( vertices, new Ray( position - l, -l.Normal ), f, u, texSize, color );

			CreateQuad( vertices, new Ray( position + u, u.Normal ), f, l, texSize, color );
			CreateQuad( vertices, new Ray( position - u, -u.Normal ), f, -l, texSize, color );
		}

		public static void CreateQuad( List<MeshVertex> vertices, Ray origin, Vector3 width, Vector3 height, int texSize = 64, Color color = new Color() )
		{
			Vector3 normal = origin.Forward;
			var tangent = width.Normal;

			MeshVertex a = new( origin.Position - width - height, normal, tangent, new Vector2( 0, 0 ), color );
			MeshVertex b = new( origin.Position + width - height, normal, tangent, new Vector2( width.Length / texSize, 0 ), color );
			MeshVertex c = new( origin.Position + width + height, normal, tangent, new Vector2( width.Length / texSize, height.Length / texSize ), color );
			MeshVertex d = new( origin.Position - width + height, normal, tangent, new Vector2( 0, height.Length / texSize ), color );

			vertices.Add( a );
			vertices.Add( b );
			vertices.Add( c );

			vertices.Add( c );
			vertices.Add( d );
			vertices.Add( a );
		}


		[StructLayout( LayoutKind.Sequential )]
		public struct MeshVertex
		{
			public Vector3 Position;
			public Vector3 Normal;
			public Vector3 Tangent;
			public Vector2 TexCoord;
			public Color Color;

			public MeshVertex( Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord, Color color )
			{
				Position = position;
				Normal = normal;
				Tangent = tangent;
				TexCoord = texCoord;
				Color = color;
			}

			public static readonly VertexAttribute[] Layout = {
				new VertexAttribute(VertexAttributeType.Position, VertexAttributeFormat.Float32, 3),
				new VertexAttribute(VertexAttributeType.Normal, VertexAttributeFormat.Float32, 3),
				new VertexAttribute(VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 3),
				new VertexAttribute(VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2),
				new VertexAttribute(VertexAttributeType.Color, VertexAttributeFormat.Float32, 4)
			};
		}
	}
}
