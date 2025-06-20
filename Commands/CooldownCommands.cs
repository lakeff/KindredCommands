using System;
using KindredCommands.Commands.Converters;
using ProjectM;
using Unity.Entities;
using VampireCommandFramework;

namespace KindredCommands.Commands;

public static class ResetCooldown
{
	[Command("r", "Reseta todos cooldowns do jogador.", adminOnly: true)]
	public static void ResetCooldownCommand(ChatCommandContext ctx, FoundPlayer player = null)
	{
		var playerCharacter = player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity;
		var abilityBuffer = Core.EntityManager.GetBuffer<AbilityGroupSlotBuffer>(playerCharacter);
		foreach (var ability in abilityBuffer)
		{
			var abilitySlot = ability.GroupSlotEntity._Entity;
			var activeAbility = Core.EntityManager.GetComponentData<AbilityGroupSlot>(abilitySlot);
			var activeAbility_Entity = activeAbility.StateEntity._Entity;

			var b = Helper.GetPrefabGUID(activeAbility_Entity);
			if (b.GuidHash == 0) continue;

			var abilityStateBuffer = Core.EntityManager.GetBuffer<AbilityStateBuffer>(activeAbility_Entity);
			foreach (var state in abilityStateBuffer)
			{
				var abilityState = state.StateEntity._Entity;
				var abilityCooldownState = Core.EntityManager.GetComponentData<AbilityCooldownState>(abilityState);
				abilityCooldownState.CooldownEndTime = 0;
				Core.EntityManager.SetComponentData(abilityState, abilityCooldownState);
			}
		}

		var name = player?.Value.CharacterName.ToString() ?? ctx.Name;
		ctx.Reply($"Player {name}'s cooldowns have been reset.");
	}

	internal static void Initialize(Entity character)
	{
		throw new NotImplementedException();
	}
}
