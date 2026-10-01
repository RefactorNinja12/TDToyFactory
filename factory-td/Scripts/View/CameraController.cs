using Godot;

namespace FactoryTD.View;

/// <summary>
/// Debug camera.
/// Pan: WASD / arrows, or two-finger drag on a touchpad.
/// Zoom: mouse wheel, touchpad pinch, Q/E or -/+ keys. Home resets.
/// </summary>
public partial class CameraController : Camera2D
{
	[Export] public float PanSpeed = 1500f;
	[Export] public float KeyZoomSpeed = 2f;  // zoom factor per second while a key is held
	[Export] public float MinZoom = 0.1f;
	[Export] public float MaxZoom = 3f;
	[Export] public float ZoomStep = 1.15f;

	private Vector2 _startPosition;
	private Vector2 _startZoom;

	public override void _Ready()
	{
		_startPosition = Position;
		_startZoom = Zoom;
	}

	/// <summary>Build hotkeys: while they want a letter (or it is still held), the camera leaves it alone.</summary>
	public FactoryTD.UI.BuildHotkeys Hotkeys { get; set; }

	private bool Held(Key key) => Input.IsKeyPressed(key) && (Hotkeys == null || !Hotkeys.BlocksCamera((char)(long)key));

	public override void _Process(double delta)
	{
		var dir = Vector2.Zero;
		if (Held(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
		if (Held(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
		if (Held(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
		if (Held(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;

		// Divide by zoom so panning feels the same at every zoom level.
		Position += dir.Normalized() * PanSpeed * (float)delta / Zoom.X;

		float zoomDir = 0;
		if (Held(Key.E) || Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.KpAdd)) zoomDir += 1;
		if (Held(Key.Q) || Input.IsKeyPressed(Key.Minus) || Input.IsKeyPressed(Key.KpSubtract)) zoomDir -= 1;
		if (zoomDir != 0)
			SetZoom(Zoom.X * Mathf.Pow(KeyZoomSpeed, zoomDir * (float)delta));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
				ZoomAt(Zoom.X * ZoomStep, wheel.Position);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
				ZoomAt(Zoom.X / ZoomStep, wheel.Position);
				break;
			case InputEventMagnifyGesture pinch:
				ZoomAt(Zoom.X * pinch.Factor, pinch.Position);
				break;
			case InputEventPanGesture pan:
				Position += pan.Delta * 20f / Zoom.X;
				break;
			case InputEventKey { Pressed: true, Keycode: Key.Home }:
				Position = _startPosition;
				SetZoom(_startZoom.X);
				break;
		}
	}

	/// <summary>Zooms while keeping the world point under the cursor fixed.</summary>
	private void ZoomAt(float zoom, Vector2 screenPoint)
	{
		var before = GetGlobalMousePositionFor(screenPoint);
		SetZoom(zoom);
		Position += before - GetGlobalMousePositionFor(screenPoint);
	}

	private Vector2 GetGlobalMousePositionFor(Vector2 screenPoint) =>
		GetCanvasTransform().AffineInverse() * screenPoint;

	private void SetZoom(float zoom)
	{
		float z = Mathf.Clamp(zoom, MinZoom, MaxZoom);
		Zoom = new Vector2(z, z);
		ForceUpdateScroll();
	}
}
