using System;
using KindredCommands.Commands.Converters;
using KindredCommands.Services;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;

namespace KindredCommands.Commands;
internal class BuffCommands
{	public record struct BuffInput(string Name, PrefabGUID Prefab);

	public class BuffConverter : CommandArgumentConverter<BuffInput>
	{
		public override BuffInput Parse(ICommandContext ctx, string input)
		{
			if (Core.Prefabs.TryGetBuff(input, out PrefabGUID buffPrefab))
			{
				return new(buffPrefab.LookupName(), buffPrefab);
			}
			
			if (int.TryParse(input, out var id))
			{
				var prefabGuid = new PrefabGUID(id);
				if (Core.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out var prefabEntity)
					&& prefabEntity.Has<Buff>())
				{
					var name = Core.Prefabs.CollectionSystem._PrefabLookupMap.GetName(prefabGuid);
					return new(name, prefabGuid);
				}
			}

			throw ctx.Error($"Can't find buff {input.Bold()}");
		}
	}

	[Command("buff", adminOnly: true)]
	public static void BuffCommand(ChatCommandContext ctx, BuffInput buff,OnlinePlayer player = null, int duration = 0, bool immortal = false)
	{
		var userEntity = player?.Value.UserEntity ?? ctx.Event.SenderUserEntity;
		var charEntity = player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity;

		Buffs.AddBuff(userEntity, charEntity, buff.Prefab, duration, immortal);
		ctx.Reply($"Buff {buff.Name} aplicado para {userEntity.Read<User>().CharacterName}");
	}

	[Command("debuff", adminOnly: true)]
	public static void DebuffCommand(ChatCommandContext ctx, BuffInput buff, OnlinePlayer player = null)
	{
		var targetEntity = (Entity)(player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity);
		Buffs.RemoveBuff(targetEntity, buff.Prefab);
		ctx.Reply($"Buff {buff.Name} removido de {targetEntity.Read<PlayerCharacter>().Name}");
	}

	[Command("listbuffs", description: "Lista os buffs de um player.", adminOnly: true)]
	public static void ListBuffsCommand(ChatCommandContext ctx, OnlinePlayer player = null)
	{
		var Character = player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity;
		var buffEntities = Helper.GetEntitiesByComponentTypes<Buff, PrefabGUID>();
		foreach (var buffEntity in buffEntities)
		{
			if (buffEntity.Read<EntityOwner>().Owner == Character)
			{
				ctx.Reply(buffEntity.Read<PrefabGUID>().LookupName());
			}
		}
	}

	internal static void DebuffCommand(Entity character, PrefabGUID buff_InCombat_PvPVampire)
	{
		throw new NotImplementedException();
	}
}
