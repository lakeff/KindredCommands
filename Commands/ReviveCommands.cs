using KindredCommands.Commands.Converters;
using KindredCommands.Models;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using VampireCommandFramework;

namespace KindredCommands.Commands;

internal class ReviveCommands
{
	[Command("revive", adminOnly: true)]
	public static void ReviveCommand(ChatCommandContext ctx, FoundPlayer player = null)
	{
		var character = player?.Value.CharEntity ?? ctx.Event.SenderCharacterEntity;
		var user = player?.Value.UserEntity ?? ctx.Event.SenderUserEntity;

		Helper.ReviveCharacter(character, user);

		ctx.Reply($"Reviveu {user.Read<User>().CharacterName}");
	}
	
	[Command("revivetarget", adminOnly: true)]
	public static void ReviveTargetCommand(ChatCommandContext ctx)
	{
		var charEntity = ctx.Event.SenderCharacterEntity;

		var entityInput = charEntity.Read<EntityInput>();
		if (entityInput.HoveredEntity != Entity.Null)
		{
			if (entityInput.HoveredEntity.Has<PlayerCharacter>())
			{
				var playerCharacter = entityInput.HoveredEntity.Read<PlayerCharacter>();
				var name = playerCharacter.Name;
				if (entityInput.HoveredEntity.Read<Health>().Value <= 0)
				{

					Helper.ReviveCharacter(entityInput.HoveredEntity, playerCharacter.UserEntity);
					ctx.Reply($"Revived {name}");
				}
				else
				{
					ctx.Reply($"{name} is not dead");
				}
			}
			else
			{
				ctx.Reply("Target is not a player");
			}
		}
		else
		{
			ctx.Reply("No target selected");
		}
	}

}
