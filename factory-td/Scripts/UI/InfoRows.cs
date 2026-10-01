using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

public enum Tone { Normal, Dim, Good, Missing }
public enum RowKind { Title, Text, Item, Progress }

/// <summary>One line of the hover panel. Item rows show the item's icon; progress rows are a bar (Done of Total).</summary>
public readonly record struct InfoRow(RowKind Kind, string Text, Tone Tone = Tone.Normal, ItemType Item = ItemType.None, int Done = 0, int Total = 0);

/// <summary>What the hover panel says about a building: pure data, drawn by View.InfoPanel.</summary>
public sealed class InfoRows
{
	private readonly World _world;
	private readonly int _localPlayer;
	private readonly List<InfoRow> _rows = new();

	private InfoRows(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
	}

	public static List<InfoRow> For(World world, Building building, int localPlayer)
	{
		var rows = new InfoRows(world, localPlayer);
		rows.Fill(building);
		return rows._rows;
	}

	private void Fill(Building building)
	{
		string owner = building.Owner == _localPlayer ? "" : "  (fiende)";
		Title(Texts.DisplayName(building.Type) + owner);
		Text($"Hälsa {building.Health}/{building.MaxHealth}", building.Health < building.MaxHealth ? Tone.Missing : Tone.Dim);

		if (!building.IsBuilt)
		{
			int builders = _world.BuildersOn(building);
			int percent = building.BuildTime == 0 ? 100 : building.BuildWork * 100 / building.BuildTime;
			Text($"Byggs: {percent}%", Tone.Good);
			Progress(building.BuildWork, building.BuildTime);
			Text(builders == 0
				? "Ingen byggare på väg. Fler byggare får du från en verktygslåda."
				: $"{builders} byggare på väg eller jobbar här.", builders == 0 ? Tone.Missing : Tone.Dim);
			Text("Fungerar inte förrän den är klar.", Tone.Dim);
			return;
		}

		switch (building)
		{
			case UnitFactory factory:
				Text($"Gör: {Texts.UnitName(factory.Produces)}");
				Text("Behöver per trupp:", Tone.Dim);
				Needs(factory.Recipe, factory.Crafter);
				Progress(factory.Crafter.Progress, UnitStats.BuildTicks(factory.Produces));
				Text("Mata in materialet med band från vilken sida som helst.", Tone.Dim);
				break;

			case Assembler assembler:
				Text($"Gör: {Texts.ItemName(assembler.Recipe.Output)}  (klicka för att byta)");
				Text("Behöver per styck:", Tone.Dim);
				Needs(assembler.Recipe.Inputs, assembler.Crafter);
				Progress(assembler.Crafter.Progress, assembler.Recipe.Ticks);
				Text($"Klara, väntar på att komma ut: {assembler.Finished}", Tone.Dim);
				Text("Tar emot från alla håll, lämnar ut åt pilens håll.", Tone.Dim);
				break;

			case Tower tower:
				Item(tower.Stats.Ammo, $"Ammo: {Texts.ItemName(tower.Stats.Ammo).ToLowerInvariant()}  (1 = {tower.Stats.ShotsPerItem} skott)");
				Text($"Skott kvar: {tower.Shots}/{tower.Stats.MaxShots}", tower.Shots > 0 ? Tone.Good : Tone.Missing);
				Text($"Räckvidd {tower.Stats.RangeTiles} rutor, skada {tower.Stats.Damage}", Tone.Dim);
				break;

			case Extractor extractor:
				Item(extractor.Output, $"Gör: {Texts.ItemCount(extractor.Output, 1)} var {Extractor.ProductionTicks / World.TicksPerSecond}:a sekund");
				Text($"I lager: {extractor.Stored}/{Extractor.MaxStored}", extractor.Stored >= Extractor.MaxStored ? Tone.Missing : Tone.Dim);
				Text("Lämnar till band på alla sidor (utom band som pekar in i den).", Tone.Dim);
				break;

			case Sorter sorter:
				Item(sorter.Filter, $"{Texts.ItemName(sorter.Filter)} rakt fram, allt annat åt sidorna");
				Text("Klicka för att byta sort.", Tone.Dim);
				break;

			case Kitchen kitchen:
				Item(ItemType.Food, "Gör: 1 matlåda");
				Text("Behöver per styck:", Tone.Dim);
				Needs(Kitchen.Recipe, kitchen.Crafter);
				Text("eller", Tone.Dim);
				Needs(Kitchen.CheeseRecipe, kitchen.CheeseCrafter);
				Progress(System.Math.Max(kitchen.Crafter.Progress, kitchen.CheeseCrafter.Progress), Kitchen.CookTicks);
				Text($"Klar mat som väntar på att komma ut: {kitchen.Finished}", kitchen.Finished >= 5 ? Tone.Missing : Tone.Dim);
				Text("Bönder lämnar morötter här. Maten går ut åt pilens håll.", Tone.Dim);
				break;

			case CropField field:
				if (field.IsRipe)
					Item(ItemType.Crop, $"Mogen! {Texts.ItemCount(ItemType.Crop, CropField.Yield)} väntar på en bonde", Tone.Good);
				else
				{
					int left = (CropField.GrowTicks - field.Growth + World.TicksPerSecond - 1) / World.TicksPerSecond;
					Item(ItemType.Crop, $"Växer: {field.Growth * 100 / CropField.GrowTicks}%, mogen om {left} s");
					Progress(field.Growth, CropField.GrowTicks);
				}
				Text("Bönder skördar och bär morötterna till ett kök eller lager.", Tone.Dim);
				break;

			case Core:
				Text("Tar emot allt från banden. Det betalar dina byggen.", Tone.Dim);
				Storage(building.Owner);
				break;

			case Warehouse:
				Text($"Lagrar allt från banden, +{PlayerState.WarehouseCapacity} plats per sort.", Tone.Dim);
				Storage(building.Owner);
				break;

			case BatteryCharger charger:
				Item(ItemType.Battery, $"Batterier som väntar: {charger.Batteries}/{PowerStats.ChargerBatteryBuffer}", charger.Batteries > 0 ? Tone.Good : Tone.Missing);
				Text($"Laddning: {PowerMeter.Bolts(charger.Energy)}/{PowerMeter.Bolts(PowerStats.ChargerCapacity)}⚡  (1 batteri = {PowerMeter.Bolts(PowerStats.EnergyPerBattery)}⚡)");
				Progress(charger.Energy, PowerStats.ChargerCapacity);
				Network(building);
				break;

			case Treadmill mill:
				Text($"Tränar: {Texts.UnitName(UnitType.CheeseHunter)}");
				Text("Behöver per ostjägare:", Tone.Dim);
				Needs(Treadmill.Recipe, mill.Crafter);
				if (mill.HasTrainee)
				{
					Text("En byggarmus tränar i ringen.", Tone.Good);
					Progress(mill.Crafter.Progress, UnitStats.BuildTicks(UnitType.CheeseHunter));
				}
				else if (_world.BuildersOn(mill) > 0)
					Text("En byggarmus är på väg.", Tone.Good);
				else if (mill.Crafter.CanWork(Treadmill.Recipe))
					Text("Väntar på en ledig byggarmus (tar aldrig din sista).", Tone.Missing);
				else
					Text("Väntar på smält ost.", Tone.Dim);
				Text("Ingen ström behövs.", Tone.Dim);
				break;

			case Pylon:
				Text($"Ger ström inom {PowerStats.PylonRadius} rutor, sladd till master/laddare inom {PowerStats.LinkRange}.", Tone.Dim);
				Network(building);
				break;

			default:
				var description = Texts.Description(building.Type);
				if (description.Length > 0)
					Text(description, Tone.Dim);
				break;
		}

		if (NeedsPower(building))
			PowerStatus(building);
	}

