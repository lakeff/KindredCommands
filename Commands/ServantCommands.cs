using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.CastleBuilding;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;
using static KindredCommands.Commands.SpawnCommands;

namespace KindredCommands.Commands;

[CommandGroup("servant")]
internal class ServantCommands
{
	static EntityQuery servantCoffinQuery = default;
	public static Entity FindClosestServantCoffin(Vector3 pos)
	{
		if (servantCoffinQuery == default)
		{
			var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
				.AddAll(new(Il2CppType.Of<TilePosition>(), ComponentType.AccessMode.ReadOnly))
				.AddAll(new(Il2CppType.Of<Translation>(), ComponentType.AccessMode.ReadOnly))
				.AddAll(new(Il2CppType.Of<ServantCoffinstation>(), ComponentType.AccessMode.ReadOnly))
				.WithOptions(EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludeSpawnTag);
			servantCoffinQuery = Core.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
		}

		var closestEntity = Entity.Null;
		var closestDistance = float.MaxValue;
		var entities = servantCoffinQuery.ToEntityArray(Allocator.Temp);
		for (var i = 0; i < entities.Length; ++i)
		{
			var entity = entities[i];
			if (!entity.Has<TilePosition>()) continue;
			var entityPos = entity.Read<Translation>().Value;
			var distance = math.distancesq(pos, entityPos);
			if (distance < closestDistance)
			{
				var prefabName = Helper.GetPrefabGUID(entity).LookupName();
				if (!prefabName.StartsWith("TM_")) continue;

				closestDistance = distance;
				closestEntity = entity;
			}
		}
		entities.Dispose();

		return closestEntity;
	}

	[Command("servo", "Faz o servo do caixão ficar perfeito.", adminOnly: false)]
	public static void PerfectServant(ChatCommandContext ctx)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Não está mirando no caixão.");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();
		coffin.BloodQuality = 100;
		coffin.ServantProficiency = 0.44f;
		closest.Write(coffin);

		var servant = coffin.ConnectedServant.GetEntityOnServer();
		if (servant != Entity.Null)
		{
			var stats = servant.Read<ServantPower>();
			stats.Power = 20;
			stats.Expertise = 0.44f;
			servant.Write(stats);
		}

		ctx.Reply($"Servo <color=white>{coffin.ServantName}</color> agora é perfeito.");
	}

	//[Command("seteyecolor", "sc", "Sets the eye color of the servant in the coffin", adminOnly: true)]
	public static void SetServantEyeColor(ChatCommandContext ctx, int colorIndex)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Não está mirando no caixão.");
			return;
		}
		var coffin = closest.Read<ServantCoffinstation>();
		coffin.ServantEyeColorIndex = (byte)colorIndex;
		closest.Write(coffin);

		var servant = coffin.ConnectedServant.GetEntityOnServer();
		if (servant != Entity.Null)
		{
			var stats = servant.Read<NPCServantColorIndex>();
			stats.EyeColorIndex = (byte)colorIndex;
			servant.Write(stats);
		}

		ctx.Reply($"Servant <color=white>{coffin.ServantName}</color>'s eye color is now <color=white>{colorIndex}</color>.");
	}

	[Command("change", "ch", "Changes the servant in the coffin to a different servant", adminOnly: true)]
	public static void ChangeServant(ChatCommandContext ctx, CharacterUnit character)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Not pointing at a servant coffin.");
			return;
		}

		var servantName = character.Name + "_Servant";
		if (!Core.Prefabs.SpawnableNameToGuid.TryGetValue(servantName.ToLower(), out var toPrefab))
		{
			ctx.Reply($"Can't find a servant prefab for the type {character.Name}");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();
		var servant = coffin.ConnectedServant.GetEntityOnServer();

		if (servant != Entity.Null && coffin.State == ServantCoffinState.ServantAlive)
		{
			StatChangeUtility.KillEntity(Core.EntityManager, servant, Entity.Null, 0.0, StatChangeReason.Default, true);
			coffin.State = ServantCoffinState.Reviving;
			coffin.ConvertionProgress = 600;
			coffin.ConvertFromUnit = character.Prefab;
			coffin.ConvertToUnit = toPrefab.Prefab;
			closest.Write(coffin);
		}
		
		ctx.Reply($"Servant has been changed to <color=white>{character.Name}</color>.");
	}

	[Command("heal", description: "Cures the servant of injuries", adminOnly: true)]
	public static void HealServant(ChatCommandContext ctx)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Not pointing at a servant coffin.");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();
		var servant = coffin.ConnectedServant.GetEntityOnServer();
		if (servant != Entity.Null)
		{
			coffin.InjuryEndTimeTicks = 1;
			closest.Write(coffin);
		}

		ctx.Reply($"Servant <color=white>{coffin.ServantName}</color> has been healed or injuries.");
	}

	[Command("revive", "r", "Revives the servant in the coffin", adminOnly: true)]
	public static void ReviveServant(ChatCommandContext ctx)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Not pointing at a servant coffin.");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();

		if (coffin.State == ServantCoffinState.ServantRevivable)
		{
			coffin.State = ServantCoffinState.Reviving;
			coffin.ConvertionProgress = 600;
			closest.Write(coffin);
		}
		else if (coffin.State == ServantCoffinState.Reviving)
		{
			coffin.ConvertionProgress = 600;
			closest.Write(coffin);
		}

		ctx.Reply($"Servant <color=white>{coffin.ServantName}</color> is now revived.");
	}

	[Command("completemission", "cm", "Completes the mission of the servant in the coffin", adminOnly: true)]
	public static void CompleteMissionServant(ChatCommandContext ctx)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Not pointing at a servant coffin.");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();

		var castleHeartEntity = closest.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
		var activeServantMissions = Core.EntityManager.GetBuffer<ActiveServantMission>(castleHeartEntity);
		for(var i = 0; i < activeServantMissions.Length; i++)
		{
			var mission = activeServantMissions[i]	;
			if (mission.Servant1.Equals(coffin.ConnectedServant) ||
				mission.Servant2.Equals(coffin.ConnectedServant) ||
				mission.Servant3.Equals(coffin.ConnectedServant))
			{
				mission.MissionLengthSeconds = 1;
				activeServantMissions[i] = mission;
				break;
			}
		}

		ctx.Reply($"Servant <color=white>{coffin.ServantName}</color>'s mission is now completed.");
	}

	[Command("add", "a", "Adds a servant to an empty coffin", adminOnly: true)]
	public static void AddServant(ChatCommandContext ctx, CharacterUnit character)
	{
		var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
		var closest = FindClosestServantCoffin(aimPos);
		if (closest == Entity.Null)
		{
			ctx.Reply("Not pointing at a servant coffin.");
			return;
		}

		var servantName = character.Name + "_Servant";
		if (!Core.Prefabs.SpawnableNameToGuid.TryGetValue(servantName.ToLower(), out var toPrefab))
		{
			ctx.Reply($"Can't find a servant prefab for the type {character.Name}");
			return;
		}

		var coffin = closest.Read<ServantCoffinstation>();
		if (coffin.State == ServantCoffinState.Empty)
		{
			coffin.State = ServantCoffinState.Reviving;
			coffin.ConvertionProgress = 600;
			coffin.ConvertFromUnit = character.Prefab;
			coffin.ConvertToUnit = toPrefab.Prefab;
			closest.Write(coffin);
		}
		else
		{
			ctx.Reply("Coffin is not empty.");
		}
	}

}
