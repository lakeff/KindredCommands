using System;
using System.Collections.Generic;
using System.Linq;
using KindredCommands.Commands.Converters;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Terrain;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;

namespace KindredCommands.Commands;

[CommandGroup("clan")]
class ClanCommands
{
    [Command("add", description: "Adiciona um player no clã", adminOnly: false)]
    public static void AddToClan(ChatCommandContext ctx, OnlinePlayer playerToAdd, string clanName)
    {
        var userToAddEntity = playerToAdd.Value.UserEntity;
		var user = userToAddEntity.Read<User>();
		var limitType = CastleHeartLimitType.User;
		if (!user.ClanEntity.Equals(NetworkedEntity.Empty))
		{
			var clanTeam = user.ClanEntity.GetEntityOnServer().Read<ClanTeam>();
			ctx.Reply($"Jogador já esta em um clã!");
			return;
		}

		if (!FindClan(clanName, out var clanEntity))
        {
            ctx.Reply($"No clan found matching name '{clanName}'");
            return;
        }

        TeamUtility.AddUserToClan(Core.EntityManager, clanEntity, userToAddEntity, ref user, limitType);
        userToAddEntity.Write<User>(user);

        var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clanEntity);
        var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(clanEntity);

        for (var i = 0; i < members.Length; ++i)
        {
            var member = members[i];
            var userBufferEntry = userBuffer[i];
            var userToTest = userBufferEntry.UserEntity.Read<User>();
            if (userToTest.CharacterName.Equals(user.CharacterName))
            {
                member.ClanRole = ClanRoleEnum.Member;
                members[i] = member;
            }
        }