	/// <summary>Army factories, assemblers and towers run on the grid.</summary>
	private static bool NeedsPower(Building building) => building switch
	{
		UnitFactory factory => !UnitStats.IsWorker(factory.Produces),
		Assembler or Tower => true,
		_ => false,
	};

	private void PowerStatus(Building building)
	{
		var network = _world.NetworkOf(building);
		if (network == null)
			Text("⚡ Ingen ström här: bygg en leksaksmast i närheten.", Tone.Missing);
		else if (building.NoPower || network.Energy == 0)
			Text("⚡ Nätet är tomt: mata en batteriladdare med batterier.", Tone.Missing);
		else
			Text($"⚡ Ström: ok ({PowerMeter.Bolts(network.Energy)}⚡ i nätet)", Tone.Good);
	}

	/// <summary>The network a pylon or charger belongs to.</summary>
	private void Network(Building building)
	{
		PowerNetwork network = null;
		foreach (var n in _world.Power.Networks)
			if (n.Nodes.Contains(building))
				network = n;
		if (network == null)
			return;
		int pylons = 0;
		foreach (var node in network.Nodes)
			if (node is Pylon)
				pylons++;
		Text($"Nätet: {PowerMeter.Bolts(network.Energy)}⚡ lagrat, {pylons} master, {network.Chargers.Count} laddare",
			network.Chargers.Count == 0 || network.Energy == 0 ? Tone.Missing : Tone.Dim);
		if (network.Chargers.Count == 0)
			Text("Ingen batteriladdare i det här nätet.", Tone.Missing);
	}

	/// <summary>The shared stock of toybox + warehouses: what's stored and how much room there is.</summary>
	private void Storage(int owner)
	{
		var player = _world.Players[owner];
		Text($"Förråd (leksakslåda + {player.Warehouses} lager):", Tone.Dim);
		foreach (var item in Items.All)
		{
			int count = player.GetCount(item), capacity = player.Capacity(item);
			if (count == 0 && item is not (ItemType.Brick or ItemType.Plastic or ItemType.Battery))
				continue;
			Item(item, $"{count}/{capacity}", count >= capacity ? Tone.Missing : Tone.Normal);
		}
	}

	private void Needs(ItemStack[] recipe, Crafter crafter)
	{
		foreach (var input in recipe)
		{
			int have = crafter.Stock(input.Type);
			Item(input.Type, $"{Texts.ItemCount(input.Type, input.Amount)}   (har {have})",
				have >= input.Amount ? Tone.Good : Tone.Missing);
		}
	}

	private void Progress(int done, int total) => _rows.Add(new InfoRow(RowKind.Progress, null, Done: done, Total: total));
	private void Title(string text) => _rows.Add(new InfoRow(RowKind.Title, text));
	private void Text(string text, Tone tone = Tone.Normal) => _rows.Add(new InfoRow(RowKind.Text, text, tone));
	private void Item(ItemType item, string text, Tone tone = Tone.Normal) => _rows.Add(new InfoRow(RowKind.Item, text, tone, item));
}
