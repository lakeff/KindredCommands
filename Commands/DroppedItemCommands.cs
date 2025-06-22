using Unity.Transforms;
using VampireCommandFramework;

namespace KindredCommands.Commands;

[CommandGroup("dropitems", "Commands for managing dropped items.")]
internal class DroppedItemCommands
{
	//reduce the time a dropped item stays on the ground
	[Command("lifetime", description: "Sets the lifetime of dropped items in seconds.", adminOnly: true)]
	public static void SetDroppedItemLifetime(ChatCommandContext ctx, int seconds)
	{
		if (seconds < 0)
		{
			throw ctx.Error("Lifetime must be a positive number.");
		}
		Core.DropItem.SetDroppedItemLifetime(seconds);
		ctx.Reply($"Dropped item lifetime set to {seconds} seconds.");
	}

	[Command("removelifetime", description:"Removes the lifetime of dropped items.", adminOnly: true)]
	public static void RemoveDroppedItemLifetime(ChatCommandContext ctx)
	{
		Core.DropItem.RemoveDroppedItemLifetime();
		ctx.Reply("Dropped item lifetime removed.");
	}

	[Command("lifetimewhendisabled", description:"Sets the lifetime of dropped items when disabled in seconds.", adminOnly: true)]
	public static void SetDroppedItemLifetimeWhenDisabled(ChatCommandContext ctx, int seconds=300)
	{
		if (seconds < 0)
		{
			throw ctx.Error("Lifetime must be a positive number.");
		}
		Core.DropItem.SetDroppedItemLifetimeWhenDisabled(seconds);
		ctx.Reply($"Dropped item lifetime when disabled set to {seconds} seconds.");
	}

	[Command("shardlifetime", description: "Sets the lifetime of dropped shards when disabled in seconds.", adminOnly: true)]
	public static void SetDroppedShardLifetimeWhenDisabled(ChatCommandContext ctx, int seconds=3600)
	{
		if (seconds < 0)
		{
			throw ctx.Error("Lifetime must be a positive number.");
		}
		Core.DropItem.SetDroppedShardLifetime(seconds);
		ctx.Reply($"Dropped shard lifetime set to {seconds} seconds.");
	}

	//remove dropped items around the player in a radius
	[Command("clear", description: "Clears all dropped items within a radius of the player.", adminOnly: true)]
	public static void ClearDroppedItems(ChatCommandContext ctx, float radius)
	{
		var pos = ctx.Event.SenderCharacterEntity.Read<Translation>().Value;
		var cleared = Core.DropItem.ClearDropItemsInRadius(pos, radius);
		ctx.Reply($"Cleared {cleared}x dropped items within a radius of {radius}.");
	}

	[Command("clearall", description: "Clears all dropped items in the world.", adminOnly: true)]
	public static void ClearAllDroppedItems(ChatCommandContext ctx)
	{
		var cleared = Core.DropItem.ClearDropItems();
		ctx.Reply($"Cleared all {cleared} dropped items in the world.");
	}

	[Command("clearshards", description: "Clears all dropped shards within a radius of the player.", adminOnly: true)]
	public static void ClearDroppedShards(ChatCommandContext ctx, float radius)
	{
		var pos = ctx.Event.SenderCharacterEntity.Read<Translation>().Value;
		var cleared = Core.DropItem.ClearDropShardsInRadius(pos, radius);
		ctx.Reply($"Cleared {cleared} dropped shards within a radius of {radius}.");
	}

	[Command("clearallshards", description: "Clears all dropped shards in the world.", adminOnly: true)]
	public static void ClearAllDroppedShards(ChatCommandContext ctx)
	{
		var cleared = Core.DropItem.ClearDropShards();
		ctx.Reply($"Cleared all {cleared} dropped shards in the world.");
	}
}
