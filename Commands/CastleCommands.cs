using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KindredCommands.Commands.Converters;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Terrain;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;

namespace KindredCommands.Commands;

internal class CastleCommands
{
	[Command("claim", description: "Claima o castelo mais próximo para um player escolhido.", adminOnly: true)]
	public static void CastleClaim(ChatCommandContext ctx, OnlinePlayer player = null)
	{
		Entity newOwnerUser = player?.Value.UserEntity ?? ctx.Event.SenderUserEntity;

		var castleHearts = Helper.GetEntitiesByComponentType<CastleHeart>();
		var playerPos = ctx.Event.SenderCharacterEntity.Read<LocalToWorld>().Position;
		var limitType = CastleHeartLimitType.User;
		foreach (var castleHeart in castleHearts)
		{
			var castleHeartPos = castleHeart.Read<LocalToWorld>().Position;

			if (Vector3.Distance(playerPos, castleHeartPos) > 5f)
			{
				continue;
			}

			var name = player?.Value.CharacterName.ToString() ?? ctx.Name;

			ctx.Reply($"Claimando o castelo para {name}");

			TeamUtility.ClaimCastle(Core.EntityManager, newOwnerUser, castleHeart, limitType);
			return;
		}
		ctx.Reply("Você não está perto o suficiente de o castelo!");

	}

	[Command("spots", description: "Mostra todos spots abertos ou em decay.")]
	public static void OpenPlots(ChatCommandContext ctx)
	{
		Dictionary<WorldRegionType, int> openPlots = [];
		Dictionary<WorldRegionType, int> plotsInDecay = [];
		foreach (var castleTerritoryEntity in Helper.GetEntitiesByComponentType<CastleTerritory>())
		{
			var castleTerritory = castleTerritoryEntity.Read<CastleTerritory>();
			if (!castleTerritory.CastleHeart.Equals(Entity.Null))
			{
				var castleHeart = castleTerritory.CastleHeart.Read<CastleHeart>();
				if ((castleHeart.FuelEndTime - Core.ServerTime) > 0 || castleHeart.FuelQuantity > 0) continue;
				
				var region = castleTerritoryEntity.Read<TerritoryWorldRegion>().Region;
				if(plotsInDecay.ContainsKey(region))
				{
					plotsInDecay[region]++;
				}
				else
				{
					plotsInDecay[region] = 1;
				}
				continue;
			}
			else
			{
				var region = castleTerritoryEntity.Read<TerritoryWorldRegion>().Region;
				if(openPlots.ContainsKey(region))
				{
					openPlots[region]++;
				}
				else
				{
					openPlots[region] = 1;
				}
			}	
		}
		var stringList = new List<string>();

		foreach(var plot in openPlots)
		{
			if(plotsInDecay.ContainsKey(plot.Key))
			{
				stringList.Add($"{RegionName(plot.Key)} has {plot.Value} open plots and {plotsInDecay[plot.Key]} plots in decay");
			}
			else
			{
				stringList.Add($"{RegionName(plot.Key)} has {plot.Value} open plots");
			}
		}
		foreach(var plot in plotsInDecay)
		{
			if(!openPlots.ContainsKey(plot.Key))
			{
				stringList.Add($"{RegionName(plot.Key)} has {plot.Value} plots in decay");
			}
		}
		stringList.Sort();

		var sb = new StringBuilder();
		foreach (var appendString in stringList)
		{
			if (sb.Length + appendString.Length > Core.MAX_REPLY_LENGTH)
			{
				ctx.Reply(sb.ToString());
				sb.Clear();
			}
			sb.AppendLine(appendString);
		}

		if (stringList.Count == 0)
			sb.AppendLine("No open or decaying plots");

		ctx.Reply(sb.ToString());
	}

	public static string RegionName(WorldRegionType region)
	{
		return Regex.Replace(region.ToString().Replace("_", ""), "(?<!^)([A-Z])", " $1");
	}