        ctx.Reply($"{playerToAdd.Value.CharacterName} convidado para o clã {clanEntity.Read<ClanTeam>().Name}");
    }

	[Command("kick", description: "Remove o jogador de um clã", adminOnly: true)]
	public static void RemoveFromClan(ChatCommandContext ctx, OnlinePlayer playerToRemove)
	{
		var clanEntity = playerToRemove.Value.UserEntity.Read<User>().ClanEntity.GetEntityOnServer();
		if (clanEntity.Equals(Entity.Null))
		{
			ctx.Reply("Jogador não está em um clã!");
			return;
		}


		var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clanEntity);
		var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(clanEntity);
		bool foundLeader = false;
		FromCharacter fromCharacter = new();
		for (var i = 0; i < members.Length; ++i)
		{
			var member = members[i];
			if (member.ClanRole == ClanRoleEnum.Leader)
			{
				var userBufferEntry = userBuffer[i];
				fromCharacter = new FromCharacter()
				{
					Character = userBufferEntry.UserEntity.Read<User>().LocalCharacter.GetEntityOnServer(),
					User = userBufferEntry.UserEntity
				};
				foundLeader = true;
				break;
			}
		}

		if (!foundLeader)
		{
			ctx.Reply("Nenhum líder foi encontrado.");
			return;
		}

		for (var i = 0; i < members.Length; ++i)
		{
			var userBufferEntry = userBuffer[i];
			if (userBufferEntry.UserEntity.Equals(playerToRemove.Value.UserEntity))
			{
				var member = members[i];
				if (member.ClanRole == ClanRoleEnum.Leader)
				{
					ctx.Reply("Não é possível remover o líder do clã.");
					return;
				}

				var archetype = Core.EntityManager.CreateArchetype(new ComponentType[]
				{
					ComponentType.ReadWrite<FromCharacter>(),
					ComponentType.ReadWrite<ClanEvents_Client.Kick_Request>()
				});

				var entity = Core.EntityManager.CreateEntity(archetype);
				entity.Write(fromCharacter);

				entity.Write(new ClanEvents_Client.Kick_Request()
				{
					TargetUserIndex = members[i].UserIndex
				});

				Core.Log.LogInfo($"Expulsando {userBufferEntry.UserEntity.Read<User>().CharacterName}\n" +
							$"FromCharacter {fromCharacter.Character} {fromCharacter.User} TargetUserIndex: {members[i].UserIndex}");
				ctx.Reply($"{playerToRemove.Value.CharacterName} removido do clã {clanEntity.Read<ClanTeam>().Name}");
				return;
			}
		}
	}

	[Command("listar", description: "Lista os clãs do servidor.")]
    public static void ListClans(ChatCommandContext ctx, int page = 1)
    {
        var clanList = new List<string>();
        foreach (var clan in Helper.GetEntitiesByComponentType<ClanTeam>())
        {
            var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clan);
            if (members.Length == 0) continue;

            var clanTeam = clan.Read<ClanTeam>();
            clanList.Add($"{clanTeam.Name} - {clanTeam.Motto}");
        }

        // Set newest clans first
        clanList.Reverse();

        const int clanBatchSize = 8;
        // Group the clans into batches
        var groupedClans = clanList
            .Select((name, index) => new { Index = index, Value = name })
            .GroupBy(x => x.Index / clanBatchSize)
            .Select(group => group.Select(x => x.Value)).ToList();

        var totalPages = groupedClans.Count;
        if (totalPages == 0)
        {
            ctx.Reply("Sem clãs.");
            return;
        }

        page = Mathf.Clamp(page, 1, totalPages);

        ctx.Reply($"Clãs (Page {page}/{totalPages})\n" + String.Join("\n", groupedClans[page - 1]));
    }


    [Command("membros", description: "Lista os membros do clã.")]
    public static void ListClanMembers(ChatCommandContext ctx, string clanName)
    {
        if (!FindClan(clanName, out var clanEntity))
        {
            ctx.Reply($"Nenhum clã com o nome: '{clanName}'");
            return;
        }

        var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clanEntity);
        var memberList = new List<string>();
        var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(clanEntity);

        for (var i = 0; i < members.Length; ++i)
        {
            var member = members[i];
            var userBufferEntry =	userBuffer[i];
            var user = userBufferEntry.UserEntity.Read<User>();
			memberList.Add($"{user.CharacterName} - {member.ClanRole}");
        }

        ctx.Reply($"Membros no clã: '{clanEntity.Read<ClanTeam>().Name}'\n" + string.Join("\n", memberList));
    }


    [Command("mudarcargo", description: "Muda o cargo de um jogador do clã.", adminOnly: true)]
    public static void ChangeClanRole(ChatCommandContext ctx, OnlinePlayer player, ClanRoleEnum newRole)
    {
        var user = player.Value.UserEntity.Read<User>();
        if (user.ClanEntity.Equals(NetworkedEntity.Empty))
        {
            ctx.Reply($"{player.Value.CharacterName} não está em um clã.");
            return;
        }

        var clanRole = player.Value.UserEntity.Read<ClanRole>();
        var oldRole = clanRole.Value;
        clanRole.Value = newRole;
        player.Value.UserEntity.Write<ClanRole>(clanRole);
		ctx.Reply($"Alterou {player.Value.CharacterName} o cargo de {oldRole} para {newRole}");
	}

	[Command("castles", "c", description: "List castles owned by a clan", adminOnly: true)]
    public static void ListClanCastles(ChatCommandContext ctx, string clanName)
    {
        if (!FindClan(clanName, out var clanEntity))
        {
            ctx.Reply($"No clan found matching name '{clanName}'");
            return;
        }

        var teamValue = clanEntity.Read<ClanTeam>().TeamValue;
        var castleHearts = Helper.GetEntitiesByComponentType<CastleHeart>();
        var castleList = new List<string>();
        int castleCount = 0; // Initialize castle count

        foreach (var castle in castleHearts)
        {
            var heartTeam = castle.Read<Team>().Value;
            if (heartTeam != teamValue) continue;
            var ownerEntity = castle.Read<UserOwner>().Owner.GetEntityOnServer();
            var owner = ownerEntity.Read<User>();
            var castleData = castle.Read<CastleHeart>();
            var castleTerritoryEntity = castleData.CastleTerritoryEntity;
            var region = castleTerritoryEntity.Read<TerritoryWorldRegion>().Region;

            castleList.Add($"{owner.CharacterName} - {castleData.CastleTerritoryEntity.Read<CastleTerritory>().CastleTerritoryIndex} in {region} ");
            castleCount++; 
        }

        ctx.Reply($"Castles owned by Clan '{clanEntity.Read<ClanTeam>().Name}' (Total: {castleCount})\n" + string.Join("\n", castleList));
	}

	public static bool FindClan(string clanName, out Entity clanEntity)
	{
		var clans = Helper.GetEntitiesByComponentType<ClanTeam>().ToArray();
		var matchedClans = clans.Where(x => x.Read<ClanTeam>().Name.ToString().ToLower() == clanName.ToLower());

		foreach (var clan in matchedClans)
		{
			var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clan);
			if (members.Length == 0) continue;
			clanEntity = clan;
			return true;
		}
		clanEntity = new Entity();
		return false;
	}
	public static List<Entity> FindClans(string clanName)
	{
		var matchingClans = new List<Entity>();
		var clans = Helper.GetEntitiesByComponentType<ClanTeam>().ToArray();

		var matchedClans = clans.Where(x => x.Read<ClanTeam>().Name.ToString().ToLower() == clanName.ToLower());
		foreach (var clan in matchedClans)
		{
			var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clan);
			if (members.Length == 0) continue; // Skip empty clans
			matchingClans.Add(clan);
		}

		return matchingClans;
	}
}

