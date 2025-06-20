using System.Collections.Generic;
using System.Linq;
using KindredCommands.Commands.Converters;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;

namespace KindredCommands.Commands;
[CommandGroup("boss")]
internal class BossCommands
{
	[Command("modify", description: "Muda o level do boss mais próximo.", adminOnly: true)]
	public static void ModifyBossCommand(ChatCommandContext ctx, FoundVBlood boss, int level)
	{
		var entityManager = Core.EntityManager;
		var playerEntity = ctx.Event.SenderCharacterEntity;
        var playerPos = playerEntity.Read<LocalToWorld>().Position;
        var closestVBlood = Entity.Null;
        var closestDistance = float.MaxValue;
		
		foreach (var entity in Helper.GetEntitiesByComponentType<VBloodUnit>(includeDisabled: true).ToArray().Where(x => Vector3.Distance(x.Read<Translation>().Value, Vector3.zero) > 1))
		{
			if (entity.Read<PrefabGUID>().GuidHash != boss.Value.GuidHash)
				continue;

			if (Vector3.Distance(entity.Read<Translation>().Value, playerPos) < closestDistance)
			{
				closestDistance = Vector3.Distance(entity.Read<Translation>().Value, playerPos);
				closestVBlood = entity;
			}
		}

		if (closestVBlood.Equals(Entity.Null))
        {
            ctx.Reply($"Não foi possível achar '{boss.Name}' para modificar");
            return;
        }
		
		var unitLevel = closestVBlood.Read<UnitLevel>();
		var previousLevel = unitLevel.Level;
		unitLevel.Level._Value = level;
        closestVBlood.Write<UnitLevel>(unitLevel);

		ctx.Reply($"Mudou o boss {boss.Name} para o level {level} do level {previousLevel}");
	}

	[Command("mp", description: "Modifica o level do boss primal mais próximo.", adminOnly: true)]
	public static void ModifyPrimalBossCommand(ChatCommandContext ctx, FoundPrimal boss, int level)
	{
		var entityManager = Core.EntityManager;
		var playerEntity = ctx.Event.SenderCharacterEntity;
		var playerPos = playerEntity.Read<LocalToWorld>().Position;
		var closestVBlood = Entity.Null;
		var closestDistance = float.MaxValue;

		foreach (var entity in Helper.GetEntitiesByComponentType<VBloodUnit>(includeDisabled: true).ToArray().Where(x => Vector3.Distance(x.Read<Translation>().Value, Vector3.zero) > 1))
		{
			if (entity.Read<PrefabGUID>().GuidHash != boss.Value.GuidHash)
				continue;

			if (Vector3.Distance(entity.Read<Translation>().Value, playerPos) < closestDistance)
			{
				closestDistance = Vector3.Distance(entity.Read<Translation>().Value, playerPos);
				closestVBlood = entity;
			}
		}

		if (closestVBlood.Equals(Entity.Null))
		{
			ctx.Reply($"Não foi possível achar o boss '{boss.Name}' para modificar");
			return;
		}

		var unitLevel = closestVBlood.Read<UnitLevel>();
		var previousLevel = unitLevel.Level;
		unitLevel.Level._Value = level;
		closestVBlood.Write<UnitLevel>(unitLevel);

		ctx.Reply($"Alterou o boss {boss.Name} para o level {level} do level {previousLevel}");
	}

	[Command("travar", description: "Desabilita o spawn do boss.", adminOnly: true)]
	public static void LockBossCommand(ChatCommandContext ctx, FoundVBlood boss)
	{
		if (Core.Boss.LockBoss(boss))
			ctx.Reply($"Locked {boss.Name}");
		else
			ctx.Reply($"{boss.Name} is already locked");
	}


	[Command("destravar", description: "Habilita o spawn do boss.", adminOnly: true)]
	public static void UnlockBossCommand(ChatCommandContext ctx, FoundVBlood boss)
	{
		if(Core.Boss.UnlockBoss(boss))
			ctx.Reply($"Destravou o boss {boss.Name}");
		else
			ctx.Reply($"{boss.Name} já está destravado!");
	}

	[Command("lockprimal", description: "Desabiita certo boss primal de spawnar.", adminOnly: true)]
	public static void LockPrimalBossCommand(ChatCommandContext ctx, FoundPrimal primalBoss)
	{
		var boss = new FoundVBlood(primalBoss.Value, "Primal "+primalBoss.Name);
		if (Core.Boss.LockBoss(boss))
			ctx.Reply($"Travou {boss.Name}");
		else
			ctx.Reply($"{boss.Name} já está bloqueado!");
	}

	[Command("unlockprimal", description: "Habilita certo boss primal de spawnar.", adminOnly: true)]
	public static void UnlockPrimalBossCommand(ChatCommandContext ctx, FoundPrimal primalBoss)
	{
		var boss = new FoundVBlood(primalBoss.Value, "Primal " + primalBoss.Name);
		if (Core.Boss.UnlockBoss(boss))
			ctx.Reply($"Habilitou o boss {boss.Name}");
		else
			ctx.Reply($"{boss.Name} já está liberado!");

        }
    }
}