	[Command("plotsowned", "po", description: "Reports the number of plots owned by each player", adminOnly: true)]
    public static void PlotsOwned(ChatCommandContext ctx, int? page = null)
    {
        var castleTerritories = Helper.GetEntitiesByComponentType<CastleTerritory>();
        var playerPlots = new Dictionary<Entity, int>();
        foreach (var castleTerritoryEntity in castleTerritories)
        {
			var castleTerritory = castleTerritoryEntity.Read<CastleTerritory>();
            if (castleTerritory.CastleHeart.Equals(Entity.Null)) continue;

            var userOwner = castleTerritory.CastleHeart.Read<UserOwner>();
            if (playerPlots.ContainsKey(userOwner.Owner.GetEntityOnServer()))
            {
                playerPlots[userOwner.Owner.GetEntityOnServer()]++;
            }
            else
            {
                playerPlots[userOwner.Owner.GetEntityOnServer()] = 1;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Players by Plots Owned");
        int count = 0;
        int startIndex = (page ?? 1) == 1 ? 0 : ((page ?? 1) - 1) * 8;
        foreach (var playerPlot in playerPlots.OrderByDescending(x => x.Value).Skip(startIndex).Take(8))
        {
            var user = playerPlot.Key.Read<User>();
            sb.AppendLine($"{user.CharacterName} owns {playerPlot.Value} plots");
            count++;
            if (count % 8 == 0)
            {
                ctx.Reply(sb.ToString());
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            ctx.Reply(sb.ToString());
        }
    }
	[Command("freezeheart", "Freezes the time left on a castle heart, keeping it from ever decaying", adminOnly:true)]
	public static void NeverDecay(ChatCommandContext ctx)
	{
		var castleHearts = Helper.GetEntitiesByComponentType<CastleHeart>();
		var playerPos = ctx.Event.SenderCharacterEntity.Read<LocalToWorld>().Position;
		foreach (var castleHeart in castleHearts)
		{
			var castleHeartPos = castleHeart.Read<LocalToWorld>().Position;

			if (Vector3.Distance(playerPos, castleHeartPos) > 5f)
			{
				continue;
			}

			var castleHeartComponent = castleHeart.Read<CastleHeart>();
			castleHeartComponent.FuelEndTime = double.PositiveInfinity;
			castleHeart.Write(castleHeartComponent);
			ctx.Reply("Castle Heart will never decay");
			return;
		}
		ctx.Reply("Not close enough to a castle heart");
	}
	[Command("frozenhearts", description: "Lists all the castle hearts that will never decay", adminOnly: true)]
	public static void NeverDecayList(ChatCommandContext ctx, int page = 1)
	{

		var castleHeartEntities = Helper.GetEntitiesByComponentType<CastleHeart>();
		var nonDecayingHearts = castleHeartEntities.ToArray()
			.Select(x => (Entity: x, CastleHeart: x.Read<CastleHeart>()))
			.Where(x => double.IsPositiveInfinity(x.CastleHeart.FuelEndTime));

		if (!nonDecayingHearts.Any())
		{
			ctx.Reply("No Castle Hearts are frozen in time");
			return;
		}

		var numOfPages = (nonDecayingHearts.Count() + 7)/ 8;
		if (numOfPages < page)
		{
			ctx.Reply($"No more Castle Hearts to display ({numOfPages} pages)");
			return;
		}

		var sb = new StringBuilder();
		sb.AppendLine($"<color=lightblue>Frozen Hearts ({page}/{numOfPages})</color>:");
		foreach (var (castleHeartEntity, castleHeart) in nonDecayingHearts.Skip((page - 1) * 8).Take(8))
		{
			var userOwner = castleHeartEntity.Read<UserOwner>();
			var user = userOwner.Owner.GetEntityOnServer().Read<User>();
			var castleTerritory = castleHeart.CastleTerritoryEntity.Read<CastleTerritory>();
			sb.AppendLine($"<color=white>{user.CharacterName}</color>'s castle heart at territory <color=white>{castleTerritory.CastleTerritoryIndex}</color>");
		}

		ctx.Reply(sb.ToString());
	}
	[Command("thawheart", description: "Removes the frozen time from a castle heart and resumes ticking down", adminOnly:true)]
	public static void NeverDecayRemove(ChatCommandContext ctx)
	{
		var castleHearts = Helper.GetEntitiesByComponentType<CastleHeart>();
		var playerPos = ctx.Event.SenderCharacterEntity.Read<LocalToWorld>().Position;
		foreach (var castleHeart in castleHearts)
		{
			var castleHeartPos = castleHeart.Read<LocalToWorld>().Position;

			if (Vector3.Distance(playerPos, castleHeartPos) > 5f)
			{
				continue;
			}

			var castleHeartComponent = castleHeart.Read<CastleHeart>();
			castleHeartComponent.FuelEndTime = 0;
			castleHeart.Write(castleHeartComponent);
			ctx.Reply("Castle Heart will decay normally");
			return;
		}
		ctx.Reply("Not close enough to a castle heart");
	}

	[Command("clanplotsowned", "cpo", description: "Reports the number of plots owned by each clan", adminOnly: true)]
	public static void ClanPlotsOwned(ChatCommandContext ctx, int? page = null)
	{
		var castleTerritories = Helper.GetEntitiesByComponentType<CastleTerritory>();
		var clanPlots = new Dictionary<Entity, int>();
		foreach (var castleTerritoryEntity in castleTerritories)
		{
			var castleTerritory = castleTerritoryEntity.Read<CastleTerritory>();
			if (castleTerritory.CastleHeart.Equals(Entity.Null)) continue;

			var userOwner = castleTerritory.CastleHeart.Read<UserOwner>();
			var user = userOwner.Owner.GetEntityOnServer().Read<User>();

			if (user.ClanEntity.Equals(NetworkedEntity.Empty)) continue;

			if (clanPlots.ContainsKey(user.ClanEntity.GetEntityOnServer()))
			{
				clanPlots[user.ClanEntity.GetEntityOnServer()]++;
			}
			else
			{
				clanPlots[user.ClanEntity.GetEntityOnServer()] = 1;
			}
		}

		var sb = new StringBuilder();
		sb.AppendLine("Clans by Plots Owned");
		int count = 0;
		int startIndex = (page ?? 1) == 1 ? 0 : ((page ?? 1) - 1) * 8;
		foreach (var clanPlot in clanPlots.OrderByDescending(x => x.Value).Skip(startIndex).Take(8))
		{
			var clan = clanPlot.Key.Read<ClanTeam>();
			sb.AppendLine($"{clan.Name} owns {clanPlot.Value} plots.");
			count++;
			if (count % 8 == 0)
			{
				ctx.Reply(sb.ToString());
				sb.Clear();
			}
		}

		if (sb.Length > 0)
		{
			ctx.Reply(sb.ToString());
		}
	}
	[Command("teleporttoplot", "tpp", description: "Teleports you to the castle heart of the specified territory", adminOnly: true)]
	public static void TeleportToPlot(ChatCommandContext ctx, int territoryIndex)
	{
		var castleTerritories = Helper.GetEntitiesByComponentType<CastleTerritory>();
		foreach (var castleTerritoryEntity in castleTerritories)
		{
			var castleTerritory = castleTerritoryEntity.Read<CastleTerritory>();
			if (castleTerritory.CastleTerritoryIndex != territoryIndex) continue;

			var castleHeart = castleTerritory.CastleHeart;
			float3 teleportTo;
			if (castleHeart.Equals(Entity.Null))
			{
				var territoryCenter = Core.TerritoryLocation.GetTerritoryCenter(territoryIndex);

				if (territoryCenter.Equals(float2.zero))
				{
					ctx.Reply("Territory has no center");
					return;
				}

				teleportTo = new float3(territoryCenter.x, 0, territoryCenter.y);
			}
			else
			{
				var castleHeartPos = castleHeart.Read<LocalToWorld>().Position;
				teleportTo = new float3(castleHeartPos.x + 1.5f, castleHeartPos.y, castleHeartPos.z);
			}
            var user = ctx.Event.SenderUserEntity.Read<User>();
            var charEntity = user.LocalCharacter.GetEntityOnServer();
            charEntity.Write(new Translation { Value = teleportTo });
            charEntity.Write(new LastTranslation { Value = teleportTo });
			ctx.Reply($"Teleported to territory {territoryIndex}");
			return;
		}

		ctx.Reply("Territory not found");
	}

	[Command("plotinfo", description: "Reports information about the territory specified", adminOnly: true)]
	public static void PlotInfo(ChatCommandContext ctx, int territoryIndex)
	{
		var castleTerritories = Helper.GetEntitiesByComponentType<CastleTerritory>();
		foreach (var castleTerritoryEntity in castleTerritories)
		{
			var castleTerritory = castleTerritoryEntity.Read<CastleTerritory>();
			if (castleTerritory.CastleTerritoryIndex != territoryIndex) continue;

			var castleHeart = castleTerritory.CastleHeart;
			if (castleHeart.Equals(Entity.Null))
			{
				ctx.Reply("Territory has no castle heart");
				return;
			}

			var userOwner = castleHeart.Read<UserOwner>();
			var user = userOwner.Owner.GetEntityOnServer().Read<User>();
			var region = castleTerritoryEntity.Read<TerritoryWorldRegion>().Region;
			var secondsRemaining = GetFuelTimeRemaining(castleHeart);
			var sb = new StringBuilder();
			sb.AppendLine($"Castle {territoryIndex} in {RegionName(region)}");
			sb.AppendLine($"Owner: {user.CharacterName}");
			if (!user.ClanEntity.Equals(NetworkedEntity.Empty))
			{
				var clan = user.ClanEntity.GetEntityOnServer().Read<ClanTeam>();
				sb.AppendLine($"Clan: {clan.Name}");
			}
			if (secondsRemaining == double.PositiveInfinity)
			{
				sb.AppendLine("Time Remaining: Infinite");
			}
			else
			{
				var time = TimeSpan.FromSeconds(secondsRemaining);
				if (time >  TimeSpan.Zero)
					sb.AppendLine($"Time Remaining: {time.Days}d {time.Hours}h {time.Minutes}m");
				else
					sb.AppendLine($"Time in Decay: {time.Days}d {time.Hours}h {time.Minutes}m");
			}
			ctx.Reply(sb.ToString());
			return;
		}

		ctx.Reply("Territory not found");
	}

}
