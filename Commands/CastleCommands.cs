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

	[Command("plotsowned", description: "Mostra a quantidade de spots de um jogador.", adminOnly: true)]
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
        sb.AppendLine("Jogador por spots pegos");
        int count = 0;
        int startIndex = (page ?? 1) == 1 ? 0 : ((page ?? 1) - 1) * 8;
        foreach (var playerPlot in playerPlots.OrderByDescending(x => x.Value).Skip(startIndex).Take(8))
        {
            var user = playerPlot.Key.Read<User>();
            sb.AppendLine($"{user.CharacterName} possui {playerPlot.Value} spots");
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

	[Command("clanspots", description: "Mostra a quantidade de spots de um clã.", adminOnly: false)]
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
		sb.AppendLine("Clãs por spots");
		int count = 0;
		int startIndex = (page ?? 1) == 1 ? 0 : ((page ?? 1) - 1) * 8;
		foreach (var clanPlot in clanPlots.OrderByDescending(x => x.Value).Skip(startIndex).Take(8))
		{
			var clan = clanPlot.Key.Read<ClanTeam>();
			sb.AppendLine($"{clan.Name} possui {clanPlot.Value} spots.");
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
}
