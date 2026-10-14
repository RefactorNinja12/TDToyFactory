namespace FactoryTD.Sim;

// MapLayout partial: the "many rooms" layout between the home rooms. Interior coordinates of the left half; the right
// half is the mirror image (x -> Width - 1 - x).
//
//  x: 62-68  69  70-78 79-80 81-89  90  91-97
//  y 1-12  [ L1 ]-D-[ C1 ] ## [ C1 ]-D-[ R1 ]
//  y 14-24 [ L2 ]   [ C2 ] ## [ C2 ]   [ R2 ]
//  y 26-35 [ LM ]  #[   centre room  ]#  [ RM ]   LM/RM: entry rooms behind the home doors (x 61 / 98)
//  y 37-47 [ L4 ]   [ C4 ] ## [ C4 ]   [ R4 ]
//  y 49-60 [ L5 ]-D-[ C5 ] ## [ C5 ]-D-[ R5 ]
// Vertical doors join each column's rooms (none between LM and the centre), so the way out of a base goes up or down
// through the side rooms, across into the middle column and back towards the middle: the sides meet in the centre.
public sealed partial class MapLayout
{
	/// <summary>The left half's small rooms (inclusive interiors); the centre room spans both halves.</summary>
	internal static readonly (int X0, int Y0, int X1, int Y1)[] SmallRooms =
	{
		(62, 1, 68, 12), (62, 14, 68, 24), (62, 26, 68, 35), (62, 37, 68, 47), (62, 49, 68, 60),
		(70, 1, 78, 12), (70, 14, 78, 24), (70, 37, 78, 47), (70, 49, 78, 60),
	};

	/// <summary>The centre room, where the two sides meet.</summary>
	internal static readonly (int X0, int Y0, int X1, int Y1) CentreRoom = (70, 26, 89, 35);

	/// <summary>The doors between the small rooms in the left half (inclusive), mirrored to the right.</summary>
	internal static readonly (int X0, int Y0, int X1, int Y1)[] SmallRoomDoors =
	{
		(64, 25, 67, 25), (64, 36, 67, 36),   // entry room LM up into L2, down into L4
		(64, 13, 67, 13), (64, 48, 67, 48),   // L2 up into L1, L4 down into L5
		(69, 5, 69, 8), (69, 53, 69, 56),     // L1 across into C1, L5 across into C5
		(72, 13, 75, 13), (72, 48, 75, 48),   // C1 down into C2, C5 up into C4
		(72, 25, 75, 25), (72, 36, 75, 36),   // C2 and C4 into the centre room
	};

	private void AddSmallRooms()
	{
		const int x0 = RoomSize + 2, y1 = RoomSize + 1;
		Fill(x0, 0, Width - 1 - x0, y1, TileType.Wall);
		foreach (var (ax, ay, bx, by) in SmallRooms)
			FillMirrored(ax, ay, bx, by, TileType.Floor);
		var (cx0, cy0, cx1, cy1) = CentreRoom;
		Fill(cx0, cy0, cx1, cy1, TileType.Floor);
		foreach (var (ax, ay, bx, by) in SmallRoomDoors)
			FillMirrored(ax, ay, bx, by, TileType.Floor);
		// The home doors (in the rooms' own walls) open into the entry rooms.
		int doorTop = (y1 + 1 - DoorWidth) / 2;
		FillMirrored(x0 - 1, doorTop, x0 - 1, doorTop + DoorWidth - 1, TileType.Floor);
	}

	/// <summary>A cluster in every small room (mirrored), the big battery field in the centre room.</summary>
	private void AddSmallRoomDeposits()
	{
		AddMirroredDeposit(62, 1, 64, 3, ResourceType.Brick);       // L1
		AddMirroredDeposit(62, 17, 63, 20, ResourceType.Plastic);   // L2
		AddMirroredDeposit(62, 41, 63, 44, ResourceType.Plastic);   // L4
		AddMirroredDeposit(62, 58, 64, 60, ResourceType.Brick);     // L5
		AddMirroredDeposit(76, 1, 78, 3, ResourceType.Cheese);      // C1
		AddMirroredDeposit(70, 17, 71, 21, ResourceType.Battery);   // C2
		AddMirroredDeposit(70, 40, 71, 44, ResourceType.Battery);   // C4
		AddMirroredDeposit(76, 58, 78, 60, ResourceType.Cheese);    // C5
		AddMirroredDeposit(77, 29, 79, 32, ResourceType.Battery);   // the centre: worth fighting over
	}

	private void FillMirrored(int x0, int y0, int x1, int y1, TileType type)
	{
		Fill(x0, y0, x1, y1, type);
		Fill(Width - 1 - x1, y0, Width - 1 - x0, y1, type);
	}
}
