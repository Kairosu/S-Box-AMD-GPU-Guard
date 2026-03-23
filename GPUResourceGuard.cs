using Sandbox;
using System.Linq;

// Fixes AMD Vulkan crash from envmap probes leaking descriptors across play/stop cycles
// Drop this on any GameObject in your scene
public sealed class GpuResourceGuard : Component
{
	public static GpuResourceGuard Instance { get; private set; }

	[Property] public bool DisableEnvmapRerender { get; set; } = true;
	[Property] public int MapLoadDelayFrames { get; set; } = 10;
	[Property] public int MaxSearchFrames { get; set; } = 600;

	private enum GuardState { WaitingForGpuSettle, WaitingForMapLoad, WaitingForProbes, Complete }

	private GuardState _state = GuardState.WaitingForGpuSettle;
	private int _frameCount;
	private int _searchFrames;
	private MapInstance _mapInstance;
	private bool _mapWasEnabled;

	protected override void OnAwake()
	{
		Instance = this;

		if ( !DisableEnvmapRerender )
			return;

		// Hold off on loading the map so the GPU can clean up from last session
		_mapInstance = Scene.GetAll<MapInstance>().FirstOrDefault();
		if ( _mapInstance != null && _mapInstance.Enabled )
		{
			_mapWasEnabled = true;
			_mapInstance.Enabled = false;
			Log.Info( "[GpuGuard] MapInstance disabled, delaying map load for GPU settle" );
		}
	}

	protected override void OnUpdate()
	{
		if ( _state == GuardState.Complete || !DisableEnvmapRerender )
			return;

		switch ( _state )
		{
			case GuardState.WaitingForGpuSettle:
				_frameCount++;
				if ( _frameCount < MapLoadDelayFrames )
					return;
				if ( _mapInstance != null && _mapWasEnabled )
				{
					_mapInstance.Enabled = true;
					Log.Info( $"[GpuGuard] MapInstance re-enabled after {_frameCount} frame delay" );
				}
				_state = GuardState.WaitingForMapLoad;
				_searchFrames = 0;
				break;

			case GuardState.WaitingForMapLoad:
			case GuardState.WaitingForProbes:
				_searchFrames++;
				int disabled = 0;
				foreach ( var probe in Scene.GetAll<EnvmapProbe>() )
				{
					probe.Enabled = false;
					disabled++;
				}
				if ( disabled > 0 )
				{
					Log.Info( $"[GpuGuard] Disabled {disabled} envmap probe(s) at search frame {_searchFrames}" );
					_state = GuardState.Complete;
					return;
				}
				if ( _searchFrames > MaxSearchFrames )
				{
					Log.Info( "[GpuGuard] No envmap probes found, map may have none" );
					_state = GuardState.Complete;
				}
				break;
		}
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;

		// Kill everything we can on the way out so descriptors get freed
		foreach ( var probe in Scene.GetAll<EnvmapProbe>() )
			probe.Enabled = false;

		foreach ( var cam in Scene.GetAll<CameraComponent>() )
		{
			if ( cam.IsValid && !cam.IsMainCamera )
			{
				cam.RenderTarget = null;
				cam.Enabled = false;
			}
		}

		Log.Info( "[GpuGuard] Session end cleanup, rendering state released" );
	}
}
